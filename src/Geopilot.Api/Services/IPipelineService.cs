using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;

namespace Geopilot.Api.Services;

/// <summary>
/// Provides methods for managing and retrieving pipelines.
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// Retrieves a list of pipeline configurations that are currently available for use.
    /// </summary>
    /// <returns>A list of <see cref="PipelineConfig"/> objects representing the available pipelines. The list is empty if no
    /// pipelines are available.</returns>
    List<PipelineConfig> GetAvailablePipelines();

    /// <summary>
    /// Retrieves the pipeline configuration with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the pipeline to retrieve. Cannot be null or empty.</param>
    /// <returns>The <see cref="PipelineConfig"/> object representing the pipeline if found; otherwise, null.</returns>
    PipelineConfig? GetById(string id);

    /// <summary>
    /// Retrieves the reaction sets of the definition, which a mandate can choose.
    /// </summary>
    /// <returns>The reaction sets, empty when the definition has none.</returns>
    IReadOnlyList<ReactionSetConfig> GetReactionSets();

    /// <summary>
    /// Describes the values a reaction set asks the mandate for.
    /// </summary>
    /// <param name="reactionSetId">The id of the set.</param>
    /// <returns>The values the set asks for, empty when it asks for none.</returns>
    IReadOnlyList<MandateField> DescribeMandateFields(string reactionSetId);
}
