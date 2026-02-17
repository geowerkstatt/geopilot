using Geopilot.Api.Contracts;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for managing pipelines.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[AllowAnonymous]
public class PipelineController : ControllerBase
{
    private readonly ILogger<PipelineController> logger;
    private readonly IPipelineService pipelineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineController"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pipelineService">The pipeline service instance.</param>
    public PipelineController(ILogger<PipelineController> logger, IPipelineService pipelineService)
    {
        this.logger = logger;
        this.pipelineService = pipelineService;
    }

    /// <summary>
    /// Retrieves the list of available pipelines that can be used for processing files.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing an <see cref="AvailablePipelinesResponse"/> object with the available
    /// pipeline summaries. The response is returned with HTTP status code 200 (OK).</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "The available pipelines.", typeof(AvailablePipelinesResponse), "application/json")]
    public async Task<IActionResult> GetAvailablePipelines()
    {
        var response = new AvailablePipelinesResponse()
        {
            Pipelines = this.pipelineService.GetAvailablePipelines().Select(ToPipelineSumary).ToList(),
        };
        return Ok(response);
    }

    private static PipelineSummary ToPipelineSumary(PipelineConfig pipeline)
    {
        return new PipelineSummary() { Id = pipeline.Id, DisplayName = pipeline.DisplayName, };
    }
}
