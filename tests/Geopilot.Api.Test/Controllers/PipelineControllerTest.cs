using Geopilot.Api.Contracts;
using Geopilot.Api.Controllers;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Controllers;

[TestClass]
public sealed class PipelineControllerTest
{
    private Mock<ILogger<PipelineController>> loggerMock;
    private Mock<IPipelineService> pipelineServiceMock;
    private PipelineController controller;

    [TestInitialize]
    public void Initialize()
    {
        this.loggerMock = new Mock<ILogger<PipelineController>>();
        this.pipelineServiceMock = new Mock<IPipelineService>();

        controller = new PipelineController(loggerMock.Object, pipelineServiceMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        loggerMock.VerifyAll();
        pipelineServiceMock.VerifyAll();
    }

    [TestMethod]
    public async Task GetAvailablePipelines()
    {
        var pipeline01 = new PipelineConfig()
        {
            Id = "Pipeline1",
            DisplayName = new Dictionary<string, string>()
            {
                { "en", "pipeline 1" },
                { "de", "Pipeline 1" },
            },
            Parameters = new PipelineParametersConfig() { UploadStep = "", Mappings = new List<FileMappingsConfig>(), },
            Steps = new List<StepConfig>(),
        };
        var pipeline02 = new PipelineConfig()
        {
            Id = "Pipeline2",
            DisplayName = new Dictionary<string, string>()
            {
                { "en", "pipeline 2" },
                { "de", "Pipeline 2" },
            },
            Parameters = new PipelineParametersConfig() { UploadStep = "", Mappings = new List<FileMappingsConfig>(), },
            Steps = new List<StepConfig>(),
        };
        pipelineServiceMock.Setup(s => s.GetAvailablePipelines())
            .Returns(new List<PipelineConfig> { pipeline01, pipeline02 });

        var response = await controller.GetAvailablePipelines() as OkObjectResult;
        Assert.IsInstanceOfType<OkObjectResult>(response);
        var availablePipelines = response?.Value as AvailablePipelinesResponse;
        Assert.IsNotNull(availablePipelines);
        Assert.IsInstanceOfType<AvailablePipelinesResponse>(availablePipelines);

        var availablePipeline1 = availablePipelines.Pipelines.ElementAt(0);
        var availablePipeline2 = availablePipelines.Pipelines.ElementAt(1);

        Assert.AreEqual("Pipeline1", availablePipeline1.Id, "pipeline 1 ID not as expected");
        CollectionAssert.AreEqual(new Dictionary<string, string>() { { "en", "pipeline 1" }, { "de", "Pipeline 1" }, }, availablePipeline1.DisplayName, "pipeline 1 Display Name not as expected");

        Assert.AreEqual("Pipeline2", availablePipeline2.Id, "pipeline 2 ID not as expected");
        CollectionAssert.AreEqual(new Dictionary<string, string>() { { "en", "pipeline 2" }, { "de", "Pipeline 2" }, }, availablePipeline2.DisplayName, "pipeline 2 Display Name not as expected");
    }
}
