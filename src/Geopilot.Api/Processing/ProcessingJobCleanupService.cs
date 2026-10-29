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
    private readonly IProcessingJobStore jobStore;
    private readonly JobLifecycle lifecycle;
    private readonly IDirectoryProvider directoryProvider;
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
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ProcessingJobCleanupService> logger,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.jobStore = jobStore;
        this.lifecycle = lifecycle;
        this.directoryProvider = directoryProvider;
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

            // The in-memory job entry ages out on JobRetention, counted from the end of its run like the downloads,
            // so retiring a job never cuts their retention short; a job that has not finished counts from its
            // creation, which is how a stuck one shows up below. Its upload is not released here: it went when
            // the job ended, or it stays on purpose until its upload session expires (a cancelled run, see
            // JobLifecycle.Decide). The asset directory is the long-term archive of deliveries, which survive
            // cleanup and are only removed via DeliveryController.Delete; a job directory there without a
            // delivery is a declaration that failed after the promotion, and goes. A staged payload (the staging
            // root below the asset directory) belongs to a deliverable run that was never delivered: once the
            // in-memory job is gone nobody can declare its delivery any more, so it retires with the job.
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

                var hasDelivery = dbContext.Deliveries.Any(d => d.JobId == jobId);
                if (RetireJob(jobId, hasDelivery))
                    retiredJobs++;
            }

            var purgedRuns = await PurgeExpiredProtocolRecordsAsync(dbContext, now);

            logger.LogInformation("ProcessingJobCleanupService completed. Retired jobs: {Retired}, expired download dirs: {Downloads}, expired visualization dirs: {Visualizations}, purged protocol runs: {PurgedRuns}.", retiredJobs, cleanedDownloads, cleanedVisualizations, purgedRuns);
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
        return await dbContext.PipelineRuns
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

    private bool RetireJob(Guid jobId, bool hasSubmittedDelivery)
    {
        try
        {
            DeleteIfExists(directoryProvider.GetDownloadDirectoryPath(jobId));
            DeleteIfExists(directoryProvider.GetVisualizationDirectoryPath(jobId));
            DeleteIfExists(directoryProvider.GetPipelineDirectoryPath(jobId));
            DeleteIfExists(directoryProvider.GetAssetStagingDirectoryPath(jobId));
            if (!hasSubmittedDelivery)
                DeleteIfExists(directoryProvider.GetAssetDirectoryPath(jobId));
            lifecycle.Retire(jobId);
            logger.LogTrace("Retired job <{JobId}>. Removed asset directory: {RemovedAssets}.", jobId, !hasSubmittedDelivery);
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
}
