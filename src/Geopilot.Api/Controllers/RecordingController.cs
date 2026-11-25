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
public class RecordingController : ControllerBase
{
    private readonly ILogger<RecordingController> logger;
    private readonly Context context;
    private readonly IProcessingService processingService;
    private readonly IPipelineFactory pipelineFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingController"/> class.
    /// </summary>
    public RecordingController(ILogger<RecordingController> logger, Context context, IProcessingService processingService, IPipelineFactory pipelineFactory)
    {
        this.logger = logger;
        this.context = context;
        this.processingService = processingService;
        this.pipelineFactory = pipelineFactory;
    }

    /// <summary>
    /// Lists the recordings that match the <paramref name="query"/>, newest first, one page at a time. The attempts
    /// of one delivery, runs with the same client reference, starter and mandate, form one group and one row.
    /// </summary>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "One page of recordings, grouped by delivery.", typeof(RecordingPageResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The period ends before it starts, or the page or a filter is out of bounds.", typeof(ValidationProblemDetails), "application/json")]
    public async Task<IActionResult> List([FromQuery] RecordingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From > query.To)
            ModelState.AddModelError(nameof(query.To), "The period must not end before it starts.");
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var from = query.From is { } fromValue ? AsUtc(fromValue) : (DateTime?)null;
        var to = query.To is { } toValue ? AsUtc(toValue) : (DateTime?)null;
        var latestRuns = FilterByState(
            LatestAttemptOfEachGroup(FilterByMandateAndStarter(context.Recordings.AsNoTracking(), query), from, to),
            query.States);
        var totalCount = await latestRuns.CountAsync(cancellationToken);

        // shortcut: fixed order; the outcome is derived per page in C#, so it cannot be filtered on yet.
        var pageRuns = await WithSummaryIncludes(latestRuns
                .OrderByDescending(r => r.StartedAt)
                .ThenByDescending(r => r.Id)
                .Skip(query.Page * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(cancellationToken);

        var attemptCounts = await CountAttemptsAsync(pageRuns, cancellationToken);
        var deliveries = await RecordingOutcomes.LoadDeliveriesAsync(context, pageRuns.Select(r => r.JobId).ToList(), cancellationToken);
        var items = pageRuns
            .Select(run => new RecordingGroupResponse(ToSummary(run, deliveries), attemptCounts.GetValueOrDefault(GroupKeyOf(run), 1)))
            .ToList();

        return Ok(new RecordingPageResponse(totalCount, items));
    }

    /// <summary>
    /// Gets the execution protocol of the job with the specified <paramref name="jobId"/>.
    /// </summary>
    [HttpGet("{jobId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The execution protocol of the job.", typeof(RecordingResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "No execution protocol exists for the specified jobId.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Get(Guid jobId, CancellationToken cancellationToken)
    {
        logger.LogTrace("Execution protocol for job <{JobId}> requested.", jobId);

        var run = await context.RecordingsWithIncludes
            .Include(r => r.User)
            .Include(r => r.MachineClient)
            .Include(r => r.Mandate)
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.JobId == jobId, cancellationToken);
        if (run is null)
            return Problem($"No execution protocol found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);

        var attempts = await LoadAttemptsOfDeliveryAsync(run, cancellationToken);
        var deliveries = await RecordingOutcomes.LoadDeliveriesAsync(context, attempts.Select(r => r.JobId).ToList(), cancellationToken);
        var delivery = deliveries.GetValueOrDefault(run.JobId);
        var outcome = RecordingOutcomes.Determine(run, IsRunning(run.JobId), delivery);
        var (forwardingState, notificationState) = await RecordingOutcomes.LoadReactionStatesAsync(context, delivery, cancellationToken);
        var attemptSummaries = attempts.Select(attempt => ToSummary(attempt, deliveries)).ToList();
        return Ok(run.ToResponse(outcome, forwardingState, notificationState, attemptSummaries, pipelineFactory.ReactionDisplayNames()));
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

        var definition = await context.Recordings
            .Where(r => r.JobId == jobId)
            .Select(r => r.Definition)
            .SingleOrDefaultAsync(cancellationToken);
        if (definition is null)
            return Problem($"No execution protocol found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);

        // Stored as jsonb and passed through verbatim; it was serialized as JSON when the job started.
        return Content(definition, "application/json");
    }

    // Mandate and starter are part of the group key, so narrowing by them before grouping splits no group.
    private static IQueryable<Recording> FilterByMandateAndStarter(IQueryable<Recording> runs, RecordingQuery query)
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
    private static IQueryable<Recording> LatestAttemptOfEachGroup(IQueryable<Recording> runs, DateTime? from, DateTime? to)
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

    private static IQueryable<Recording> FilterByState(IQueryable<Recording> runs, List<RecordingStateFilter> states)
    {
        if (states.Count == 0)
            return runs;

        var terminalStates = states.SelectMany(TerminalStatesOf).ToList();
        var open = states.Contains(RecordingStateFilter.Open);
        return runs.Where(r => (r.TerminalState != null && terminalStates.Contains(r.TerminalState.Value)) || (open && r.TerminalState == null));
    }

    // An open run may also have no terminal state at all, which FilterByState adds.
    private static ProcessingState[] TerminalStatesOf(RecordingStateFilter state) => state switch
    {
        RecordingStateFilter.Success => [ProcessingState.Success],
        RecordingStateFilter.Warning => [ProcessingState.Warning],
        RecordingStateFilter.DeliveryRestriction => [ProcessingState.DeliveryRestriction],
        RecordingStateFilter.Failed => [ProcessingState.Failed],
        RecordingStateFilter.Cancelled => [ProcessingState.Cancelled],
        RecordingStateFilter.Open => [ProcessingState.Pending, ProcessingState.Running],
        _ => [],
    };

    // The group key of LatestAttemptOfEachGroup, newest first; a run without reference is a delivery of its own.
    // shortcut: loads every attempt with its steps, each time the page of a run asks; only the newest ones with their
    // number if a client keeps sending one reference for all its deliveries.
    private async Task<List<Recording>> LoadAttemptsOfDeliveryAsync(Recording run, CancellationToken cancellationToken)
    {
        if (run.ClientReference is null)
            return [run];

        return await WithSummaryIncludes(context.Recordings.AsNoTracking().Where(r =>
                r.ClientReference == run.ClientReference
                && r.UserId == run.UserId
                && r.MachineClientId == run.MachineClientId
                && r.MandateId == run.MandateId))
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    // Counts the groups of LatestAttemptOfEachGroup that the given runs stand for. A run without reference is a group
    // of its own and has no entry.
    private async Task<Dictionary<(string? ClientReference, int? UserId, int? MachineClientId, int? MandateId), int>> CountAttemptsAsync(IReadOnlyList<Recording> latestRuns, CancellationToken cancellationToken)
    {
        var references = latestRuns.Where(r => r.ClientReference is not null).Select(r => r.ClientReference).Distinct().ToList();
        if (references.Count == 0)
            return [];

        var groups = await context.Recordings
            .Where(r => references.Contains(r.ClientReference))
            .GroupBy(r => new { r.ClientReference, r.UserId, r.MachineClientId, r.MandateId })
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return groups.ToDictionary(group => (group.Key.ClientReference, group.Key.UserId, group.Key.MachineClientId, group.Key.MandateId), group => group.Count);
    }

    private static (string? ClientReference, int? UserId, int? MachineClientId, int? MandateId) GroupKeyOf(Recording run)
        => (run.ClientReference, run.UserId, run.MachineClientId, run.MandateId);

    private static IQueryable<Recording> WithSummaryIncludes(IQueryable<Recording> runs)
        => runs.Include(r => r.Steps).Include(r => r.User).Include(r => r.MachineClient).Include(r => r.Mandate);

    private RecordingSummaryResponse ToSummary(Recording run, IReadOnlyDictionary<Guid, RecordingDelivery> deliveries) => new(
        run.JobId,
        run.StartedAt,
        run.TerminalAt,
        run.ToStarter(),
        run.ToMandateReference(),
        run.TerminalState,
        RecordingOutcomes.Determine(run, IsRunning(run.JobId), deliveries.GetValueOrDefault(run.JobId)));

    // The API documents its times as UTC: a time without offset binds as unspecified and is read as UTC, not in the
    // time zone of the host. The database compares timestamps with time zone and takes only UTC values.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    // A run without terminal state is either still running here or died with its instance.
    private bool IsRunning(Guid jobId) => processingService.GetJob(jobId)?.State is ProcessingState.Pending or ProcessingState.Running;
}
