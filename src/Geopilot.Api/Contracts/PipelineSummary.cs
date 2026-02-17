namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents a summary of a pipeline.
/// </summary>
public record PipelineSummary
{
    /// <summary>
    /// Gets the unique identifier of the pipeline.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the display name of the pipeline in different languages.
    /// </summary>
    public required Dictionary<string, string> DisplayName { get; init; }
}
