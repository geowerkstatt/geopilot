using Geopilot.Api.Contracts;
using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

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
    /// <param name="file">The file to be processed by the pipeline.</param>
    /// <returns>A <see cref="Pipeline"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the pipeline cannot be created.</exception>
    IPipeline CreatePipeline(string id, IPipelineTransferFile file);
}
