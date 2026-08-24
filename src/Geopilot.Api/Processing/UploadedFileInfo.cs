namespace Geopilot.Api.Processing;

/// <summary>
/// Metadata about a file uploaded to the upload storage.
/// </summary>
public record UploadedFileInfo(string FileName, string StorageKey, long ExpectedSize, string? ContentType = null);
