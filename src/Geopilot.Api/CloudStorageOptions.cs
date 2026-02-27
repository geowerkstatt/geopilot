namespace Geopilot.Api;

/// <summary>
/// Configuration options for cloud storage uploads.
/// </summary>
public class CloudStorageOptions
{
    /// <summary>
    /// Whether cloud storage uploads are enabled. When disabled, only direct uploads are available.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The Azure Blob Storage connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of the cloud storage bucket.
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// The maximum file size in megabytes. Default is 2048 MB (2 GB).
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 2048;

    /// <summary>
    /// The maximum number of files per job. Default is 50.
    /// </summary>
    public int MaxFilesPerJob { get; set; } = 50;

    /// <summary>
    /// The maximum total job size in megabytes. Default is 10240 MB (10 GB).
    /// </summary>
    public int MaxJobSizeMB { get; set; } = 10240;

    /// <summary>
    /// The maximum total size of all active cloud uploads in megabytes. Default is 204800 MB (200 GB).
    /// </summary>
    public int MaxGlobalActiveSizeMB { get; set; } = 204800;

    /// <summary>
    /// The maximum number of active cloud upload jobs. Default is 100.
    /// </summary>
    public int MaxActiveJobs { get; set; } = 100;

    /// <summary>
    /// The expiry time for presigned upload URLs in minutes. Default is 60 minutes.
    /// </summary>
    public int PresignedUrlExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// The age in hours after which cloud files are eligible for cleanup. Default is 48 hours.
    /// </summary>
    public int CleanupAgeHours { get; set; } = 48;

    /// <summary>
    /// The interval in minutes between cleanup runs. Default is 15 minutes.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of upload initiation requests per IP within the rate limit window. Default is 10.
    /// </summary>
    public int RateLimitRequests { get; set; } = 10;

    /// <summary>
    /// Rate limit window duration in minutes. Default is 1 minute.
    /// </summary>
    public int RateLimitWindowMinutes { get; set; } = 1;

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
