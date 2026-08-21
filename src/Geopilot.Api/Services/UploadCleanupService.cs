using Geopilot.Api.Processing;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// A background service that periodically cleans up stale upload files.
/// </summary>
public class UploadCleanupService : BackgroundService
{
    private readonly IUploadStorage uploadStorage;
    private readonly IUploadStore uploadStore;
    private readonly ILogger<UploadCleanupService> logger;
    private readonly CloudStorageOptions options;
    private readonly ProcessingOptions processingOptions;
    private readonly SemaphoreSlim cleanupSemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadCleanupService"/> class.
    /// </summary>
    public UploadCleanupService(
        IUploadStorage uploadStorage,
        IUploadStore uploadStore,
        ILogger<UploadCleanupService> logger,
        IOptions<CloudStorageOptions> options,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.uploadStorage = uploadStorage;
        this.uploadStore = uploadStore;
        this.logger = logger;
        this.options = options.Value;
        this.processingOptions = processingOptions.Value;
    }

    /// <summary>
    /// Executes the background cleanup service, periodically cleaning up stale upload files.
    /// </summary>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that is used to signal the operation should stop.</param>
    /// <returns>A task that represents the asynchronous execution of the cleanup service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.CleanupIntervalMinutes);
        logger.LogInformation("UploadCleanupService started. Cleanup interval: {Interval}.", interval);

        // Uploaded files are fetched only when a step reads them, so their blobs have to outlive the job
        // that may still need them. This sweep would otherwise pull them away mid-job.
        var maxAge = TimeSpan.FromHours(options.CleanupAgeHours);
        var longestJobLifetime = processingOptions.JobRetention + processingOptions.JobTimeout;
        if (maxAge <= longestJobLifetime)
        {
            logger.LogWarning(
                "CloudStorage:CleanupAgeHours ({CleanupAge}) does not cover the longest job lifetime ({JobLifetime} = Processing:JobRetention + Processing:JobTimeout). Uploaded files may be deleted while a job or its delivery still needs them.",
                maxAge,
                longestJobLifetime);
        }

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
    /// Performs the cleanup of stale upload files.
    /// </summary>
    public async Task RunCleanupAsync()
    {
        if (!cleanupSemaphore.Wait(0))
        {
            logger.LogWarning("Upload cleanup is already running. Skipping this run.");
            return;
        }

        try
        {
            var maxAge = TimeSpan.FromHours(options.CleanupAgeHours);
            var cutoff = DateTime.UtcNow - maxAge;
            var maxFileSizeBytes = (long)options.MaxFileSizeMB * 1024 * 1024;
            int deletedPrefixes = 0;

            var uploadFiles = await uploadStorage.ListFilesAsync("uploads/");

            var filesByUploadId = uploadFiles
                .GroupBy(f => ExtractUploadId(f.Key))
                .ToList();

            // Delete blobs with invalid paths (no valid GUID)
            foreach (var file in filesByUploadId.Where(g => g.Key == null).SelectMany(g => g))
            {
                await uploadStorage.DeleteAsync(file.Key);
                logger.LogTrace("Deleted invalid blob: {Key}.", file.Key);
            }

            foreach (var group in filesByUploadId.Where(g => g.Key != null))
            {
                var uploadId = group.Key!.Value;

                if (group.Any(f => f.LastModified < cutoff))
                {
                    await uploadStorage.DeletePrefixAsync($"uploads/{uploadId}/");
                    uploadStore.RemoveUpload(uploadId);
                    deletedPrefixes++;
                    logger.LogTrace("Deleted stale files of upload <{UploadId}>.", uploadId);
                    continue;
                }

                if (group.Any(f => f.Size > maxFileSizeBytes))
                {
                    await uploadStorage.DeletePrefixAsync($"uploads/{uploadId}/");
                    uploadStore.RemoveUpload(uploadId);
                    deletedPrefixes++;
                    logger.LogTrace("Deleted oversized files of upload <{UploadId}>.", uploadId);
                    continue;
                }

                if (uploadStore.GetUpload(uploadId) == null)
                {
                    await uploadStorage.DeletePrefixAsync($"uploads/{uploadId}/");
                    deletedPrefixes++;
                    logger.LogTrace("Deleted orphaned files of upload <{UploadId}>.", uploadId);
                }
            }

            // Delete blobs outside the uploads/ prefix
            var allFiles = await uploadStorage.ListFilesAsync(string.Empty);
            foreach (var file in allFiles.Where(f => !f.Key.StartsWith("uploads/", StringComparison.Ordinal)))
            {
                await uploadStorage.DeleteAsync(file.Key);
                logger.LogTrace("Deleted blob outside uploads/ prefix: {Key}.", file.Key);
            }

            logger.LogInformation("UploadCleanupService completed. Deleted prefixes: {Deleted}.", deletedPrefixes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during upload cleanup.");
        }
        finally
        {
            cleanupSemaphore.Release();
        }
    }

    private static Guid? ExtractUploadId(string key)
    {
        // Expected format: "uploads/{uploadId}/filename"
        var parts = key.Split('/');
        if (parts.Length >= 2 && Guid.TryParse(parts[1], out var uploadId))
            return uploadId;

        return null;
    }
}
