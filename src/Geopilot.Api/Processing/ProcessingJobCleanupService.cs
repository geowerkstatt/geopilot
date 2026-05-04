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
    private readonly ILogger<ProcessingJobCleanupService> logger;
    private readonly ProcessingOptions processingOptions;
    private readonly SemaphoreSlim cleanupSemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingJobCleanupService"/> class.
    /// </summary>
    public ProcessingJobCleanupService(
        IProcessingJobStore jobStore,
        IDirectoryProvider directoryProvider,
        ILogger<ProcessingJobCleanupService> logger,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.jobStore = jobStore;
        this.directoryProvider = directoryProvider;
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
            var now = DateTime.UtcNow;
            int deletedJobs = 0;
            var uploadRoot = directoryProvider.UploadDirectory;

            foreach (var dir in Directory.GetDirectories(uploadRoot))
            {
                var folderName = Path.GetFileName(dir);

                if (!Guid.TryParse(folderName, out var jobId))
                    continue;

                var job = jobStore.GetJob(jobId);
                var jobAge = now - job?.CreatedAt;

                if (job == null || jobAge > processingOptions.JobRetention)
                {
                    if (DeleteJob(jobId))
                        deletedJobs++;
                }
            }

            logger.LogInformation("ProcessingJobCleanupService completed. Deleted jobs: {Deleted}.", deletedJobs);
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

    private bool DeleteJob(Guid jobId)
    {
        try
        {
            var uploadDir = directoryProvider.GetUploadDirectoryPath(jobId);
            Directory.Delete(uploadDir, true);
            jobStore.RemoveJob(jobId);
            logger.LogTrace("Deleted job <{JobId}>.", jobId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting job <{JobId}>.", jobId);
        }

        return false;
    }
}
