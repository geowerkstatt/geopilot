namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents the response containing available pipelines.
/// </summary>
/// <param name="Pipelines">The list of available pipelines.</param>
public record AvailablePipelinesResponse(IEnumerable<PipelineSummary> Pipelines);
