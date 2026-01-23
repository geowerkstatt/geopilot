namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Output action for handling output data in a pipeline step.
/// </summary>
internal enum OutputAction
{
    /// <summary>
    /// Ignore the output data.
    /// </summary>
    IGNORE,

    /// <summary>
    /// Download the output data.
    /// </summary>
    DOWNLOAD,

    /// <summary>
    /// Base64 encode the output data for download.
    /// </summary>
    BASE_64_TO_DOWNLOAD,
}
