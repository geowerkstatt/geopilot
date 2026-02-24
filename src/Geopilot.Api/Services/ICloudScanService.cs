namespace Geopilot.Api.Services;

/// <summary>
/// Provides malware scanning for files in cloud storage.
/// </summary>
public interface ICloudScanService
{
    /// <summary>
    /// Scans the specified cloud storage keys for threats.
    /// </summary>
    /// <param name="keys">The storage keys of files to scan.</param>
    /// <returns>The scan result indicating whether files are clean.</returns>
    Task<ScanResult> CheckFilesAsync(IReadOnlyList<string> keys);
}

/// <summary>
/// Result of a malware scan.
/// </summary>
public record ScanResult(bool IsClean, string? ThreatDetails = null);
