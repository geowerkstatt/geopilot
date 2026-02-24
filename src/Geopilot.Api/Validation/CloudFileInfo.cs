namespace Geopilot.Api.Validation;

/// <summary>
/// Metadata about a file uploaded to cloud storage.
/// </summary>
public record CloudFileInfo(string FileName, string CloudKey, long ExpectedSize, string? ContentType = null);
