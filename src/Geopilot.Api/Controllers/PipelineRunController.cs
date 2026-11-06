using Asp.Versioning;
using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
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
    private const int MaxPageSize = 100;

    private readonly ILogger<PipelineRunController> logger;
    private readonly Context context;
    private readonly IProcessingService processingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunController"/> class.
    /// </summary>
    public PipelineRunController(ILogger<PipelineRunController> logger, Context context, IProcessingService processingService)
    {
        this.logger = logger;
        this.context = context;
        this.processingService = processingService;
    }

    /// <summary>
    /// Lists the pipeline runs that match the <paramref name="query"/>, newest first, one page at a time.
    /// </summary>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "One page of pipeline runs.", typeof(PipelineRunPageResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The period ends before it starts, or a declarer id lacks its kind.", typeof(ValidationProblemDetails), "application/json")]
    public async Task<IActionResult> List([FromQuery] PipelineRunQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From > query.To)
            ModelState.AddModelError(nameof(query.To), "The period must not end before it starts.");
        if (query.DeclarerId is not null && query.DeclarerKind is null)
            ModelState.AddModelError(nameof(query.DeclarerKind), "A declarer id needs the kind of declarer it belongs to.");
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var runs = Filter(context.PipelineRuns.AsNoTracking(), query);
        var totalCount = await runs.CountAsync();

        // shortcut: fixed order and no indexes; the outcome is derived per page in C#, so it cannot be filtered on yet.
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // A page beyond any offset the database takes lies past the last run like any other page there.
        var skip = (long)Math.Max(query.Page, 0) * pageSize;
        if (skip > int.MaxValue)
            return Ok(new PipelineRunPageResponse(totalCount, []));

        var pageRuns = await runs
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id)
            .Skip((int)skip)
            .Take(pageSize)
            .Include(r => r.Steps)
            .Include(r => r.User)
            .Include(r => r.MachineClient)
            .Include(r => r.Mandate)
            .ToListAsync();

        var deliveries = await PipelineRunOutcomes.LoadDeliveriesAsync(context, pageRuns.Select(r => r.JobId).ToList());
        var items = pageRuns
            .Select(run => new PipelineRunSummaryResponse(
                run.JobId,
                run.StartedAt,
                run.TerminalAt,
                run.ToDeclarer(),
                run.ClientKind,
                run.ToMandateReference(),
                run.PipelineId,
                run.TerminalState,
                PipelineRunOutcomes.Determine(run, IsRunning(run.JobId), deliveries.GetValueOrDefault(run.JobId))))
            .ToList();

        return Ok(new PipelineRunPageResponse(totalCount, items));
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
            .Include(r => r.User)
            .Include(r => r.MachineClient)
            .Include(r => r.Mandate)
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.JobId == jobId);
        if (run is null)
            return Problem($"No execution protocol found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);

        var deliveries = await PipelineRunOutcomes.LoadDeliveriesAsync(context, [run.JobId]);
        var outcome = PipelineRunOutcomes.Determine(run, IsRunning(run.JobId), deliveries.GetValueOrDefault(run.JobId));
        return Ok(run.ToResponse(outcome));
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

    private static IQueryable<PipelineRun> Filter(IQueryable<PipelineRun> runs, PipelineRunQuery query)
    {
        var from = query.From is { } fromValue ? AsUtc(fromValue) : (DateTime?)null;
        var to = query.To is { } toValue ? AsUtc(toValue) : (DateTime?)null;
        if (from is not null)
            runs = runs.Where(r => r.StartedAt >= from);
        if (to is not null)
            runs = runs.Where(r => r.StartedAt < to);
        if (query.MandateId is not null)
            runs = runs.Where(r => r.MandateId == query.MandateId);

        runs = query.DeclarerKind switch
        {
            DeclarerKind.Person => runs.Where(r => r.UserId != null && (query.DeclarerId == null || r.UserId == query.DeclarerId)),
            DeclarerKind.MachineClient => runs.Where(r => r.MachineClientId != null && (query.DeclarerId == null || r.MachineClientId == query.DeclarerId)),
            _ => runs,
        };

        return query.State switch
        {
            PipelineRunStateFilter.Success => runs.Where(r => r.TerminalState == ProcessingState.Success),
            PipelineRunStateFilter.Warning => runs.Where(r => r.TerminalState == ProcessingState.Warning),
            PipelineRunStateFilter.DeliveryRestriction => runs.Where(r => r.TerminalState == ProcessingState.DeliveryRestriction),
            PipelineRunStateFilter.Failed => runs.Where(r => r.TerminalState == ProcessingState.Failed),
            PipelineRunStateFilter.Cancelled => runs.Where(r => r.TerminalState == ProcessingState.Cancelled),
            PipelineRunStateFilter.Open => runs.Where(r => r.TerminalState == null || r.TerminalState == ProcessingState.Pending || r.TerminalState == ProcessingState.Running),
            _ => runs,
        };
    }

    // The API documents its times as UTC: a time without offset binds as unspecified and is read as UTC, not in the
    // time zone of the host. The database compares timestamps with time zone and takes only UTC values.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    // A run without terminal state is either still running here or died with its instance.
    private bool IsRunning(Guid jobId) => processingService.GetJob(jobId)?.State is ProcessingState.Pending or ProcessingState.Running;
}
