namespace Geopilot.Api.Enums;

/// <summary>
/// The object storage service of the cloud upload backend.
/// </summary>
public enum CloudStorageProvider
{
    /// <summary>
    /// Azure Blob Storage, configured with <see cref="UploadCloudOptions.ConnectionString"/>.
    /// </summary>
    AzureBlob,

    /// <summary>
    /// An S3-compatible object storage, configured in <see cref="UploadS3Options"/>.
    /// </summary>
    S3,
}
