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
    /// Lists the pipeline runs that match the <paramref name="query"/>, newest first, one page at a time. The attempts
    /// of one delivery, runs with the same client reference, declarer and mandate, form one group and one row.
    /// </summary>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "One page of pipeline runs, grouped by delivery.", typeof(PipelineRunPageResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The period ends before it starts.", typeof(ValidationProblemDetails), "application/json")]
    public async Task<IActionResult> List([FromQuery] PipelineRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From > query.To)
            ModelState.AddModelError(nameof(query.To), "The period must not end before it starts.");
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var from = query.From is { } fromValue ? AsUtc(fromValue) : (DateTime?)null;
        var to = query.To is { } toValue ? AsUtc(toValue) : (DateTime?)null;
        var latestRuns = FilterByState(
            LatestAttemptOfEachGroup(FilterByMandateAndDeclarer(context.PipelineRuns.AsNoTracking(), query), from, to),
            query.States);
        var totalCount = await latestRuns.CountAsync(cancellationToken);

        // shortcut: fixed order; the outcome is derived per page in C#, so it cannot be filtered on yet.
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // A page beyond any offset the database takes lies past the last run like any other page there.
        var skip = (long)Math.Max(query.Page, 0) * pageSize;
        if (skip > int.MaxValue)
            return Ok(new PipelineRunPageResponse(totalCount, []));

        var pageRuns = await WithSummaryIncludes(latestRuns
                .OrderByDescending(r => r.StartedAt)
                .ThenByDescending(r => r.Id)
                .Skip((int)skip)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        var attemptCounts = await CountAttemptsAsync(pageRuns, cancellationToken);
        var deliveries = await PipelineRunOutcomes.LoadDeliveriesAsync(context, pageRuns.Select(r => r.JobId).ToList(), cancellationToken);
        var items = pageRuns
            .Select(run => new PipelineRunGroupResponse(ToSummary(run, deliveries), attemptCounts.GetValueOrDefault(GroupKeyOf(run), 1)))
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
    public async Task<IActionResult> Get(Guid jobId, CancellationToken cancellationToken)
    {
        logger.LogTrace("Execution protocol for job <{JobId}> requested.", jobId);

        var run = await context.PipelineRunsWithIncludes
            .Include(r => r.User)
            .Include(r => r.MachineClient)
            .Include(r => r.Mandate)
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.JobId == jobId, cancellationToken);
        if (run is null)
            return Problem($"No execution protocol found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);

        var attempts = await LoadAttemptsOfDeliveryAsync(run, cancellationToken);
        var deliveries = await PipelineRunOutcomes.LoadDeliveriesAsync(context, attempts.Select(r => r.JobId).ToList(), cancellationToken);
        var delivery = deliveries.GetValueOrDefault(run.JobId);
        var outcome = PipelineRunOutcomes.Determine(run, IsRunning(run.JobId), delivery);
        var (forwardingState, notificationState) = await PipelineRunOutcomes.LoadReactionStatesAsync(context, delivery, cancellationToken);
        var attemptSummaries = attempts.Select(attempt => ToSummary(attempt, deliveries)).ToList();
        return Ok(run.ToResponse(outcome, forwardingState, notificationState, attemptSummaries));
    }

    /// <summary>
    /// Gets the definition snapshot the job with the specified <paramref name="jobId"/> executed on.
    /// Served separately from the protocol because the document is several kilobytes of configuration.
    /// </summary>
    [HttpGet("{jobId}/definition")]
    [SwaggerResponse(StatusCodes.Status200OK, "The definition snapshot the job executed on.", contentTypes: "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No execution protocol exists for the specified jobId.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> GetDefinition(Guid jobId, CancellationToken cancellationToken)
    {
        logger.LogTrace("Definition snapshot for job <{JobId}> requested.", jobId);

        var definition = await context.PipelineRuns
            .Where(r => r.JobId == jobId)
            .Select(r => r.Definition)
            .SingleOrDefaultAsync(cancellationToken);
        if (definition is null)
            return Problem($"No execution protocol found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);

        // Stored as jsonb and passed through verbatim; it was serialized as JSON when the job started.
        return Content(definition, "application/json");
    }

    // Mandate and declarer are part of the group key, so narrowing by them before grouping splits no group.
    private static IQueryable<PipelineRun> FilterByMandateAndDeclarer(IQueryable<PipelineRun> runs, PipelineRunQuery query)
    {
        var mandateIds = query.MandateIds;
        var userIds = query.UserIds;
        var machineClientIds = query.MachineClientIds;

        if (mandateIds.Count > 0)
            runs = runs.Where(r => r.MandateId != null && mandateIds.Contains(r.MandateId.Value));

        if (userIds.Count > 0 || machineClientIds.Count > 0)
        {
            runs = runs.Where(r =>
                (r.UserId != null && userIds.Contains(r.UserId.Value))
                || (r.MachineClientId != null && machineClientIds.Contains(r.MachineClientId.Value)));
        }

        return runs;
    }

    // Only the runs with a client reference are grouped: a run without one is a delivery of its own, taken by its
    // start. Rows are written when a job starts, so the highest id of a group is its newest attempt.
    private static IQueryable<PipelineRun> LatestAttemptOfEachGroup(IQueryable<PipelineRun> runs, DateTime? from, DateTime? to)
    {
        var latestOfDeliveries = runs
            .Where(r => r.ClientReference != null)
            .GroupBy(r => new { r.ClientReference, r.UserId, r.MachineClientId, r.MandateId })
            .Where(group => group.Count(r => (from == null || r.StartedAt >= from) && (to == null || r.StartedAt < to)) > 0)
            .Select(group => group.Max(r => r.Id));
        return runs.Where(r =>
            (r.ClientReference == null && (from == null || r.StartedAt >= from) && (to == null || r.StartedAt < to))
            || latestOfDeliveries.Contains(r.Id));
    }

    private static IQueryable<PipelineRun> FilterByState(IQueryable<PipelineRun> runs, List<PipelineRunStateFilter> states)
    {
        if (states.Count == 0)
            return runs;

        var terminalStates = states.SelectMany(TerminalStatesOf).ToList();
        var open = states.Contains(PipelineRunStateFilter.Open);
        return runs.Where(r => (r.TerminalState != null && terminalStates.Contains(r.TerminalState.Value)) || (open && r.TerminalState == null));
    }

    // An open run may also have no terminal state at all, which FilterByState adds.
    private static ProcessingState[] TerminalStatesOf(PipelineRunStateFilter state) => state switch
    {
        PipelineRunStateFilter.Success => [ProcessingState.Success],
        PipelineRunStateFilter.Warning => [ProcessingState.Warning],
        PipelineRunStateFilter.DeliveryRestriction => [ProcessingState.DeliveryRestriction],
        PipelineRunStateFilter.Failed => [ProcessingState.Failed],
        PipelineRunStateFilter.Cancelled => [ProcessingState.Cancelled],
        PipelineRunStateFilter.Open => [ProcessingState.Pending, ProcessingState.Running],
        _ => [],
    };

    // The group key of LatestAttemptOfEachGroup, newest first; a run without reference is a delivery of its own.
    private async Task<List<PipelineRun>> LoadAttemptsOfDeliveryAsync(PipelineRun run, CancellationToken cancellationToken)
    {
        if (run.ClientReference is null)
            return [run];

        return await WithSummaryIncludes(context.PipelineRuns.AsNoTracking().Where(r =>
                r.ClientReference == run.ClientReference
                && r.UserId == run.UserId
                && r.MachineClientId == run.MachineClientId
                && r.MandateId == run.MandateId))
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    // Counts the groups of LatestAttemptOfEachGroup that the given runs stand for. A run without reference is a group
    // of its own and has no entry.
    private async Task<Dictionary<(string? ClientReference, int? UserId, int? MachineClientId, int? MandateId), int>> CountAttemptsAsync(IReadOnlyList<PipelineRun> latestRuns, CancellationToken cancellationToken)
    {
        var references = latestRuns.Where(r => r.ClientReference is not null).Select(r => r.ClientReference).Distinct().ToList();
        if (references.Count == 0)
            return [];

        var groups = await context.PipelineRuns
            .Where(r => references.Contains(r.ClientReference))
            .GroupBy(r => new { r.ClientReference, r.UserId, r.MachineClientId, r.MandateId })
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return groups.ToDictionary(group => (group.Key.ClientReference, group.Key.UserId, group.Key.MachineClientId, group.Key.MandateId), group => group.Count);
    }

    private static (string? ClientReference, int? UserId, int? MachineClientId, int? MandateId) GroupKeyOf(PipelineRun run)
        => (run.ClientReference, run.UserId, run.MachineClientId, run.MandateId);

    private static IQueryable<PipelineRun> WithSummaryIncludes(IQueryable<PipelineRun> runs)
        => runs.Include(r => r.Steps).Include(r => r.User).Include(r => r.MachineClient).Include(r => r.Mandate);

    private PipelineRunSummaryResponse ToSummary(PipelineRun run, IReadOnlyDictionary<Guid, PipelineRunDelivery> deliveries) => new(
        run.JobId,
        run.StartedAt,
        run.TerminalAt,
        run.ToDeclarer(),
        run.ToMandateReference(),
        run.TerminalState,
        PipelineRunOutcomes.Determine(run, IsRunning(run.JobId), deliveries.GetValueOrDefault(run.JobId)));

    // The API documents its times as UTC: a time without offset binds as unspecified and is read as UTC, not in the
    // time zone of the host. The database compares timestamps with time zone and takes only UTC values.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    // A run without terminal state is either still running here or died with its instance.
    private bool IsRunning(Guid jobId) => processingService.GetJob(jobId)?.State is ProcessingState.Pending or ProcessingState.Running;
}
