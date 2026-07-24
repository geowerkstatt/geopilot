using Geopilot.Pipeline.Config;
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

        return new PipelineProcessFactory(options.Object, loggerFactory.Object);
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
}
