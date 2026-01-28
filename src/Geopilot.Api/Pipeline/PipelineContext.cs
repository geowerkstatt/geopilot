namespace Geopilot.Api.Pipeline;

internal class PipelineContext
{
    public required PipelineState State { get; set; }

    public required Dictionary<string, StepResult> StepResults { get; set; }
}
