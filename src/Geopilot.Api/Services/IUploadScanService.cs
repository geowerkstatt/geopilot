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
/// <param name="IsClean">Whether no threat was found. Also <see langword="true"/> when scanning is disabled; check <paramref name="Scanned"/> to tell the two apart.</param>
/// <param name="ThreatDetails">Details of the detected threats, when there are any.</param>
/// <param name="Scanned">Whether a scan actually ran. <see langword="false"/> when scanning is disabled, so a skipped scan is never recorded as clean.</param>
/// <param name="Hashes">The SHA-256 of each scanned file as lowercase hex, keyed by storage key. Computed on the scan stream, so it costs no extra read. <see langword="null"/> when no scan ran.</param>
public record ScanResult(bool IsClean, string? ThreatDetails = null, bool Scanned = true, IReadOnlyDictionary<string, string>? Hashes = null);
