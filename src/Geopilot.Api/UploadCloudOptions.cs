using Geopilot.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api;

/// <summary>
/// Settings of the cloud upload backend: the object storage provider, the bucket and the
/// deployment surface only a browser-to-storage upload needs (CORS, CSP endpoint).
/// </summary>
public class UploadCloudOptions
{
    /// <summary>
    /// Gets the name of the configuration section that contains the cloud backend settings.
    /// </summary>
    public static string SectionName => "Upload:Cloud";

    /// <summary>
    /// The object storage service. The S3 provider reads its connection from <see cref="UploadS3Options"/>.
    /// </summary>
    public CloudStorageProvider Provider { get; set; } = CloudStorageProvider.AzureBlob;

    /// <summary>
    /// The Azure Blob Storage connection string. Required for <see cref="CloudStorageProvider.AzureBlob"/>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of the storage bucket.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string BucketName { get; set; }

    /// <summary>
    /// The public-facing origin of the blob storage endpoint (e.g., "https://localhost:10000").
    /// Added to the Content-Security-Policy connect-src directive to allow browser-based uploads via presigned URLs.
    /// </summary>
    public string? BlobEndpoint { get; set; }

    /// <summary>
    /// Whether to automatically create the blob container on startup. Should only be enabled in development.
    /// </summary>
    public bool AutoCreateContainer { get; set; }

    /// <summary>
    /// Allowed origins for CORS on the blob storage service. When set, configures CORS rules on the
    /// storage account at startup to allow browser-based uploads via presigned URLs.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = [];
}
