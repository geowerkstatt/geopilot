using Geopilot.Api.Validation;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// A background service that periodically cleans up old cloud upload files.
/// </summary>
public class CloudCleanupService : BackgroundService
{
    private readonly ICloudStorageService cloudStorageService;
    private readonly IValidationJobStore jobStore;
    private readonly ILogger<CloudCleanupService> logger;
    private readonly CloudStorageOptions options;
    private readonly SemaphoreSlim cleanupSemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudCleanupService"/> class.
    /// </summary>
    public CloudCleanupService(
        ICloudStorageService cloudStorageService,
        IValidationJobStore jobStore,
        ILogger<CloudCleanupService> logger,
        IOptions<CloudStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.cloudStorageService = cloudStorageService;
        this.jobStore = jobStore;
        this.logger = logger;
        this.options = options.Value;
    }

    /// <summary>
    /// Executes the background cleanup service, periodically cleaning up old cloud upload files.
    /// </summary>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that is used to signal the operation should stop.</param>
    /// <returns>A task that represents the asynchronous execution of the cleanup service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.CleanupIntervalMinutes);
        logger.LogInformation("CloudCleanupService started. Cleanup interval: {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync();
            await Task.Delay(interval, stoppingToken);
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
    /// Performs the cleanup of old cloud upload files.
    /// </summary>
    public async Task RunCleanupAsync()
    {
        if (!cleanupSemaphore.Wait(0))
        {
            logger.LogWarning("Cloud cleanup is already running. Skipping this run.");
            return;
        }

        try
        {
            var maxAge = TimeSpan.FromHours(options.CleanupAgeHours);
            var cutoff = DateTime.UtcNow - maxAge;
            var maxFileSizeBytes = (long)options.MaxFileSizeMB * 1024 * 1024;
            int deletedPrefixes = 0;

            var uploadFiles = await cloudStorageService.ListFilesAsync("uploads/");

            var filesByJobId = uploadFiles
                .GroupBy(f => ExtractJobId(f.Key))
                .ToList();

            // Delete blobs with invalid paths (no valid GUID)
            foreach (var file in filesByJobId.Where(g => g.Key == null).SelectMany(g => g))
            {
                await cloudStorageService.DeleteAsync(file.Key);
                logger.LogTrace("Deleted invalid blob: {Key}.", file.Key);
            }

            foreach (var group in filesByJobId.Where(g => g.Key != null))
            {
                var jobId = group.Key!.Value;

                if (group.Any(f => f.LastModified < cutoff))
                {
                    await cloudStorageService.DeletePrefixAsync($"uploads/{jobId}/");
                    jobStore.RemoveJob(jobId);
                    deletedPrefixes++;
                    logger.LogTrace("Deleted stale cloud files for job <{JobId}>.", jobId);
                    continue;
                }

                if (group.Any(f => f.Size > maxFileSizeBytes))
                {
                    await cloudStorageService.DeletePrefixAsync($"uploads/{jobId}/");
                    jobStore.RemoveJob(jobId);
                    deletedPrefixes++;
                    logger.LogTrace("Deleted oversized cloud files for job <{JobId}>.", jobId);
                    continue;
                }

                if (jobStore.GetJob(jobId) == null)
                {
                    await cloudStorageService.DeletePrefixAsync($"uploads/{jobId}/");
                    deletedPrefixes++;
                    logger.LogTrace("Deleted orphaned cloud files for job <{JobId}>.", jobId);
                }
            }

            // Delete blobs outside the uploads/ prefix
            var allFiles = await cloudStorageService.ListFilesAsync(string.Empty);
            foreach (var file in allFiles.Where(f => !f.Key.StartsWith("uploads/", StringComparison.Ordinal)))
            {
                await cloudStorageService.DeleteAsync(file.Key);
                logger.LogTrace("Deleted blob outside uploads/ prefix: {Key}.", file.Key);
            }

            logger.LogInformation("CloudCleanupService completed. Deleted prefixes: {Deleted}.", deletedPrefixes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cloud cleanup.");
        }
        finally
        {
            cleanupSemaphore.Release();
        }
    }

    private static Guid? ExtractJobId(string key)
    {
        // Expected format: "uploads/{jobId}/filename"
        var parts = key.Split('/');
        if (parts.Length >= 2 && Guid.TryParse(parts[1], out var jobId))
            return jobId;

        return null;
    }
}
