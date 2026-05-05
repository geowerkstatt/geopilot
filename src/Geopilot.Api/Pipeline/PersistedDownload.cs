namespace Geopilot.Api.Pipeline;

/// <summary>
/// Tracking record for a file produced by a pipeline step that has been persisted to disk
/// for download (or delivery).
/// </summary>
/// <param name="OriginalFileName">The human-readable file name reported by the producing process.</param>
/// <param name="PersistedFileName">The file name on disk in the job's upload directory.</param>
public record PersistedDownload(string OriginalFileName, string PersistedFileName);
