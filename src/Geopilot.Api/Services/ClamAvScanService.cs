using Microsoft.Extensions.Options;
using nClam;

namespace Geopilot.Api.Services;

/// <summary>
/// Scans files via ClamAV using the clamd INSTREAM protocol.
/// Streams content directly from cloud storage to clamd without buffering in the API.
/// See https://docs.clamav.net/manual/Usage/Scanning.html#clamd for protocol details.
/// </summary>
public class ClamAvScanService : ICloudScanService
{
    private readonly ICloudStorageService cloudStorageService;
    private readonly ClamAvOptions options;
    private readonly ILogger<ClamAvScanService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClamAvScanService"/> class.
    /// </summary>
    public ClamAvScanService(ICloudStorageService cloudStorageService, IOptions<ClamAvOptions> options, ILogger<ClamAvScanService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.cloudStorageService = cloudStorageService;
        this.logger = logger;

        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Host))
            throw new InvalidOperationException("ClamAV:Host is not configured.");
        if (config.Port <= 0 || config.Port > 65535)
            throw new InvalidOperationException("ClamAV:Port must be between 1 and 65535.");

        this.options = config;
    }

    /// <inheritdoc/>
    public async Task<ScanResult> CheckFilesAsync(IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
            return new ScanResult(true);

        var threats = new List<string>();

        foreach (var key in keys)
        {
            var threat = await ScanSingleFileAsync(key);
            if (threat != null)
                threats.Add(threat);
        }

        return threats.Count == 0
            ? new ScanResult(true)
            : new ScanResult(false, string.Join("; ", threats));
    }

    private async Task<string?> ScanSingleFileAsync(string key)
    {
        using var fileStream = new MemoryStream();
        await cloudStorageService.DownloadAsync(key, fileStream);
        fileStream.Position = 0;

        var clam = new ClamClient(options.Host, options.Port);
        var result = await clam.SendAndScanFileAsync(fileStream);

        logger.LogDebug("ClamAV scan for {Key}: {Result} (raw: {RawResult})", key, result.Result, result.RawResult);

        return ToThreatDescription(key, result);
    }

    /// <summary>
    /// Maps a ClamAV scan result to a threat description string.
    /// Returns <c>null</c> if the file is clean, or a description like "uploads/file.xtf: Win.Test.EICAR_HDB-1" if infected.
    /// See https://docs.clamav.net/manual/Usage/Scanning.html#clamd for response format details.
    /// </summary>
    private static string? ToThreatDescription(string key, ClamScanResult result)
    {
        return result.Result switch
        {
            ClamScanResults.Clean => null,
            ClamScanResults.VirusDetected => $"{key}: {string.Join(", ", result.InfectedFiles?.Select(f => f.VirusName) ?? [])}",
            _ => $"{key}: scan error — {result.RawResult}",
        };
    }
}
