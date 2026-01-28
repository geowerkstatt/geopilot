namespace Geopilot.Api.Pipeline;

internal class PipelineContext
{
    public required Dictionary<string, StepResult> StepResults { get; set; }
}
