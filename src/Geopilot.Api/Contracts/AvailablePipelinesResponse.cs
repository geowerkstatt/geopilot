using Geopilot.Pipeline.Channels;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents the response containing available pipelines.
/// </summary>
/// <param name="Pipelines">The list of available pipelines.</param>
/// <param name="ReactionSets">The reaction sets a mandate can choose, each with the values it asks the mandate for.</param>
public record AvailablePipelinesResponse(IEnumerable<PipelineSummary> Pipelines, IEnumerable<ReactionSetSummary> ReactionSets);

/// <summary>
/// A reaction set as the form of a mandate offers it.
/// </summary>
/// <param name="Id">The id of the set, which a mandate refers to.</param>
/// <param name="DisplayName">The localized name of the set.</param>
/// <param name="MandateFields">The values the set asks the mandate for, in the order the definition declares them.</param>
public record ReactionSetSummary(string Id, LocalizedText DisplayName, IReadOnlyList<MandateField> MandateFields);
