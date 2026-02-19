using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Provides methods for managing and retrieving pipelines.
/// </summary>
public class PipelineService : IPipelineService
{
    private readonly ILogger<PipelineService> logger;
    private readonly IPipelineFactory pipelineFactory;

    /// <summary>
    /// Initializes a new instance of the PipelineService class with the specified logger.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information for the service. Cannot be null.</param>
    /// <param name="pipelineFactory">The factory used to create pipeline instances. Cannot be null.</param>
    public PipelineService(ILogger<PipelineService> logger, IPipelineFactory pipelineFactory)
    {
        this.logger = logger;
        this.pipelineFactory = pipelineFactory;
    }

    /// <inheritdoc />
    public List<PipelineConfig> GetAvailablePipelines()
    {
        return pipelineFactory.Pipelines;
    }

    /// <inheritdoc />
    public PipelineConfig? GetById(string id)
    {
        return pipelineFactory.Pipelines.FirstOrDefault(p => p.Id == id);
    }
}
