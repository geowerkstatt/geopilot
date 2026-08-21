namespace Geopilot.Api.Services;

/// <summary>
/// Provides malware scanning for files in the upload storage.
/// </summary>
public interface IUploadScanService
{
    /// <summary>
    /// Scans the specified storage keys for threats.
    /// </summary>
    /// <param name="keys">The storage keys of files to scan.</param>
    /// <returns>The scan result indicating whether files are clean.</returns>
    Task<ScanResult> CheckFilesAsync(IReadOnlyList<string> keys);
}

/// <summary>
/// Result of a malware scan.
/// </summary>
public record ScanResult(bool IsClean, string? ThreatDetails = null);
