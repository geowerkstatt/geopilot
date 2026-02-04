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

        var pipeline = new Api.Pipeline.Pipeline("test_pipeline", pipelineDisplayName, steps, pipelineParameters);

        Assert.AreEqual(expectedState, pipeline.State);
    }
}
