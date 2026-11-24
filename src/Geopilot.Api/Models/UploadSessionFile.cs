namespace Geopilot.Api.Models;

/// <summary>
/// One file registered for an <see cref="UploadSession"/>.
/// </summary>
public class UploadSessionFile
{
    /// <summary>
    /// The unique identifier of the file record. Ascending in the order the files were registered.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The upload session this file belongs to.
    /// </summary>
    public Guid UploadSessionId { get; set; }

    /// <summary>
    /// The original file name as announced by the client.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The key the file is stored under in the upload storage.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>
    /// The file size in bytes as declared by the client, or as stored when the server received the file itself.
    /// </summary>
    public long DeclaredSize { get; set; }

    /// <summary>
    /// The content type the client sent with the file, if any.
    /// </summary>
    public string? ContentType { get; set; }
}
