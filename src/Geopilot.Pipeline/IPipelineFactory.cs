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
}
