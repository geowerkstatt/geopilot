using Geopilot.Api.Contracts;
using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Defines the contract for a service that manages or executes processing pipelines.
/// </summary>
/// <remarks>Implementations of this interface typically provide methods for configuring, executing, or monitoring
/// pipelines composed of multiple processing steps. The specific operations and usage patterns depend on the concrete
/// implementation.</remarks>
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
}
