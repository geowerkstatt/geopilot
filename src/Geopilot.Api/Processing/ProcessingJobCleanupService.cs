using Geopilot.Api.FileAccess;
using Geopilot.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Processing;

/// <summary>
/// Background service that periodically cleans up old or orphaned processing jobs and their associated files.
/// </summary>
public class ProcessingJobCleanupService : BackgroundService
{
    // Enough to start looking: a restore can leave thousands of mismatches, and they are reported every run.
    private const int ReportedJobIdLimit = 10;

    private readonly IProcessingJobStore jobStore;
    private readonly JobLifecycle lifecycle;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IAssetFileStore assetFileStore;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<ProcessingJobCleanupService> logger;
    private readonly ProcessingOptions processingOptions;
    private readonly SemaphoreSlim cleanupSemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingJobCleanupService"/> class.
    /// </summary>
    public ProcessingJobCleanupService(
        IProcessingJobStore jobStore,
        JobLifecycle lifecycle,
        IDirectoryProvider directoryProvider,
        IAssetFileStore assetFileStore,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ProcessingJobCleanupService> logger,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.jobStore = jobStore;
        this.lifecycle = lifecycle;
        this.directoryProvider = directoryProvider;
        this.assetFileStore = assetFileStore;
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
        this.processingOptions = processingOptions.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ProcessingJobCleanupService started. Cleanup interval: {Interval}.", processingOptions.JobCleanupInterval.ToString());

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync();
            await Task.Delay(processingOptions.JobCleanupInterval, stoppingToken);
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        cleanupSemaphore.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the cleanup of old or orphaned processing jobs and their associated files.
    /// </summary>
    public async Task RunCleanupAsync()
    {
        if (!cleanupSemaphore.Wait(0))
        {
            logger.LogWarning("Processing job cleanup is already running. Skipping this run.");
            return;
        }

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Context>();
            var now = DateTime.UtcNow;
            int cleanedDownloads = 0;
            int cleanedVisualizations = 0;
            int retiredJobs = 0;

            // Delivery and staging directories follow the deliveries in the database alone, deleted ones included:
            // a delivery directory exists only once its delivery is saved, so one without a delivery holds a
            // delivery the database no longer knows, after restoring an older database for instance. It is
            // reported, never deleted (#947). Both are listed before the deliveries load, staging first, so a creation
            // renaming in between leaves no false report: a listed delivery directory has its delivery loaded, and the
            // files of a loaded delivery are in one of the two lists.
            var stagingDirectories = EnumerateJobIds(directoryProvider.AssetStagingDirectory);
            var deliveryDirectories = EnumerateJobIds(directoryProvider.AssetDirectory);
            var deliveryRows = await LoadDeliveriesAsync(dbContext);
            var deliveries = ByJob(deliveryRows);
            var movedStagings = MoveStagedDeliveriesIntoPlace(deliveries);
            ReportMismatches(deliveryRows, deliveries, deliveryDirectories, stagingDirectories);

            // The run in the database decides, so no instance deletes the files of a run another instance executes,
            // except for the local pipeline directory: it exists before its run is recorded, so memory counts as well.
            var retiredCandidates = EnumerateJobIds(directoryProvider.AssetDirectory);
            retiredCandidates.UnionWith(EnumerateJobIds(directoryProvider.AssetStagingDirectory));
            retiredCandidates.UnionWith(EnumerateJobIds(directoryProvider.PipelineDirectory));
            retiredCandidates.UnionWith(jobStore.GetJobIds());
            var downloadDirectories = EnumerateJobIds(directoryProvider.DownloadDirectory);
            var visualizationDirectories = EnumerateJobIds(directoryProvider.VisualizationDirectory);

            var databaseNow = await dbContext.Database.SqlQuery<DateTime>($"SELECT now() AS \"Value\"").SingleAsync();
            var runs = await LoadRunsAsync(dbContext, retiredCandidates.Union(downloadDirectories).Union(visualizationDirectories).ToList());

            foreach (var jobId in downloadDirectories)
            {
                if (FilesAreDue(jobId, runs, databaseNow, processingOptions.DownloadRetention)
                    && DeleteExpired(jobId, directoryProvider.GetDownloadDirectoryPath))
                    cleanedDownloads++;
            }

            foreach (var jobId in visualizationDirectories)
            {
                if (FilesAreDue(jobId, runs, databaseNow, processingOptions.VisualizationRetention)
                    && DeleteExpired(jobId, directoryProvider.GetVisualizationDirectoryPath))
                    cleanedVisualizations++;
            }

            foreach (var jobId in retiredCandidates)
            {
                if (!IsRetirable(jobId, runs, databaseNow, now))
                    continue;

                if (RetireJob(jobId, deliveries.GetValueOrDefault(jobId, JobDeliveries.None)))
                    retiredJobs++;
            }

            var purgedRuns = await PurgeExpiredProtocolRecordsAsync(dbContext, now);

            logger.LogInformation("ProcessingJobCleanupService completed. Retired jobs: {Retired}, expired download dirs: {Downloads}, expired visualization dirs: {Visualizations}, staged deliveries moved into place: {MovedStagings}, purged protocol runs: {PurgedRuns}.", retiredJobs, cleanedDownloads, cleanedVisualizations, movedStagings, purgedRuns);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during processing job cleanup.");
        }
        finally
        {
            cleanupSemaphore.Release();
        }
    }

    /// <summary>
    /// Purges execution protocol records past their own retention, which is deliberately much longer than
    /// the job retention: the protocol is the audit record and outlives the jobs it describes. Children
    /// (files, steps, conditions, artifacts) go via FK cascade, and the definition snapshots live on the
    /// runs themselves, so nothing is left to sweep. An unset or non-positive retention disables purging,
    /// so a missing configuration key can never silently erase the protocol.
    /// </summary>
    private async Task<int> PurgeExpiredProtocolRecordsAsync(Context dbContext, DateTime now)
    {
        if (processingOptions.ProtocolRetention is not { } retention || retention <= TimeSpan.Zero)
            return 0;

        var cutoff = now - retention;
        return await dbContext.Recordings
            .Where(run => run.StartedAt < cutoff)
            .ExecuteDeleteAsync();
    }

    private static HashSet<Guid> EnumerateJobIds(string root)
    {
        var jobIds = new HashSet<Guid>();
        if (!Directory.Exists(root))
            return jobIds;
        foreach (var dir in Directory.GetDirectories(root))
        {
            if (Guid.TryParse(Path.GetFileName(dir), out var jobId))
                jobIds.Add(jobId);
        }

        return jobIds;
    }

    private static async Task<IReadOnlyList<DeliveryRow>> LoadDeliveriesAsync(Context dbContext)
        => await dbContext.Deliveries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(d => d.Id)
            .Select(d => new DeliveryRow(d.JobId, d.Deleted))
            .ToListAsync();

    private static Dictionary<Guid, JobDeliveries> ByJob(IReadOnlyList<DeliveryRow> rows)
        => rows
            .GroupBy(row => row.JobId)
            .ToDictionary(group => group.Key, group => group.Any(row => !row.Deleted) ? JobDeliveries.Active : JobDeliveries.OnlyDeleted);

    /// <summary>
    /// Moves the staged files of every active delivery into its delivery directory, whether the job is still known
    /// or not: the creation renames them only after saving, and when that rename failed, this is where it happens.
    /// </summary>
    private int MoveStagedDeliveriesIntoPlace(IReadOnlyDictionary<Guid, JobDeliveries> deliveries)
    {
        var moved = 0;
        foreach (var jobId in EnumerateJobIds(directoryProvider.AssetStagingDirectory))
        {
            if (deliveries.GetValueOrDefault(jobId) != JobDeliveries.Active)
                continue;

            if (Directory.Exists(directoryProvider.GetAssetDirectoryPath(jobId)))
            {
                logger.LogWarning("The staged files of the delivery of job <{JobId}> cannot move into place: its delivery directory already exists. geopilot keeps both; compare them by hand.", jobId);
                continue;
            }

            try
            {
                assetFileStore.PromoteStagedFiles(jobId);
                moved++;
            }
            catch (Exception ex)
            {
                // Also where a creation renaming the same directory at the same moment ends up, without harm.
                logger.LogWarning(ex, "Could not move the staged files of the delivery of job <{JobId}> into place. The next run tries again.", jobId);
            }
        }

        return moved;
    }

    /// <summary>
    /// Warns once per direction about delivery directories without a delivery and active deliveries without files,
    /// every run until an operator reconciles them. Nothing is deleted or filled in: only an operator can tell which
    /// side is right.
    /// </summary>
    private void ReportMismatches(
        IReadOnlyList<DeliveryRow> rows,
        IReadOnlyDictionary<Guid, JobDeliveries> deliveries,
        HashSet<Guid> deliveryDirectories,
        HashSet<Guid> stagingDirectories)
    {
        var directoriesWithoutDelivery = deliveryDirectories
            .Where(jobId => deliveries.GetValueOrDefault(jobId) == JobDeliveries.None)
            .ToList();
        if (directoriesWithoutDelivery.Count > 0)
        {
            logger.LogWarning(
                "{Count} delivery directories have no delivery in the database, first job ids: {JobIds}. geopilot keeps them. After restoring an older database, reconcile them by hand.",
                directoriesWithoutDelivery.Count,
                FirstJobIds(directoriesWithoutDelivery));
        }

        // Newest first, since files restored from an older backup lack the latest deliveries. Files still waiting in
        // staging are on their way, not missing.
        var deliveriesWithoutDirectory = rows
            .Where(row => !row.Deleted)
            .Select(row => row.JobId)
            .Distinct()
            .Where(jobId => !deliveryDirectories.Contains(jobId) && !stagingDirectories.Contains(jobId))
            .ToList();
        if (deliveriesWithoutDirectory.Count > 0)
        {
            logger.LogWarning(
                "{Count} deliveries have no delivery directory, first job ids: {JobIds}. Their files may be missing from Storage:AssetsDirectory, for example after restoring an older backup of it.",
                deliveriesWithoutDirectory.Count,
                FirstJobIds(deliveriesWithoutDirectory));
        }
    }

    private static string FirstJobIds(IEnumerable<Guid> jobIds) => string.Join(", ", jobIds.Take(ReportedJobIdLimit));

    private bool RetireJob(Guid jobId, JobDeliveries deliveries)
    {
        try
        {
            DeleteIfExists(directoryProvider.GetDownloadDirectoryPath(jobId));
            DeleteIfExists(directoryProvider.GetVisualizationDirectoryPath(jobId));
            DeleteIfExists(directoryProvider.GetPipelineDirectoryPath(jobId));
            if (deliveries != JobDeliveries.Active)
                DeleteIfExists(directoryProvider.GetAssetStagingDirectoryPath(jobId));
            if (deliveries == JobDeliveries.OnlyDeleted)
                DeleteIfExists(directoryProvider.GetAssetDirectoryPath(jobId));
            lifecycle.Retire(jobId);
            logger.LogTrace("Retired job <{JobId}>. Deliveries: {Deliveries}.", jobId, deliveries);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retiring job <{JobId}>.", jobId);
        }

        return false;
    }

    private static async Task<Dictionary<Guid, KnownRun>> LoadRunsAsync(Context dbContext, IReadOnlyList<Guid> jobIds)
    {
        var deliveries = dbContext.Deliveries.IgnoreQueryFilters();
        var known = dbContext.Recordings.AsNoTracking().Where(r => jobIds.Contains(r.JobId));
        var rows = await known
            .Select(r => new { r.JobId, r.StartedAt, r.HeartbeatAt, r.TerminalAt })
            .ToListAsync();
        var alive = (await known.WhereAlive(deliveries).Select(r => r.JobId).ToListAsync()).ToHashSet();
        var settled = (await known.WhereSettled(deliveries).Select(r => r.JobId).ToListAsync()).ToHashSet();

        return rows.ToDictionary(
            row => row.JobId,
            row => new KnownRun(
                row.StartedAt,
                alive.Contains(row.JobId),
                settled.Contains(row.JobId) && row.TerminalAt is { } terminalAt ? terminalAt : row.HeartbeatAt ?? row.StartedAt));
    }

    /// <summary>
    /// Whether the download or visualization files of a job on a <paramref name="retention"/> are due, counted from the
    /// end of its run. A job without a run is orphaned; a running one, here or elsewhere, keeps them.
    /// </summary>
    private bool FilesAreDue(Guid jobId, IReadOnlyDictionary<Guid, KnownRun> runs, DateTime databaseNow, TimeSpan retention)
    {
        if (IsRunningHere(jobStore.GetJob(jobId)))
            return false;
        if (!runs.TryGetValue(jobId, out var run))
            return true;
        return !run.Alive && databaseNow - run.End > retention;
    }

    /// <summary>
    /// Whether a job is past the job retention and may be retired. A job still running past it is stuck, not stale:
    /// retiring it would delete its working directory from under a run that may yet continue. It is reported and kept;
    /// UploadCleanupService's age-based sweep is the backstop for its upload.
    /// </summary>
    private bool IsRetirable(Guid jobId, IReadOnlyDictionary<Guid, KnownRun> runs, DateTime databaseNow, DateTime now)
    {
        var retention = processingOptions.JobRetention;
        var job = jobStore.GetJob(jobId);
        if (job is not null && IsRunningHere(job))
        {
            if (now - job.CreatedAt > retention)
            {
                logger.LogWarning(
                    "Job <{JobId}> is still <{State}> after {Age} and past the retention of {Retention}; not retiring it. Check whether preflight or the pipeline is stuck.",
                    jobId,
                    job.State,
                    now - job.CreatedAt,
                    retention);
            }

            return false;
        }

        if (runs.TryGetValue(jobId, out var run))
        {
            if (!run.Alive)
                return databaseNow - run.End > retention;

            if (databaseNow - run.StartedAt > retention)
            {
                logger.LogWarning(
                    "Job <{JobId}> is still running after {Age} and past the retention of {Retention}; not retiring it. Check whether preflight or the pipeline is stuck.",
                    jobId,
                    databaseNow - run.StartedAt,
                    retention);
            }

            return false;
        }

        return job is null || now - (job.FinishedAt ?? job.CreatedAt) > retention;
    }

    private static bool IsRunningHere(ProcessingJob? job) => job?.State is ProcessingState.Pending or ProcessingState.Running;

    /// <summary>
    /// Deletes an expired directory of a job, guarded like <see cref="RetireJob"/> so a failure skips only this directory.
    /// The cleanup of another instance may delete the same directory at the same moment, so one that is gone counts as
    /// deleted.
    /// </summary>
    private bool DeleteExpired(Guid jobId, Func<Guid, string> directoryOf)
    {
        try
        {
            var directory = directoryOf(jobId);
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception) when (!Directory.Exists(directory))
            {
                // Gone meanwhile, which is all this run wanted.
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting an expired directory of job <{JobId}>.", jobId);
            return false;
        }
    }

    private static bool DeleteIfExists(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        Directory.Delete(directory, recursive: true);
        return true;
    }

    /// <summary>
    /// What the database says about the deliveries of one job; it alone decides over its delivery and staging directories.
    /// </summary>
    private enum JobDeliveries
    {
        None,
        OnlyDeleted,
        Active,
    }

    private sealed record DeliveryRow(Guid JobId, bool Deleted);

    // End: TerminalAt of a settled run, else the last heartbeat (or the start) of a dead one. Meaningless while alive.
    private sealed record KnownRun(DateTime StartedAt, bool Alive, DateTime End);
}
