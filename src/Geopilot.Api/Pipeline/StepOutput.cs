using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents the output of a single step in a pipeline.
/// </summary>
public class StepOutput
{
    /// <summary>
    /// The action to perform with the output data.
    /// </summary>
    public required OutputAction Action { get; set; }

    /// <summary>
    /// The data produced by this step.
    /// </summary>
    public required object Data { get; set; }
}
