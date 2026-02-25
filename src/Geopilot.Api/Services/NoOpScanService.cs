namespace Geopilot.Api.Services;

/// <summary>
/// A no-op scan service that skips virus scanning. Used when ClamAV is disabled.
/// </summary>
public class NoOpScanService : ICloudScanService
{
    private readonly ILogger<NoOpScanService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoOpScanService"/> class.
    /// </summary>
    public NoOpScanService(ILogger<NoOpScanService> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public Task<ScanResult> CheckFilesAsync(IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        logger.LogWarning("Virus scanning is disabled. Skipping scan for {FileCount} file(s).", keys.Count);
        return Task.FromResult(new ScanResult(true));
    }
}
