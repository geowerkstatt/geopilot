using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Process for validating ILI files.
/// </summary>
internal class IliValidatorProcess : IPipelineProcess
{
    /// <inheritdoc/>
    public string? Name { get; set; }

    /// <summary>
    /// ToDo: Define data handling configuration for ILI validation process.
    /// </summary>
    public DataHandlingConfig? DataHandlingConfig { get; set; }

    /// <summary>
    /// ToDo: Define configuration for ILI validation process.
    /// </summary>
    public Dictionary<string, string>? Config { get; set; }
}
