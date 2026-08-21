namespace Geopilot.Api;

/// <summary>
/// Settings of the cloud upload backend: the Azure Blob Storage connection and the
/// deployment surface only a browser-to-storage upload needs (CORS, CSP endpoint).
/// </summary>
public class UploadCloudOptions
{
    /// <summary>
    /// Gets the name of the configuration section that contains the cloud backend settings.
    /// </summary>
    public static string SectionName => "Upload:Cloud";

    /// <summary>
    /// The Azure Blob Storage connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of the storage bucket.
    /// </summary>
    public string? BucketName { get; set; }

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
