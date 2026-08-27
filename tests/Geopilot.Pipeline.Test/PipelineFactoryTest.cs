using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Geopilot.Pipeline.Processes.Matcher.XtfMatcher;
using Geopilot.Pipeline.Processes.XtfValidation;
using Geopilot.PipelineCore.Ilitools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using System.Text.Json;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineFactoryTest
{
    private static string modelDir = "https://models.example.com/";
    private Mock<IOptions<PipelineOptions>> pipelineOptionsMock;
    private PipelineProcessFactory pipelineProcessFactory;
    private Mock<ILoggerFactory> loggerFactory;

    [TestInitialize]
    public void SetUp()
    {
        var pipelineOptions = new PipelineOptions()
        {
            Definition = "",
            ProcessConfigs = new Dictionary<string, Parameterization>()
            {
                {
                    "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess", new Parameterization()
                    {
                        { "modelDirs", modelDir },
                    }
                },
            },
        };

        pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        var loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
        loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        var ilitoolsOptionsMock = new Mock<IOptions<IlitoolsOptions>>();
        ilitoolsOptionsMock.SetupGet(o => o.Value).Returns(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" });
        this.pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, ilitoolsOptionsMock.Object, loggerFactory.Object);
    }

    [TestMethod(DisplayName = "Create Pipeline By Id But Pipeline Not Defined")]
    public void CreatePipelineByIdButPipelineNotDefined()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("foo", Guid.NewGuid()));
        Assert.AreEqual("pipeline for 'foo' not found", exception.Message);
    }

    [TestMethod(DisplayName = "Create Basic Pipeline")]
    public void CreateBasicPipeline()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        using var pipeline = factory.CreatePipeline("ili_validation", Guid.NewGuid());
        Assert.AreEqual(ProcessingState.Pending, pipeline.State, "pipeline state not as expected");
        Assert.AreEqual(StepState.Pending, pipeline.Steps[0].State, "step state not as expected");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.AreEqual("ili_validation", pipeline.Id, "pipeline name not as expected");
        Assert.HasCount(2, pipeline.Steps);
        var matcherStep = pipeline.Steps[0];
        Assert.AreEqual("xtf_matching", matcherStep.Id, "matcher step name not as expected");
        var matcherInputs = ((PipelineStep)matcherStep).Inputs;
        Assert.HasCount(1, matcherInputs);
        Assert.AreEqual(new InputValue.UploadReference(), matcherInputs["files"]);
        Assert.HasCount(0, matcherStep.OutputActions);
        object matcherProcess = matcherStep.Process;
        Assert.IsNotNull(matcherProcess, "matcher step process not created");
        var configuratedFileExtensions = typeof(XtfMatcherProcess)
            ?.GetField("fileExtensions", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(matcherProcess) as HashSet<string>;
        var configuratedIliModels = typeof(XtfMatcherProcess)
            ?.GetField("iliModels", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(matcherProcess) as HashSet<string>;
        var configuratedFileNamePatterns = typeof(XtfMatcherProcess)
            ?.GetField("fileNamePatterns", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(matcherProcess) as HashSet<string>;
        Assert.IsNotNull(configuratedFileExtensions);
        Assert.IsTrue(
            configuratedFileExtensions.SetEquals(new HashSet<string> { "xtf" }),
            "configurated file extensions not as expected");

        Assert.IsNotNull(configuratedIliModels);
        Assert.IsTrue(
            configuratedIliModels.SetEquals(new HashSet<string> { "RoadsExdm2ien" }),
            "configurated ILI models not as expected");

        Assert.IsNotNull(configuratedFileNamePatterns);
        Assert.IsTrue(
            configuratedFileNamePatterns.SetEquals(new HashSet<string> { ".*" }),
            "configurated file name patterns not as expected");

        var validationStep = pipeline.Steps[1];
        Assert.AreEqual("validation", validationStep.Id, "validation step name not as expected");
        var validationInputs = ((PipelineStep)validationStep).Inputs;
        Assert.HasCount(1, validationInputs);
        Assert.AreEqual(new InputValue.StepOutputReference("xtf_matching", "XtfFiles"), validationInputs["transferFile"]);
        Assert.HasCount(1, validationStep.OutputActions);
        var validationOutputAction_0 = validationStep.OutputActions.ElementAt(0);
        OutputActionConfig validationExpectedOutputAction_0 = new OutputActionConfig()
        {
            Property = "XtfLog",
            Actions = new HashSet<OutputAction>() { OutputAction.Download },
        };
        AssertOutputAction(validationExpectedOutputAction_0, validationOutputAction_0);
        object validationProcess = validationStep.Process;
        Assert.IsNotNull(validationProcess, "validation step process not created");
        var configuratedArgs = typeof(XtfValidatorProcess)
            ?.GetField("validatorArgs", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(validationProcess) as IlivalidatorArgs;
        Assert.IsNotNull(configuratedArgs, "validator arguments not built");
        Assert.AreEqual("ilidata:PROFILE-A", configuratedArgs.MetaConfig, "configurated validation profile not as expected");
        Assert.AreEqual(modelDir, configuratedArgs.ModelDirs?.Single(), "configurated model directory not as expected");
        Assert.IsNotNull(validationProcess as XtfValidatorProcess, "process is not of type ILI Validator");
    }

    [TestMethod(DisplayName = "Condition id is optional and parsed when present")]
    public void ConditionIdIsOptionalAndParsedWhenPresent()
    {
        var yaml = """
            processes:
              - id: xtf_matcher
                implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
                default_config:
                  fileExtensions:
                    - xtf
                  iliModels:
                    - RoadsExdm2ien
                  fileNamePatterns:
                    - .*
            pipelines:
              - id: test_pipeline
                display_name:
                  en: Test
                steps:
                  - id: first
                    display_name:
                      en: First
                    process_id: xtf_matcher
                    conditions:
                      post:
                        warn_conditions:
                          # Same id as on the second step: reuse across steps marks the same rule and stays valid.
                          - id: with-id
                            expression: "Length([first.XtfFiles]) == 0"
                    input:
                      files: "${upload()}"
                  - id: second
                    display_name:
                      en: Second
                    process_id: xtf_matcher
                    conditions:
                      pre:
                        skip_conditions:
                          - id: with-id
                            expression: "Length([first.XtfFiles]) == 0"
                          - expression: "Length([first.XtfFiles]) == 1"
                    input:
                      files: "${upload()}"
            """;

        var factory = PipelineFactory
            .Builder()
            .Yaml(yaml)
            .PipelineProcessFactory(this.pipelineProcessFactory)
            .LoggerFactory(this.loggerFactory.Object)
            .PipelineTempDirectory(Path.Combine(Path.GetTempPath(), "Pipeline"))
            .Build();

        var validationResult = factory.ValidateDefinition();
        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);

        var conditions = factory.Pipelines.Single().Steps[1].Conditions!.Pre!.SkipConditions!;
        Assert.AreEqual("with-id", conditions[0].Id);
        Assert.IsNull(conditions[1].Id, "a condition without id must stay valid.");
    }

    [TestMethod(DisplayName = "Definition snapshot is valid JSON restricted to the pipeline")]
    public void GetDefinitionSnapshotJsonIsTrimmedAndParseable()
    {
        var yaml = """
            processes:
              - id: xtf_matcher
                implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
                default_config:
                  fileExtensions:
                    - xtf
              - id: file_matcher
                implementation: Geopilot.Pipeline.Processes.Matcher.FileMatcher.FileMatcherProcess
                default_config:
                  fileExtensions:
                    - ili
            pipelines:
              - id: pipeline_a
                display_name:
                  de: Prüfung süss
                  fr: Validation référencée
                steps:
                  - id: first
                    display_name:
                      de: Zuordnung
                    process_id: xtf_matcher
                    input:
                      files: "${upload()}"
                    conditions:
                      post:
                        warn_conditions:
                          - id: nothing-matched
                            expression: "Length([first.XtfFiles]) == 0"
                    output_actions:
                      - property: XtfFiles
                        actions:
                          - Delivery
                  - id: second
                    display_name:
                      de: Zweiter
                    process_id: xtf_matcher
                    input:
                      files: "${step_output(first.XtfFiles)}"
              - id: pipeline_b
                display_name:
                  de: Andere
                steps:
                  - id: only
                    display_name:
                      de: Einziger
                    process_id: file_matcher
                    input:
                      files: "${upload()}"
            """;

        var processBaseConfigs = new Dictionary<string, Parameterization>
        {
            ["Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess"] = new Parameterization { { "fileNamePatterns", ".*" } },
            ["Geopilot.Pipeline.Processes.Matcher.FileMatcher.FileMatcherProcess"] = new Parameterization { { "fileNamePatterns", ".*" } },
        };

        var factory = PipelineFactory
            .Builder()
            .Yaml(yaml)
            .PipelineProcessFactory(this.pipelineProcessFactory)
            .LoggerFactory(this.loggerFactory.Object)
            .PipelineTempDirectory(Path.Combine(Path.GetTempPath(), "Pipeline"))
            .ProcessBaseConfigs(processBaseConfigs)
            .Build();

        var json = factory.GetDefinitionSnapshotJson("pipeline_a");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var pipelines = root.GetProperty("pipelines");
        Assert.AreEqual(1, pipelines.GetArrayLength(), "only the snapshotted pipeline belongs into the document.");
        Assert.AreEqual("pipeline_a", pipelines[0].GetProperty("id").GetString());
        Assert.AreEqual("Prüfung süss", pipelines[0].GetProperty("display_name").GetProperty("de").GetString());
        Assert.AreEqual("Validation référencée", pipelines[0].GetProperty("display_name").GetProperty("fr").GetString());

        var steps = pipelines[0].GetProperty("steps");
        Assert.AreEqual(2, steps.GetArrayLength());
        Assert.AreEqual("first", steps[0].GetProperty("id").GetString(), "step order is semantic and must survive.");
        Assert.AreEqual("second", steps[1].GetProperty("id").GetString());
        Assert.AreEqual(
            "nothing-matched",
            steps[0].GetProperty("conditions").GetProperty("post").GetProperty("warn_conditions")[0].GetProperty("id").GetString());

        var processes = root.GetProperty("processes");
        Assert.AreEqual(1, processes.GetArrayLength(), "only processes referenced by the pipeline's steps belong into the document.");
        Assert.AreEqual("xtf_matcher", processes[0].GetProperty("id").GetString());

        var processConfigs = root.GetProperty("process_configs");
        Assert.IsTrue(processConfigs.TryGetProperty("Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess", out _));
        Assert.IsFalse(
            processConfigs.TryGetProperty("Geopilot.Pipeline.Processes.Matcher.FileMatcher.FileMatcherProcess", out _),
            "base configuration of an unreferenced implementation must be trimmed.");
    }

    [TestMethod(DisplayName = "Every pipeline of the sample definitions snapshots to parseable JSON")]
    [DataRow("basicPipeline_01")]
    [DataRow("twoStepPipeline_01")]
    [DataRow("uploadReferencePipeline")]
    [DataRow("fileReferencePipeline")]
    public void GetDefinitionSnapshotJsonParsesForSampleDefinitions(string filename)
    {
        var factory = CreatePipelineFactory(filename);

        foreach (var pipeline in factory.Pipelines)
        {
            var json = factory.GetDefinitionSnapshotJson(pipeline.Id);
            using var document = JsonDocument.Parse(json);
            Assert.AreEqual(pipeline.Id, document.RootElement.GetProperty("pipelines")[0].GetProperty("id").GetString());
        }
    }

    [TestMethod(DisplayName = "Definition snapshot of an unknown pipeline throws")]
    public void GetDefinitionSnapshotJsonUnknownPipelineThrows()
    {
        var factory = CreatePipelineFactory("basicPipeline_01");
        var exception = Assert.Throws<InvalidOperationException>(() => factory.GetDefinitionSnapshotJson("foo"));
        Assert.AreEqual("pipeline for 'foo' not found", exception.Message);
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        string pipelineDirectory = Path.Combine(Path.GetTempPath(), "Pipeline");

        return PipelineFactory
            .Builder()
            .File(path)
            .PipelineProcessFactory(this.pipelineProcessFactory)
            .LoggerFactory(this.loggerFactory.Object)
            .PipelineTempDirectory(pipelineDirectory)
            .Build();
    }

    private static void AssertOutputAction(OutputActionConfig expected, OutputActionConfig actual)
    {
        Assert.IsNotNull(actual, "Output action not defined");
        Assert.AreEqual(expected.Property, actual.Property, "Output action 'Property' not as expected");
        Assert.IsTrue(
            actual.Actions.SetEquals(expected.Actions),
            "Output action 'Actions' not as expected");
    }
}
