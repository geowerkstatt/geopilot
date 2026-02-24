namespace Geopilot.Api.Enums;

/// <summary>
/// The method used to upload files for a validation job.
/// </summary>
public enum UploadMethod
{
    /// <summary>
    /// Files are uploaded directly to the API.
    /// </summary>
    Direct,

    /// <summary>
    /// Files are uploaded to cloud storage via presigned URLs.
    /// </summary>
    Cloud,
}
