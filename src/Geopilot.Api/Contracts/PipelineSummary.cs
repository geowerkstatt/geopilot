namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents a summary of a pipeline.
/// </summary>
/// <param name="Id">The unique identifier of the pipeline.</param>
/// <param name="DisplayName">The display name of the pipeline in different languages.</param>
public record PipelineSummary(string Id, Dictionary<string, string> DisplayName);
