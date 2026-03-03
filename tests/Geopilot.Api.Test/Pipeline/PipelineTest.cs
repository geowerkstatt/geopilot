using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Moq;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineTest
{
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

        var pipelineParameters = new PipelineParametersConfig() { UploadStep = "upload", Mappings = new List<FileMappingsConfig>() };

        using var pipeline = new Api.Pipeline.Pipeline("test_pipeline", pipelineDisplayName, steps, pipelineParameters, Mock.Of<IPipelineTransferFile>());

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
