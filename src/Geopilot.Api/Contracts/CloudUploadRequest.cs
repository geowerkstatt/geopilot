using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Request to initiate a cloud upload session.
/// </summary>
public class CloudUploadRequest
{
    /// <summary>
    /// The files to upload.
    /// </summary>
    [Required]
    public required IReadOnlyList<FileMetadata> Files { get; set; }
}

/// <summary>
/// Metadata about a file to be uploaded.
/// </summary>
public record FileMetadata(string FileName, long Size);
