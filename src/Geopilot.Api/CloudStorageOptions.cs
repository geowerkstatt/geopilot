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
    /// The maximum file size in megabytes.
    /// </summary>
    public int MaxFileSizeMB { get; set; }

    /// <summary>
    /// The maximum number of files per job.
    /// </summary>
    public int MaxFilesPerJob { get; set; }

    /// <summary>
    /// The maximum total job size in megabytes.
    /// </summary>
    public int MaxJobSizeMB { get; set; }

    /// <summary>
    /// The maximum total size of all active cloud uploads in megabytes.
    /// </summary>
    public int MaxGlobalActiveSizeMB { get; set; }

    /// <summary>
    /// The maximum number of active cloud upload jobs.
    /// </summary>
    public int MaxActiveJobs { get; set; }

    /// <summary>
    /// The expiry time for presigned upload URLs in minutes.
    /// </summary>
    public int PresignedUrlExpiryMinutes { get; set; }

    /// <summary>
    /// The age in hours after which cloud files are eligible for cleanup.
    /// </summary>
    public int CleanupAgeHours { get; set; }

    /// <summary>
    /// The interval in minutes between cleanup runs.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; }

    /// <summary>
    /// Maximum number of upload initiation requests per IP within the rate limit window.
    /// </summary>
    public int RateLimitRequests { get; set; }

    /// <summary>
    /// Rate limit window duration in minutes.
    /// </summary>
    public int RateLimitWindowMinutes { get; set; }

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
