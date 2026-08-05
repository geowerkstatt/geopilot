using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineProcessFactoryValidateTest
{
    [TestMethod]
    public void RejectsInputTargetingUnknownParameter()
    {
        using var factory = CreateFactory();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Builder()
                .StepConfig(ZipStep(new InputConfig { ["nope"] = "${step_output(match.files)}" }))
                .Processes(ZipProcesses())
                .Validate());

        Assert.Contains("nope", exception.Message);
    }

    [TestMethod]
    public void AcceptsInputTargetingKnownParameter()
    {
        using var factory = CreateFactory();

        factory.Builder()
            .StepConfig(ZipStep(new InputConfig { ["input"] = "${step_output(match.files)}" }))
            .Processes(ZipProcesses())
            .Validate();
    }

    [TestMethod]
    public void RejectsStepOutputReferenceToUnknownOutputOfEarlierStep()
    {
        using var factory = CreateFactory();
        var match = MatchStep();
        var zip = ZipStep(new InputConfig { ["input"] = "${step_output(match.DoesNotExist)}" });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Builder()
                .StepConfig(zip)
                .Steps(new List<StepConfig> { match, zip })
                .Processes(MatchAndZipProcesses())
                .Validate());

        Assert.Contains("DoesNotExist", exception.Message);
    }

    [TestMethod]
    public void AcceptsStepOutputReferenceToKnownOutputOfEarlierStep()
    {
        using var factory = CreateFactory();
        var match = MatchStep();
        var zip = ZipStep(new InputConfig { ["input"] = "${step_output(match.MatchedFiles)}" });

        factory.Builder()
            .StepConfig(zip)
            .Steps(new List<StepConfig> { match, zip })
            .Processes(MatchAndZipProcesses())
            .Validate();
    }

    [TestMethod]
    public void RejectsStepOutputReferenceOfIncompatibleTypeFromEarlierStep()
    {
        using var factory = CreateFactory();
        var match = MatchStep();

        // match.StatusMessage is a LocalizedText; the zip 'input' parameter takes IPipelineFile values.
        var zip = ZipStep(new InputConfig { ["input"] = "${step_output(match.StatusMessage)}" });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Builder()
                .StepConfig(zip)
                .Steps(new List<StepConfig> { match, zip })
                .Processes(MatchAndZipProcesses())
                .Validate());

        Assert.Contains("StatusMessage", exception.Message);
    }

    [TestMethod]
    public void RejectsOutputActionTargetingUnknownProperty()
    {
        using var factory = CreateFactory();
        var step = ZipStep(new InputConfig());
        step.OutputActions = new List<OutputActionConfig> { new OutputActionConfig { Property = "DoesNotExist", Actions = new HashSet<OutputAction> { OutputAction.Download } } };

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Builder().StepConfig(step).Processes(ZipProcesses()).Validate());
        Assert.Contains("DoesNotExist", exception.Message);
    }

    [TestMethod]
    public void AcceptsOutputActionTargetingKnownProperty()
    {
        using var factory = CreateFactory();
        var step = ZipStep(new InputConfig());
        step.OutputActions = new List<OutputActionConfig> { new OutputActionConfig { Property = "ZipPackage", Actions = new HashSet<OutputAction> { OutputAction.Download } } };

        factory.Builder().StepConfig(step).Processes(ZipProcesses()).Validate();
    }

    [TestMethod]
    public void AcceptsDownloadActionOnFileCollectionProperty()
    {
        using var factory = CreateFactory();
        var step = new StepConfig
        {
            Id = "step",
            DisplayName = new LocalizedText(new Dictionary<string, string> { ["en"] = "step" }),
            ProcessId = "file_matcher",
            OutputActions = new List<OutputActionConfig>
        {
            new OutputActionConfig { Property = "MatchedFiles", Actions = new HashSet<OutputAction> { OutputAction.Download } },
        },
        };
        var processes = new List<ProcessConfig>
    {
        new ProcessConfig { Id = "file_matcher", Implementation = "Geopilot.Pipeline.Processes.Matcher.FileMatcher.FileMatcherProcess" },
    };

        factory.Builder().StepConfig(step).Processes(processes).Validate();
    }

    [TestMethod]
    public void RejectsDownloadActionOnNonDownloadableProperty()
    {
        using var factory = CreateFactory();
        var step = ZipStep(new InputConfig());
        step.OutputActions = new List<OutputActionConfig> { new OutputActionConfig { Property = "StatusMessage", Actions = new HashSet<OutputAction> { OutputAction.Download } } };

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Builder().StepConfig(step).Processes(ZipProcesses()).Validate());
        Assert.Contains("StatusMessage", exception.Message);
    }

    [TestMethod]
    public void AcceptsStatusMessageActionOnLocalizedTextProperty()
    {
        using var factory = CreateFactory();
        var step = ZipStep(new InputConfig());
        step.OutputActions = new List<OutputActionConfig> { new OutputActionConfig { Property = "StatusMessage", Actions = new HashSet<OutputAction> { OutputAction.StatusMessage } } };

        factory.Builder().StepConfig(step).Processes(ZipProcesses()).Validate();
    }

    [TestMethod]
    public void RejectsStatusMessageActionOnNonLocalizedTextProperty()
    {
        using var factory = CreateFactory();
        var step = ZipStep(new InputConfig());
        step.OutputActions = new List<OutputActionConfig> { new OutputActionConfig { Property = "ZipPackage", Actions = new HashSet<OutputAction> { OutputAction.StatusMessage } } };

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Builder().StepConfig(step).Processes(ZipProcesses()).Validate());

        Assert.Contains("ZipPackage", exception.Message);
    }

    [TestMethod]
    public void AcceptsVisualizationActionOnVisualizationProperty()
    {
        using var factory = CreateFactory();
        var step = new StepConfig
        {
            Id = "step",
            DisplayName = new LocalizedText(new Dictionary<string, string> { ["en"] = "step" }),
            ProcessId = "xtf_error_visualization",
            OutputActions = new List<OutputActionConfig>
        {
            new OutputActionConfig { Property = "Visualization", Actions = new HashSet<OutputAction> { OutputAction.Visualization } },
        },
        };
        var processes = new List<ProcessConfig>
    {
        new ProcessConfig { Id = "xtf_error_visualization", Implementation = "Geopilot.Pipeline.Processes.XtfErrorVisualization.XtfErrorVisualizationProcess" },
    };

        factory.Builder().StepConfig(step).Processes(processes).Validate();
    }

    [TestMethod]
    public void RejectsVisualizationActionOnNonVisualizationProperty()
    {
        using var factory = CreateFactory();
        var step = ZipStep(new InputConfig());
        step.OutputActions = new List<OutputActionConfig> { new OutputActionConfig { Property = "ZipPackage", Actions = new HashSet<OutputAction> { OutputAction.Visualization } } };

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Builder().StepConfig(step).Processes(ZipProcesses()).Validate());

        Assert.Contains("Visualization", exception.Message);
    }

    [TestMethod]
    public void RejectsConditionReferencingUnknownPropertyOfEarlierStep()
    {
        using var factory = CreateFactory();
        var match_step = MatchStep();
        var zip_step = WithPreFailCondition(ZipStep(new InputConfig { ["input"] = "${step_output(match.MatchedFiles)}" }), "[match.DoesNotExist] != null");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Builder()
                .StepConfig(zip_step)
                .Steps(new List<StepConfig> { match_step, zip_step })
                .Processes(MatchAndZipProcesses())
                .Validate());

        Assert.Contains("DoesNotExist", exception.Message);
    }

    [TestMethod]
    public void AcceptsConditionReferencingKnownPropertyOfEarlierStep()
    {
        using var factory = CreateFactory();
        var match_step = MatchStep();
        var zip_step = WithPreFailCondition(ZipStep(new InputConfig { ["input"] = "${step_output(match.MatchedFiles)}" }), "[match.MatchedFiles] != null");

        factory.Builder()
            .StepConfig(zip_step)
            .Steps(new List<StepConfig> { match_step, zip_step })
            .Processes(MatchAndZipProcesses())
            .Validate();
    }

    [TestMethod]
    public void RejectsPostConditionReferencingUnknownPropertyOfCurrentStep()
    {
        using var factory = CreateFactory();
        var zip = WithPostFailCondition(ZipStep(new InputConfig()), "[zip.DoesNotExist]");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Builder()
                .StepConfig(zip)
                .Steps(new List<StepConfig> { zip })
                .Processes(ZipProcesses())
                .Validate());

        Assert.Contains("DoesNotExist", exception.Message);
    }

    [TestMethod]
    public void AcceptsPostConditionReferencingKnownPropertyOfCurrentStep()
    {
        using var factory = CreateFactory();
        var zip = WithPostFailCondition(ZipStep(new InputConfig()), "[zip.ZipPackage] != null");

        factory.Builder()
            .StepConfig(zip)
            .Steps(new List<StepConfig> { zip })
            .Processes(ZipProcesses())
            .Validate();
    }

    [TestMethod]
    public void RejectsConfiguredValueNotConvertibleToNullableParameter()
    {
        using var factory = CreateFactory();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Builder()
                .StepConfig(ValidationStep())
                .Processes(ValidationProcesses(new Parameterization
                {
                    ["checkServiceBaseUrl"] = "http://localhost/",
                    ["pollInterval"] = "abc",
                }))
                .Validate());

        Assert.Contains("pollInterval", exception.Message);
        Assert.Contains("abc", exception.Message);
    }

    [TestMethod]
    public void RejectsUnknownTreeFieldInGroupBy()
    {
        using var factory = CreateFactory();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Builder()
                .StepConfig(VisualizationStep())
                .Processes(VisualizationProcesses(new Parameterization
                {
                    ["groupBy"] = new List<object> { "Error type" },
                }))
                .Validate());

        Assert.Contains("groupBy", exception.Message);
        Assert.Contains("Error type", exception.Message);
    }

    private static PipelineProcessFactory CreateFactory()
    {
        var options = new Mock<IOptions<PipelineOptions>>();
        options.SetupGet(o => o.Value).Returns(new PipelineOptions
        {
            Definition = "pipeline.yaml",
            Plugins = new List<string>(),
            ProcessConfigs = new Dictionary<string, Parameterization>(),
        });
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        var ilitoolsOptionsMock = new Mock<IOptions<IlitoolsOptions>>();
        ilitoolsOptionsMock.SetupGet(o => o.Value).Returns(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" });
        return new PipelineProcessFactory(options.Object, ilitoolsOptionsMock.Object, loggerFactory.Object);
    }

    private static StepConfig ZipStep(InputConfig input) => new()
    {
        Id = "zip",
        DisplayName = new LocalizedText(new Dictionary<string, string> { ["en"] = "Zip" }),
        ProcessId = "zip_package_process",
        Input = input,
    };

    private static List<ProcessConfig> ZipProcesses() => new()
    {
        new ProcessConfig { Id = "zip_package_process", Implementation = "Geopilot.Pipeline.Processes.ZipPackage.ZipPackageProcess" },
    };

    private static StepConfig MatchStep() => new()
    {
        Id = "match",
        DisplayName = new LocalizedText(new Dictionary<string, string> { ["en"] = "Match" }),
        ProcessId = "file_matcher",
    };

    private static List<ProcessConfig> MatchAndZipProcesses() => new()
    {
        new ProcessConfig { Id = "file_matcher", Implementation = "Geopilot.Pipeline.Processes.Matcher.FileMatcher.FileMatcherProcess" },
        new ProcessConfig { Id = "zip_package_process", Implementation = "Geopilot.Pipeline.Processes.ZipPackage.ZipPackageProcess" },
    };

    private static StepConfig WithPreFailCondition(StepConfig step, string expression)
    {
        step.Conditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig
            {
                FailConditions = new List<ConditionConfig>
                {
                    new ConditionConfig { Expression = expression },
                },
            },
        };
        return step;
    }

    private static StepConfig WithPostFailCondition(StepConfig step, string expression)
    {
        step.Conditions = new PipelineStepConditionsConfig
        {
            Post = new PipelineStepPostConditionConfig
            {
                FailConditions = new List<ConditionConfig>
                {
                    new ConditionConfig { Expression = expression },
                },
            },
        };
        return step;
    }

    private static StepConfig ValidationStep() => new()
    {
        Id = "validate",
        DisplayName = new LocalizedText(new Dictionary<string, string> { ["en"] = "Validate" }),
        ProcessId = "xtf_validator",
    };

    private static List<ProcessConfig> ValidationProcesses(Parameterization defaultConfig) => new()
    {
        new ProcessConfig { Id = "xtf_validator", Implementation = "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess", DefaultConfig = defaultConfig },
    };

    private static StepConfig VisualizationStep() => new()
    {
        Id = "visualize",
        DisplayName = new LocalizedText(new Dictionary<string, string> { ["en"] = "Visualize" }),
        ProcessId = "xtf_error_visualizer",
    };

    private static List<ProcessConfig> VisualizationProcesses(Parameterization defaultConfig) => new()
    {
        new ProcessConfig { Id = "xtf_error_visualizer", Implementation = "Geopilot.Pipeline.Processes.XtfErrorVisualization.XtfErrorVisualizationProcess", DefaultConfig = defaultConfig },
    };
}
