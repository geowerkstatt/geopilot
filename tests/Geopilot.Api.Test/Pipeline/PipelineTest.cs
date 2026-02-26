using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Moq;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineTest
{
    [TestMethod(DisplayName = "Pipeline State Test")]
    [DataRow(PipelineState.Pending, new[] { StepState.Pending, StepState.Pending })]
    [DataRow(PipelineState.Running, new[] { StepState.Running, StepState.Pending })]
    [DataRow(PipelineState.Pending, new StepState[0])]
    [DataRow(PipelineState.Failed, new[] { StepState.Success, StepState.Failed, StepState.Pending })]
    [DataRow(PipelineState.Running, new[] { StepState.Success, StepState.Running })]
    [DataRow(PipelineState.Running, new[] { StepState.Success, StepState.Pending })]
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

        var pipelineParameters = new PipelineParametersConfig() { UploadStep = "upload", Mappings = new List<FileMappingsConfig>() };

        using var pipeline = new Api.Pipeline.Pipeline("test_pipeline", pipelineDisplayName, steps, pipelineParameters, Mock.Of<IPipelineTransferFile>());

        Assert.AreEqual(expectedState, pipeline.State);
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
            .Returns(StepState.Failed);

        var secondStep = new Mock<IPipelineStep>();
        secondStep.SetupProperty(s => s.State, StepState.Pending);

        var steps = new List<IPipelineStep> { firstStep.Object, secondStep.Object };

        var pipelineParameters = new PipelineParametersConfig() { UploadStep = "upload", Mappings = new List<FileMappingsConfig>() };

        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");

        using var pipeline = new Api.Pipeline.Pipeline("test_pipeline", pipelineDisplayName, steps, pipelineParameters, uploadFile);

        var context = pipeline.Run(CancellationToken.None);

        firstStep.Verify(
            p => p.Run(It.Is<PipelineContext>(pc => pc.StepResults.Count == 1 && pc.StepResults.ContainsKey("upload")), It.IsAny<CancellationToken>()),
            Times.Once());

        secondStep.Verify(
            p => p.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    private FileHandle CreateTestFileHandle(string file)
    {
        var stream = File.Open(file, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        return new FileHandle(file, stream);
    }
}
