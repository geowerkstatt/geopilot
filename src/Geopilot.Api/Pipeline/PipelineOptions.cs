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
}
