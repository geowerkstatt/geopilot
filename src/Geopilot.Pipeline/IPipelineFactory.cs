using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline;

/// <summary>
/// Factory interface for creating pipeline instances.
/// </summary>
public interface IPipelineFactory
{
    /// <summary>
    /// Gets the collection of pipeline configurations associated with this instance.
    /// </summary>
    List<PipelineConfig> Pipelines { get; }

    /// <summary>
    /// Gets the reaction sets of the definition, empty when it has none.
    /// </summary>
    IReadOnlyList<ReactionSetConfig> ReactionSets { get; }

    /// <summary>
    /// Resolves the transfers of a set: per reaction its id, the implementation of its channel and the configuration
    /// the pipeline definition gives it, which is the default configuration of the channel with the reaction's
    /// overwrites. The base configuration is not part of it.
    /// </summary>
    /// <param name="reactionSetId">The id of the set.</param>
    /// <returns>The transfers of the set.</returns>
    /// <exception cref="InvalidOperationException">No set with the specified id exists.</exception>
    IReadOnlyList<(string ReactionId, string Implementation, Parameterization Config)> ResolveTransfers(string reactionSetId);

    /// <summary>
    /// Checks the values a mandate supplies to the reactions of a set by constructing the channel of each reaction
    /// with them and discarding it again. Values for keys the set does not declare are the caller's to drop.
    /// </summary>
    /// <param name="reactionSetId">The id of the set.</param>
    /// <param name="values">The values per reaction id.</param>
    /// <returns>One message per reaction whose channel refuses its values, empty when all accept them.</returns>
    /// <exception cref="InvalidOperationException">No set with the specified id exists.</exception>
    IReadOnlyList<string> ValidateMandateValues(string reactionSetId, IReadOnlyDictionary<string, Parameterization> values);

    /// <summary>
    /// Describes the values a set asks the mandate for, in the order its reactions and their <c>mandate_config</c>
    /// declare them: the label from the definition, and from the channel whether a value is a text or a list of texts
    /// and whether the mandate must supply it.
    /// </summary>
    /// <param name="reactionSetId">The id of the set.</param>
    /// <returns>The values the set asks for, empty when it asks for none.</returns>
    /// <exception cref="InvalidOperationException">No set with the specified id exists.</exception>
    IReadOnlyList<MandateField> DescribeMandateFields(string reactionSetId);

    /// <summary>
    /// Creates a pipeline instance with the specified id.
    /// </summary>
    /// <param name="id">The id of the pipeline to be created.</param>
    /// <param name="jobId">Job-Id for the pipeline.</param>
    /// <returns>A <see cref="Pipeline"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the pipeline cannot be created.</exception>
    IPipeline CreatePipeline(string id, Guid jobId);

    /// <summary>
    /// Validates the pipeline definition and the process configuration of every step, without constructing any
    /// process. The caller decides what an invalid definition means; the host refuses to start.
    /// </summary>
    /// <returns>The outcome, with all problems in a single message.</returns>
    PipelineDefinitionValidationResult ValidateDefinition();

    /// <summary>
    /// Renders the definition snapshot of one pipeline as JSON: the pipeline as configured, the process
    /// catalog entries its steps reference, and the base configuration (appsettings
    /// <c>Pipeline:ProcessConfigs</c>) effective for those implementations. The document is shaped like a
    /// minimal definition file restricted to that pipeline, so it records the exact basis a job executes on,
    /// unaffected by later edits to the definition file.
    /// </summary>
    /// <param name="pipelineId">The id of the pipeline to snapshot.</param>
    /// <returns>The snapshot as a JSON document.</returns>
    /// <exception cref="InvalidOperationException">No pipeline with the specified id exists.</exception>
    string GetDefinitionSnapshotJson(string pipelineId);
}
