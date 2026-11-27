namespace Geopilot.Api.Models;

/// <summary>
/// An initiated upload that no job has released yet: the files a client announced and may upload to the
/// upload storage under the upload's id. Kept in the database, so every instance sees it and it survives a restart.
/// </summary>
public class UploadSession
{
    /// <summary>
    /// The id of the upload, as returned on initiation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// When the upload was initiated, in UTC.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The files registered on initiation, in the order they were registered.
    /// </summary>
    public List<UploadSessionFile> Files { get; set; } = new();
}
