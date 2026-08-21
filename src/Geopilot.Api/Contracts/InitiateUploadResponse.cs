namespace Geopilot.Api.Contracts;

/// <summary>
/// Response containing presigned upload URLs for a upload session.
/// </summary>
public record InitiateUploadResponse(Guid UploadId, IReadOnlyList<FileUploadInfo> Files, DateTime ExpiresAt);

/// <summary>
/// Upload information for a single file, including the presigned URL.
/// </summary>
public record FileUploadInfo(string FileName, string UploadUrl);
