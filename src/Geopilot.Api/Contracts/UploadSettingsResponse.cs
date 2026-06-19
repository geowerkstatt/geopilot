namespace Geopilot.Api.Contracts;

/// <summary>
/// The upload settings response schema.
/// </summary>
public record UploadSettingsResponse(int MaxFileSizeMB, int MaxFilesPerJob, int MaxJobSizeMB);
