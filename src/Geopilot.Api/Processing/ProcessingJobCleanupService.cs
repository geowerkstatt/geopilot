using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Processing;

/// <summary>
/// Background service that periodically cleans up old or orphaned processing jobs and their associated files.
/// </summary>
public class ProcessingJobCleanupService : BackgroundService
{
    private readonly IProcessingJobStore jobStore;
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
        IDirectoryProvider directoryProvider,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ProcessingJobCleanupService> logger,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.jobStore = jobStore;
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
            RunCleanup();
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
    public void RunCleanup()
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
                if (job == null || now - job.CreatedAt > processingOptions.DownloadRetention)
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
                if (job == null || now - job.CreatedAt > processingOptions.VisualizationRetention)
                {
                    if (DeleteIfExists(directoryProvider.GetVisualizationDirectoryPath(jobId)))
                        cleanedVisualizations++;
                }
            }

            // Uploads + the in-memory job entry age out on JobRetention. The asset directory
            // is the long-term archive; for a job whose run was never submitted as a delivery
            // we wipe its asset directory too so dead data doesn't accumulate. Submitted
            // deliveries survive cleanup and are only removed via DeliveryController.Delete.
            var retiredCandidates = EnumerateJobIds(directoryProvider.UploadDirectory);
            retiredCandidates.UnionWith(EnumerateJobIds(directoryProvider.AssetDirectory));
            foreach (var jobId in retiredCandidates)
            {
                var job = jobStore.GetJob(jobId);
                if (job != null && now - job.CreatedAt <= processingOptions.JobRetention)
                    continue;

                var hasDelivery = dbContext.Deliveries.Any(d => d.JobId == jobId);
                if (RetireJob(jobId, hasDelivery))
                    retiredJobs++;
            }

            logger.LogInformation("ProcessingJobCleanupService completed. Retired jobs: {Retired}, expired download dirs: {Downloads}, expired visualization dirs: {Visualizations}.", retiredJobs, cleanedDownloads, cleanedVisualizations);
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
            DeleteIfExists(directoryProvider.GetUploadDirectoryPath(jobId));
            DeleteIfExists(directoryProvider.GetDownloadDirectoryPath(jobId));
            DeleteIfExists(directoryProvider.GetVisualizationDirectoryPath(jobId));
            if (!hasSubmittedDelivery)
                DeleteIfExists(directoryProvider.GetAssetDirectoryPath(jobId));
            jobStore.RemoveJob(jobId);
            logger.LogTrace("Retired job <{JobId}>. Removed asset directory: {RemovedAssets}.", jobId, !hasSubmittedDelivery);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retiring job <{JobId}>.", jobId);
        }

        return false;
    }

    private static bool DeleteIfExists(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        Directory.Delete(directory, recursive: true);
        return true;
    }
}
