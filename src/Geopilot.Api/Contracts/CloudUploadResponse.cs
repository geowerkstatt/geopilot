namespace Geopilot.Api.Contracts;

/// <summary>
/// Response containing presigned upload URLs for a cloud upload session.
/// </summary>
public record CloudUploadResponse(Guid JobId, IReadOnlyList<FileUploadInfo> Files, DateTime ExpiresAt);

/// <summary>
/// Upload information for a single file, including the presigned URL.
/// </summary>
public record FileUploadInfo(string FileName, string UploadUrl);
