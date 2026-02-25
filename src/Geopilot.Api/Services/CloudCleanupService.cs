using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// A background service that periodically cleans up old cloud upload files.
/// </summary>
public class CloudCleanupService : BackgroundService
{
    private readonly ICloudStorageService cloudStorageService;
    private readonly ILogger<CloudCleanupService> logger;
    private readonly CloudStorageOptions options;
    private readonly SemaphoreSlim cleanupSemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudCleanupService"/> class.
    /// </summary>
    public CloudCleanupService(
        ICloudStorageService cloudStorageService,
        ILogger<CloudCleanupService> logger,
        IOptions<CloudStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.cloudStorageService = cloudStorageService;
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
        var interval = TimeSpan.FromHours(options.CleanupAgeHours / 2.0);
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
            int deletedPrefixes = 0;

            var allFiles = await cloudStorageService.ListFilesAsync("uploads/");

            var staleJobIds = allFiles
                .Where(f => f.LastModified < cutoff)
                .Select(f => ExtractJobId(f.Key))
                .Where(id => id != null)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            foreach (var jobId in staleJobIds)
            {
                await cloudStorageService.DeletePrefixAsync($"uploads/{jobId}/");
                deletedPrefixes++;
                logger.LogTrace("Deleted cloud files for job <{JobId}>.", jobId);
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
