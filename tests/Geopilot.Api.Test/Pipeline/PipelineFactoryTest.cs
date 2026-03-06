using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineFactoryTest
{
    private static string interlisCheckServiceBaseUrl = "http://localhost:3080/";
    private Mock<IOptions<PipelineOptions>> pipelineOptionsMock;
    private PipelineProcessFactory pipelineProcessFactory;

    [TestInitialize]
    public void SetUp()
    {
        var pipelineOptions = new PipelineOptions()
        {
            Definition = "",
            ProcessConfigs = new Dictionary<string, Parameterization>()
            {
                {
                    "Geopilot.Api.Pipeline.Process.XtfValidatorProcess", new Parameterization()
                    {
                        { "checkServiceBaseUrl", interlisCheckServiceBaseUrl },
                    }
                },
            },
        };

        pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        var loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
        this.pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, loggerMock.Object);
    }

    [TestMethod(DisplayName = "Create Pipeline By Id But Pipeline Not Defined")]
    public void CreatePipelineByIdButPipelineNotDefined()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreatePipeline("foo", Mock.Of<IPipelineTransferFile>()));
        Assert.AreEqual("pipeline for 'foo' not found", exception.Message);
    }

    [TestMethod(DisplayName = "Create Basic Pipeline")]
    public void CreateBasicPipeline()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipeline_01");
        using var pipeline = factory.CreatePipeline("ili_validation", Mock.Of<IPipelineTransferFile>());
        Assert.AreEqual(PipelineState.Pending, pipeline.State, "pipeline state not as expected");
        Assert.AreEqual(StepState.Pending, pipeline.Steps[0].State, "step state not as expected");
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.AreEqual("ili_validation", pipeline.Id, "pipeline name not as expected");
        Assert.IsNotNull(pipeline.Parameters, "pipeline parameters not initialized");
        Assert.AreEqual("upload", pipeline.Parameters.UploadStep, "upload step not as expected");
        Assert.HasCount(1, pipeline.Parameters.Mappings);
        var mapping_0 = pipeline.Parameters.Mappings.ElementAt(0);
        Assert.AreEqual("xtf", mapping_0.FileExtension, "pipeline mapping 0 file extension not as expected");
        Assert.AreEqual("ili_file", mapping_0.Attribute, "pipeline mapping 0 attribute not as expected");
        Assert.HasCount(1, pipeline.Steps);
        var validationStep = pipeline.Steps[0];
        Assert.AreEqual("validation", validationStep.Id, "validation step name not as expected");
        Assert.HasCount(1, validationStep.InputConfig);
        var inputConfig_0 = validationStep.InputConfig.ElementAt(0);
        InputConfig expectedInputConfig_0 = new InputConfig()
        {
            From = "upload",
            Take = "ili_file",
            As = "iliFile",
        };
        AssertInputConfig(expectedInputConfig_0, inputConfig_0);
        Assert.HasCount(2, validationStep.OutputConfigs);
        var outputConfig_0 = validationStep.OutputConfigs.ElementAt(0);
        var outputConfig_1 = validationStep.OutputConfigs.ElementAt(1);
        OutputConfig expectedOutputConfig_0 = new OutputConfig()
        {
            Take = "error_log",
            As = "error_log",
        };
        OutputConfig expectedOutputConfig_1 = new OutputConfig()
        {
            Take = "xtf_log",
            As = "xtf_log",
            Action = new HashSet<OutputAction>() { OutputAction.Download },
        };
        AssertOutputConfig(expectedOutputConfig_0, outputConfig_0);
        AssertOutputConfig(expectedOutputConfig_1, outputConfig_1);
        object stepProcess = validationStep.Process;
        Assert.IsNotNull(stepProcess, "step process not created");
        var configuratedValidationProfile = typeof(XtfValidatorProcess)
            ?.GetField("validationProfile", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(stepProcess) as string;
        var configuratedHttpClient = typeof(XtfValidatorProcess)
            ?.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(stepProcess) as HttpClient;
        var configuratedPollInterval = typeof(XtfValidatorProcess)
            ?.GetField("pollInterval", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(stepProcess);
        Assert.AreEqual("PROFILE-A", configuratedValidationProfile, "configurated validation profile not as expected");
        Assert.AreEqual("http://localhost:3080/", configuratedHttpClient?.BaseAddress?.ToString(), "configurated HTTP client base address not as expected");
        Assert.AreEqual(TimeSpan.FromSeconds(2), configuratedPollInterval, "configurated poll interval not as expected");
        Assert.IsNotNull(stepProcess as XtfValidatorProcess, "process is not of type ILI Validator");
    }

    [TestMethod(DisplayName = "Create Basic Pipeline No Process Config Overwrite")]
    public void CreateBasicPipelineNoProcessConfigOverwrite()
    {
        PipelineFactory factory = CreatePipelineFactory("basicPipelineNoProcessConfigOverwrite");
        using var pipeline = factory.CreatePipeline("ili_validation", Mock.Of<IPipelineTransferFile>());
        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(1, pipeline.Steps);
        var validationStep = pipeline.Steps[0];
        object stepProcess = validationStep.Process;
        Assert.IsNotNull(stepProcess, "step process not created");
        var configuratedValidationProfile = typeof(XtfValidatorProcess)
            ?.GetField("validationProfile", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(stepProcess) as string;
        var configuratedHttpClient = typeof(XtfValidatorProcess)
            ?.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(stepProcess) as HttpClient;
        var configuratedPollInterval = typeof(XtfValidatorProcess)
            ?.GetField("pollInterval", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(stepProcess);
        Assert.AreEqual("", configuratedValidationProfile, "configurated validation profile not as expected");
        Assert.AreEqual("http://localhost:3080/", configuratedHttpClient?.BaseAddress?.ToString(), "configurated HTTP client base address not as expected");
        Assert.AreEqual(TimeSpan.FromSeconds(2), configuratedPollInterval, "configurated poll interval not as expected");
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");

        return PipelineFactory
            .Builder()
            .File(path)
            .PipelineProcessFactory(this.pipelineProcessFactory)
            .Build();
    }

    private static void AssertOutputConfig(OutputConfig expectedConfig, OutputConfig actualConfig)
    {
        if (expectedConfig != null && actualConfig != null)
        {
            Assert.AreEqual(expectedConfig.Take, actualConfig.Take, "Output config 'Take' not as expected");
            Assert.AreEqual(expectedConfig.As, actualConfig.As, "Output config 'As' not as expected");
            if (expectedConfig.Action != null)
                CollectionAssert.AreEquivalent(expectedConfig.Action.ToArray(), actualConfig.Action.ToArray(), "Output config 'Action' not as expected");
            else
                Assert.IsNull(actualConfig.Action, "Output config 'Action' not as expected");
        }
        else if (expectedConfig != null && actualConfig == null)
        {
            Assert.Fail("Expected OutputConfig is defined bug actual OutputConfig is not defined");
        }
        else if (expectedConfig == null && actualConfig != null)
        {
            Assert.Fail("Expected OutputConfig is not defined bug actual OutputConfig is defined");
        }
    }

    private static void AssertInputConfig(InputConfig expectedConfig, InputConfig actualConfig)
    {
        if (expectedConfig != null && actualConfig != null)
        {
            Assert.AreEqual(expectedConfig.From, actualConfig.From, "Output config 'From' not as expected");
            Assert.AreEqual(expectedConfig.Take, actualConfig.Take, "Output config 'Take' not as expected");
            Assert.AreEqual(expectedConfig.As, actualConfig.As, "Output config 'As' not as expected");
        }
        else if (expectedConfig != null && actualConfig == null)
        {
            Assert.Fail("Expected InputConfig is defined but actual InputConfig is not defined");
        }
        else if (expectedConfig == null && actualConfig != null)
        {
            Assert.Fail("Expected InputConfig is not defined but actual InputConfig is defined");
        }
    }
}
