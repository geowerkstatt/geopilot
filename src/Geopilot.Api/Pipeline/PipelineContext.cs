namespace Geopilot.Api.Pipeline;

/// <summary>
/// Context for a pipeline execution, containing the results of each step.
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// The results of each step in the pipeline.
    /// </summary>
    public required Dictionary<string, StepResult> StepResults { get; set; }
}
