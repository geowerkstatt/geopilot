namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Output action for handling output data in a pipeline step.
/// </summary>
public enum OutputAction
{
    /// <summary>
    /// Ignore the output data.
    /// </summary>
    Ignore,

    /// <summary>
    /// Download the output data.
    /// </summary>
    Download,
}
