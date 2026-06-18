using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents a summary of a pipeline.
/// </summary>
/// <param name="Id">The unique identifier of the pipeline.</param>
/// <param name="DisplayName">The localized display name of the pipeline.</param>
public record PipelineSummary(string Id, LocalizedText DisplayName);
