using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents configuration options for a pipeline, including its definition and associated plugins.
/// </summary>
public class PipelineOptions
{
    /// <summary>
    /// Gets or sets the definition for the pipelines (YAML).
    /// </summary>
    public required string Definition { get; set; }

    /// <summary>
    /// Gets or sets the list of processor plugin assembly that are used by the pipelines.
    /// </summary>
    public List<string>? Plugins { get; set; }

    /// <summary>
    /// Gets or sets the configuration settings for each process.
    /// </summary>
    /// <remarks>Each entry in the dictionary represents a process, where the key is the process name and the
    /// value is a <see cref="Parameterization"/> for that process.</remarks>
    public Dictionary<string, Parameterization> ProcessConfigs { get; set; } = new Dictionary<string, Parameterization>();
}
