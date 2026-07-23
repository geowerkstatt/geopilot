using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Process;
using Geopilot.Pipeline.Processes.Matcher.XtfMatcher;
using Geopilot.Pipeline.Processes.XtfValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineFactoryTest
{
    private static string interlisCheckServiceBaseUrl = "http://localhost:3080/";
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
                        { "checkServiceBaseUrl", interlisCheckServiceBaseUrl },
                    }
                },
            },
        };

        pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        var loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
        loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        this.pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, loggerFactory.Object);
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
        Assert.AreEqual(new InputValue.StepOutputReference("xtf_matching", "XtfFiles"), validationInputs["iliFile"]);
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
        var configuratedValidationProfile = typeof(XtfValidatorProcess)
            ?.GetField("validationProfile", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(validationProcess) as string;
        var configuratedHttpClient = typeof(XtfValidatorProcess)
            ?.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(validationProcess) as HttpClient;
        var configuratedPollInterval = typeof(XtfValidatorProcess)
            ?.GetField("pollInterval", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(validationProcess);
        Assert.AreEqual("PROFILE-A", configuratedValidationProfile, "configurated validation profile not as expected");
        Assert.AreEqual("http://localhost:3080/", configuratedHttpClient?.BaseAddress?.ToString(), "configurated HTTP client base address not as expected");
        Assert.AreEqual(TimeSpan.FromSeconds(2), configuratedPollInterval, "configurated poll interval not as expected");
        Assert.IsNotNull(validationProcess as XtfValidatorProcess, "process is not of type ILI Validator");
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
