using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Processing;

/// <summary>
/// Keeps the runs of the jobs this instance holds alive (see <see cref="Liveness"/>): every
/// <see cref="Liveness.HeartbeatInterval"/> it refreshes their heartbeat in one statement, with the database clock. It
/// refreshes only runs the database still considers alive and aborts the job of every other one, so an instance that
/// stood still for longer than <see cref="Liveness.DeadAfter"/> never revives a run another instance may already have
/// started again. When the heartbeat cannot be written, it aborts every job it holds at the last failed tick within
/// <see cref="Liveness.AbortAfter"/> of the last success, measured with the monotonic clock of the instance, so before
/// the others consider their runs dead.
/// </summary>
public class RecordingHeartbeatService : BackgroundService
{
    private readonly JobLifecycle lifecycle;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<RecordingHeartbeatService> logger;

    // A timestamp of the time provider, so a step of the wall clock does not move it. Only the ticks touch it, and
    // they run one after the other.
    private long lastSuccess;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingHeartbeatService"/> class.
    /// </summary>
    public RecordingHeartbeatService(JobLifecycle lifecycle, IServiceScopeFactory serviceScopeFactory, TimeProvider timeProvider, ILogger<RecordingHeartbeatService> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.lifecycle = lifecycle;
        this.serviceScopeFactory = serviceScopeFactory;
        this.timeProvider = timeProvider;
        this.logger = logger;
        lastSuccess = timeProvider.GetTimestamp();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Liveness.HeartbeatInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // An exception that escaped would stop the host, and with it every job of the instance.
                logger.LogError(ex, "The heartbeat of the held jobs failed unexpectedly; the next tick tries again.");
            }
        }
    }

    /// <summary>
    /// Refreshes the runs of the held jobs once and aborts the jobs whose run is no longer alive, or every held job at
    /// the last failed tick within <see cref="Liveness.AbortAfter"/> of the last success.
    /// </summary>
    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        // Taken before the statement: the heartbeat it writes is younger, so the time measured from here is never short.
        var started = timeProvider.GetTimestamp();
        var jobIds = lifecycle.HeldJobIds;
        if (jobIds.Count == 0)
        {
            // Nothing is kept alive, so nothing can be lost while the database is away.
            lastSuccess = started;
            return;
        }

        IReadOnlySet<Guid> refreshed;
        try
        {
            // Bounded, so a statement that hangs still counts as failed before the limit runs out.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Liveness.HeartbeatInterval);
            refreshed = await BeatAsync(jobIds, timeout.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Ticks come one interval apart, so a failure of the last tick within the limit has to abort: the next one
            // could already come after the others consider the runs dead.
            var sinceLastSuccess = timeProvider.GetElapsedTime(lastSuccess);
            if (sinceLastSuccess + Liveness.HeartbeatInterval <= Liveness.AbortAfter)
            {
                logger.LogWarning(ex, "The heartbeat of {Count} jobs could not be written; the last success started {Elapsed} ago.", jobIds.Count, sinceLastSuccess);
                return;
            }

            logger.LogError(ex, "No heartbeat could be written for {Elapsed}; aborting all {Count} jobs of this instance before the others consider their runs dead.", sinceLastSuccess, jobIds.Count);
            lifecycle.Abort(jobIds, "database unreachable");
            return;
        }

        lastSuccess = started;

        var lost = jobIds.Where(jobId => !refreshed.Contains(jobId)).ToList();
        foreach (var jobId in lost)
            logger.LogError("The database considers the run of job <{JobId}> dead; aborting the job.", jobId);

        lifecycle.Abort(lost, "heartbeat lost");
    }

    /// <summary>
    /// Refreshes the heartbeat of the runs of <paramref name="jobIds"/> whose heartbeat is still fresh, in a single
    /// statement with the database clock, and returns their job ids. A run whose heartbeat is older than
    /// <see cref="Liveness.DeadAfter"/>, or that has none, is left as it is and missing from the result.
    /// </summary>
    internal async Task<IReadOnlySet<Guid>> BeatAsync(IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var ids = jobIds.ToArray();

        // Only the heartbeat half of Liveness.WhereAlive: a held job whose run has settled meanwhile is still being
        // carried out and must not be aborted. A scalar SqlQuery reads its column by the name Value.
        var refreshed = await context.Database
            .SqlQuery<Guid>($"""
                UPDATE "Recordings" SET "HeartbeatAt" = now()
                WHERE "JobId" = ANY({ids}) AND "HeartbeatAt" > now() - {Liveness.DeadAfter}
                RETURNING "JobId" AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return refreshed.ToHashSet();
    }
}
