namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents the response containing available pipelines.
/// </summary>
public record AvailablePipelinesResponse
{
    /// <summary>
    /// Gets the list of available pipelines.
    /// </summary>
    public required IEnumerable<PipelineSummary> Pipelines { get; init; }
}
