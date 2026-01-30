using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Process for validating ILI files.
/// </summary>
internal class IliValidatorProcess : IPipelineProcess
{
    /// <inheritdoc/>
    public required string Name { get; set; }

    /// <summary>
    /// ToDo: Define data handling configuration for ILI validation process.
    /// </summary>
    public required DataHandlingConfig DataHandlingConfig { get; set; }

    /// <summary>
    /// ToDo: Define configuration for ILI validation process.
    /// </summary>
    public Dictionary<string, string>? Config { get; set; }

    public ProcessData Run(ProcessData data)
    {
        // ToDo: Implement ILI validation logic here.
        return new ProcessData();
    }
}
