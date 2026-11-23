namespace Geopilot.Api.Enums;

/// <summary>
/// The storage backend uploaded files are written to.
/// </summary>
public enum UploadBackend
{
    /// <summary>
    /// Files are uploaded to an object storage via presigned URLs.
    /// </summary>
    Cloud,

    /// <summary>
    /// Files are uploaded through the API into a local directory.
    /// </summary>
    Direct,
}
