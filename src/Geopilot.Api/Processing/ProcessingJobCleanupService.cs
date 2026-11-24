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

            // Downloads expire on a separate (typically much shorter) retention so the
            // user-facing artifacts don't linger past their useful window.
            foreach (var jobId in EnumerateJobIds(directoryProvider.DownloadDirectory))
            {
                var job = jobStore.GetJob(jobId);
                if (job is null || HasExpiredSinceFinished(job, now, processingOptions.DownloadRetention))
                {
                    if (DeleteIfExists(directoryProvider.GetDownloadDirectoryPath(jobId)))
                        cleanedDownloads++;
                }
            }

            // Visualizations expire on their own (typically shortest) retention: a visualization is only
            // needed while the user views the job result in the browser.
            foreach (var jobId in EnumerateJobIds(directoryProvider.VisualizationDirectory))
            {
                var job = jobStore.GetJob(jobId);
                if (job is null || HasExpiredSinceFinished(job, now, processingOptions.VisualizationRetention))
                {
                    if (DeleteIfExists(directoryProvider.GetVisualizationDirectoryPath(jobId)))
                        cleanedVisualizations++;
                }
            }

            // Delivery and staging directories follow the deliveries in the database alone, deleted ones included:
            // a delivery directory exists only once its delivery is saved, so one without a delivery holds a
            // delivery the database no longer knows, after restoring an older database for instance. It is
            // reported, never deleted (#947).
            var deliveryRows = await LoadDeliveriesAsync(dbContext);
            var deliveries = ByJob(deliveryRows);
            var movedStagings = MoveStagedDeliveriesIntoPlace(deliveries);
            ReportMismatches(deliveryRows, deliveries);

            // The in-memory job entry ages out on JobRetention, counted from the end of its run like the downloads,
            // so retiring a job never cuts their retention short; a job that has not finished counts from its
            // creation, which is how a stuck one shows up below. Its upload is not released here: it went when
            // the job ended, or it stays on purpose until its upload session expires (a cancelled run, see
            // JobLifecycle.Decide). A delivery directory goes only once all deliveries of its job are deleted,
            // the fallback for files DeliveryController.Delete could not remove. A staged payload without an
            // active delivery belongs to a deliverable run that was never delivered: once the in-memory job is
            // gone nobody can create its delivery any more, so it retires with the job.
            // Pipeline working directories are normally removed by Pipeline.Dispose, but survive a hard restart
            // (nothing gets disposed), so they count as retirement candidates too.
            var retiredCandidates = EnumerateJobIds(directoryProvider.AssetDirectory);
            retiredCandidates.UnionWith(EnumerateJobIds(directoryProvider.AssetStagingDirectory));
            retiredCandidates.UnionWith(EnumerateJobIds(directoryProvider.PipelineDirectory));
            retiredCandidates.UnionWith(jobStore.GetJobIds());
            foreach (var jobId in retiredCandidates)
            {
                var job = jobStore.GetJob(jobId);
                if (job != null && now - (job.FinishedAt ?? job.CreatedAt) <= processingOptions.JobRetention)
                    continue;

                // A job still pending or running past its retention is stuck, not stale: its preflight or
                // its pipeline is hanging. Retiring it would delete its working directory and drop the job from
                // under a run that may yet continue. Leave it; UploadCleanupService's age-based sweep is the
                // backstop for its upload.
                if (job != null && job.State is ProcessingState.Pending or ProcessingState.Running)
                {
                    logger.LogWarning(
                        "Job <{JobId}> is still <{State}> after {Age} and past the retention of {Retention}; not retiring it. Check whether preflight or the pipeline is stuck.",
                        jobId,
                        job.State,
                        now - job.CreatedAt,
                        processingOptions.JobRetention);
                    continue;
                }

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
    private void ReportMismatches(IReadOnlyList<DeliveryRow> rows, IReadOnlyDictionary<Guid, JobDeliveries> deliveries)
    {
        var deliveryDirectories = EnumerateJobIds(directoryProvider.AssetDirectory);

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
        var directoriesOnTheirWay = EnumerateJobIds(directoryProvider.AssetStagingDirectory);
        var deliveriesWithoutDirectory = rows
            .Where(row => !row.Deleted)
            .Select(row => row.JobId)
            .Distinct()
            .Where(jobId => !deliveryDirectories.Contains(jobId) && !directoriesOnTheirWay.Contains(jobId))
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

    /// <summary>
    /// Whether a job's files on a <paramref name="retention"/> are due. Counted from the end of the run, so a job
    /// that waited long in the queue keeps them for the full window, and a job still running keeps them.
    /// </summary>
    private static bool HasExpiredSinceFinished(ProcessingJob job, DateTime now, TimeSpan retention) =>
        job.FinishedAt is { } finishedAt && now - finishedAt > retention;

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
}
