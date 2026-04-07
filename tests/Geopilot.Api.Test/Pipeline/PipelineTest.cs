using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineTest
{
    private Mock<ILoggerFactory> loggerFactory;
    private Mock<ILogger<Api.Pipeline.Pipeline>> loggerMock;

    [TestInitialize]
    public void SetUp()
    {
        loggerFactory = new Mock<ILoggerFactory>();
        loggerMock = new Mock<ILogger<Api.Pipeline.Pipeline>>();
        loggerFactory
            .Setup(lf => lf.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
    }

    [TestMethod(DisplayName = "Pipeline State Test")]
    [DataRow(PipelineState.Pending, new[] { StepState.Pending, StepState.Pending }, DisplayName = "all steps pending")]
    [DataRow(PipelineState.Running, new[] { StepState.Running, StepState.Pending }, DisplayName = "steps pending and running")]
    [DataRow(PipelineState.Running, new[] { StepState.Skipped, StepState.Running, StepState.Pending }, DisplayName = "steps pending, running and skipped")]
    [DataRow(PipelineState.Pending, new StepState[0], DisplayName = "no steps")]
    [DataRow(PipelineState.Failed, new[] { StepState.Success, StepState.Error, StepState.Pending }, DisplayName = "failed steps")]
    [DataRow(PipelineState.Running, new[] { StepState.Success, StepState.Running }, DisplayName = "running steps")]
    [DataRow(PipelineState.Running, new[] { StepState.Success, StepState.Pending }, DisplayName = "success and running steps (edge case)")]
    [DataRow(PipelineState.Success, new[] { StepState.Success, StepState.Success }, DisplayName = "all steps success")]
    [DataRow(PipelineState.Success, new[] { StepState.Success, StepState.Skipped, StepState.Success }, DisplayName = "all steps success or skipped")]
    public void PipelineStateTest(PipelineState expectedState, IEnumerable<StepState> stepStates)
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };
        var inputConfigs = new List<InputConfig>();
        var outputConfigs = new List<OutputConfig>();

        var steps = stepStates
            .Select(s =>
            {
                var step = new Mock<IPipelineStep>();
                step.SetupProperty(s => s.State, s);
                return step.Object;
            })
            .ToList();

        using var pipeline = Api.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(pipelineDisplayName)
            .Steps(steps)
            .UploadFiles(new PipelineFileList(new List<IPipelineFile> { Mock.Of<IPipelineFile>() }))
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .JobId(Guid.NewGuid())
            .Build();

        Assert.AreEqual(expectedState, pipeline.State, "pipeline state not as expected");
    }

    [TestMethod]
    public void InteruptPipelineIfAStepFails()
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };
        var inputConfigs = new List<InputConfig>();
        var outputConfigs = new List<OutputConfig>();

        var firstStep = new Mock<IPipelineStep>();
        firstStep.SetupSequence(s => s.State)
            .Returns(StepState.Pending)
            .Returns(StepState.Error);

        var secondStep = new Mock<IPipelineStep>();
        secondStep.SetupProperty(s => s.State, StepState.Pending);

        var steps = new List<IPipelineStep> { firstStep.Object, secondStep.Object };

        var uploadFile = new PipelineFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");

        using var pipeline = Api.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(pipelineDisplayName)
            .Steps(steps)
            .UploadFiles(new PipelineFileList(new List<IPipelineFile> { uploadFile }))
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .JobId(Guid.NewGuid())
            .Build();

        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery, "pipeline delivery should be allowed before running the pipeline");

        var context = pipeline.Run(CancellationToken.None);

        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery, "pipeline delivery should be allowed after running the pipeline");

        firstStep.Verify(
            p => p.Run(It.Is<PipelineContext>(pc => pc.StepResults.Count == 0), It.IsAny<CancellationToken>()),
            Times.Once());

        secondStep.Verify(
            p => p.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [TestMethod]
    public void PreventPipelineDeliveryIfConditionFails()
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };
        var inputConfigs = new List<InputConfig>();
        var outputConfigs = new List<OutputConfig>();

        var step = new Mock<IPipelineStep>();

        step.SetupProperty(s => s.State, StepState.Pending);
        step.SetupGet(s => s.Id).Returns("step_id");
        StepResult stepResult = new StepResult()
        {
            Outputs = new Dictionary<string, StepOutput>()
            {
                { "output1", new StepOutput() { Data = "my_step_data", Action = new HashSet<OutputAction>(), } },
            },
        };
        step.Setup(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(stepResult));

        var steps = new List<IPipelineStep> { step.Object };

        var uploadFile = new PipelineFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");

        string deliveryCondition = "[step_id.output1] != 'my_step_data'";

        using var pipeline = Api.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(pipelineDisplayName)
            .Steps(steps)
            .JobId(Guid.NewGuid())
            .DeliveryCondition(deliveryCondition)
            .UploadFiles(new PipelineFileList(new List<IPipelineFile> { uploadFile }))
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .Build();

        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery, "pipeline delivery should be allowed before running the pipeline");

        var context = pipeline.Run(CancellationToken.None);

        Assert.AreEqual(PipelineDelivery.Prevent, pipeline.Delivery, "pipeline delivery should be prevented after running the pipeline");
    }
}
