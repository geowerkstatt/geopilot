using Asp.Versioning;
using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Serves the execution protocol of processing jobs: the durable record of what ran, on which definition
/// version, for whom, and how it ended. Unlike the job status, the protocol survives restarts and the job
/// retention, so it stays answerable long after the job itself is gone. Admin only: the protocol spans
/// runs of all users.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Policy = GeopilotPolicies.Admin)]
public class PipelineRunController : ControllerBase
{
    private readonly ILogger<PipelineRunController> logger;
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunController"/> class.
    /// </summary>
    public PipelineRunController(ILogger<PipelineRunController> logger, Context context)
    {
        this.logger = logger;
        this.context = context;
    }

    /// <summary>
    /// Gets the execution protocol of the job with the specified <paramref name="jobId"/>.
    /// </summary>
    [HttpGet("{jobId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The execution protocol of the job.", typeof(PipelineRunResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No execution protocol exists for the specified jobId.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Get(Guid jobId)
    {
        logger.LogTrace("Execution protocol for job <{JobId}> requested.", jobId);

        var run = await context.PipelineRunsWithIncludes
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.JobId == jobId);
        if (run is null)
            return Problem($"No execution protocol found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);

        return Ok(run.ToResponse());
    }

    /// <summary>
    /// Gets the definition snapshot the job with the specified <paramref name="jobId"/> executed on.
    /// Served separately from the protocol because the document is several kilobytes of configuration.
    /// </summary>
    [HttpGet("{jobId}/definition")]
    [SwaggerResponse(StatusCodes.Status200OK, "The definition snapshot the job executed on.", contentTypes: "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No execution protocol exists for the specified jobId.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> GetDefinition(Guid jobId)
    {
        logger.LogTrace("Definition snapshot for job <{JobId}> requested.", jobId);

        var definition = await context.PipelineRuns
            .Where(r => r.JobId == jobId)
            .Select(r => r.Definition)
            .SingleOrDefaultAsync();
        if (definition is null)
            return Problem($"No execution protocol found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);

        // Stored as jsonb and passed through verbatim; it was serialized as JSON when the job started.
        return Content(definition, "application/json");
    }
}
