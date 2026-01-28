using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Dummy process for testing purposes.
/// </summary>
internal class DummyProcess : IPipelineProcess
{
    /// <inheritdoc/>
    public required string Name { get; set; }

    /// <summary>
    /// ToDo: Define data handling configuration for the dummy process.
    /// </summary>
    public DataHandlingConfig? DataHandlingConfig { get; set; }

    /// <summary>
    /// ToDo: Define configuration for the dummy process.
    /// </summary>
    public Dictionary<string, string>? Config { get; set; }
}
