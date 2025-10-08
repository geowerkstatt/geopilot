﻿using Geopilot.Api.FileAccess;

namespace Geopilot.Api.Validation;

/// <summary>
/// A background service that periodically cleans up old or orphaned validation jobs and their associated files.
/// </summary>
public class ValidationJobCleanupService : BackgroundService
{
    private readonly IValidationJobStore jobStore;
    private readonly IDirectoryProvider directoryProvider;
    private readonly ILogger<ValidationJobCleanupService> logger;
    private readonly TimeSpan jobRetention;
    private readonly TimeSpan cleanupInterval;
    private readonly object cleanupLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationJobCleanupService"/> class.
    /// </summary>
    public ValidationJobCleanupService(
        IValidationJobStore jobStore,
        IDirectoryProvider directoryProvider,
        ILogger<ValidationJobCleanupService> logger,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        this.jobStore = jobStore;
        this.directoryProvider = directoryProvider;
        this.logger = logger;

        var section = configuration.GetSection("Validation");
        jobRetention = TimeSpan.FromHours(section.GetValue<double>("JobRetentionHours", 24));
        cleanupInterval = TimeSpan.FromHours(section.GetValue<double>("CleanupIntervalHours", 24));
    }

    /// <summary>
    /// Executes the background cleanup service, periodically performing cleanup operations.
    /// </summary>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that is used to signal the operation should stop.</param>
    /// <returns>A task that represents the asynchronous execution of the cleanup service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ValidationJobCleanupService started. Cleanup interval: {Interval} hours.", cleanupInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            RunCleanup();
            await Task.Delay(cleanupInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Performs the cleanup of old or orphaned validation jobs and their associated files.
    /// </summary>
    public void RunCleanup()
    {
        try
        {
            if (!Monitor.TryEnter(cleanupLock))
            {
                logger.LogWarning("Validation job cleanup is already running. Skipping this run.");
                return;
            }

            var now = DateTime.UtcNow;
            int deletedJobs = 0;
            var uploadRoot = directoryProvider.UploadDirectory;

            foreach (var dir in Directory.GetDirectories(uploadRoot))
            {
                var folderName = Path.GetFileName(dir);

                // Only process folders with GUID names
                if (!Guid.TryParse(folderName, out var jobId))
                    continue;

                var job = jobStore.GetJob(jobId);
                var jobAge = now - job?.CreatedOn;

                // Delete orphaned job folders or jobs older than the retention period
                if (job == null || jobAge > jobRetention)
                {
                    if (DeleteJob(jobId))
                        deletedJobs++;
                }
            }

            logger.LogInformation("ValidationJobCleanupService completed. Deleted jobs: {Deleted}.", deletedJobs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during validation job cleanup.");
        }
        finally
        {
            Monitor.Exit(cleanupLock);
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
