namespace Geopilot.Api.Contracts;

/// <summary>
/// Response containing presigned upload URLs for a upload session.
/// </summary>
public record InitiateUploadResponse(Guid UploadId, IReadOnlyList<FileUploadInfo> Files, DateTime ExpiresAt);

/// <summary>
/// Upload information for a single file, including the presigned URL.
/// </summary>
/// <param name="FileName">The name of the file.</param>
/// <param name="UploadUrl">The URL to PUT the file content to.</param>
/// <param name="UploadHeaders">The headers to send with the PUT, in addition to the content type.</param>
public record FileUploadInfo(string FileName, string UploadUrl, IReadOnlyDictionary<string, string> UploadHeaders);
