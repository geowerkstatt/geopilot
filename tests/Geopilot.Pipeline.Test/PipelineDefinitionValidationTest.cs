using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineDefinitionValidationTest
{
    private PipelineProcessFactory pipelineProcessFactory;
    private Mock<ILoggerFactory> loggerFactoryMock;

    [TestInitialize]
    public void SetUp()
    {
        var pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(new PipelineOptions
        {
            Definition = "myPipeline.yaml",
            Plugins = new List<string>(),
            ProcessConfigs = new Dictionary<string, Parameterization>(),
        });

        var loggerMock = new Mock<ILogger>();
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        var ilitoolsOptionsMock = new Mock<IOptions<IlitoolsOptions>>();
        ilitoolsOptionsMock.SetupGet(o => o.Value).Returns(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" });

        pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, ilitoolsOptionsMock.Object, loggerFactoryMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        pipelineProcessFactory?.Dispose();
    }

    [TestMethod]
    public void ValidateDefinitionAcceptsValidDefinition()
    {
        var result = CreatePipelineFactory("basicPipeline_01").ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateDefinitionRejectsInvalidDefinition()
    {
        var result = CreatePipelineFactory("pipelineNotUnique").ValidateDefinition();

        Assert.IsFalse(result.IsValid);
        var message = result.ErrorMessage;
        Assert.IsNotNull(message);
        Assert.IsTrue(message.StartsWith("errors in pipeline definition:", StringComparison.Ordinal), message);
        StringAssert.Contains(message, "Duplicate Id found: ili_validation.");
    }

    // The definition check returns before any step is inspected, so an operator fixing a broken definition is not
    // buried in follow-up errors from steps that could not be checked yet. Pinned here so the two phases are not
    // merged by accident.
    [TestMethod]
    public void ValidateDefinitionReportsDefinitionErrorsWithoutProcessErrors()
    {
        var result = CreatePipelineFactory("definitionAndProcessErrors").ValidateDefinition();

        Assert.IsFalse(result.IsValid);
        var message = result.ErrorMessage;
        Assert.IsNotNull(message);
        Assert.IsTrue(message.StartsWith("errors in pipeline definition:", StringComparison.Ordinal), message);
        Assert.IsFalse(
            message.Contains("Invalid pipeline processes found:", StringComparison.Ordinal),
            "process errors must not be reported alongside definition errors");
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        var definitionPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Pipeline", filename + ".yaml");
        var pipelineDirectory = Path.Combine(Path.GetTempPath(), "Pipeline");

        return PipelineFactory
            .Builder()
            .File(definitionPath)
            .PipelineProcessFactory(pipelineProcessFactory)
            .LoggerFactory(loggerFactoryMock.Object)
            .PipelineTempDirectory(pipelineDirectory)
            .Build();
    }
}
