using Geopilot.Api.Contracts;
using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

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
}
