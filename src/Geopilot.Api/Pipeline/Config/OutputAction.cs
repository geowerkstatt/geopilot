namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Output action for handling output data in a pipeline step.
/// </summary>
public enum OutputAction
{
    /// <summary>
    /// Download the output data.
    /// </summary>
    Download,

    /// <summary>
    /// Deliver the output data.
    /// </summary>
    Delivery,
}
