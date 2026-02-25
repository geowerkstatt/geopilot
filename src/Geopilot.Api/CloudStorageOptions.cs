namespace Geopilot.Api;

/// <summary>
/// Configuration options for cloud storage uploads.
/// </summary>
public class CloudStorageOptions
{
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
    /// The expiry time for presigned upload URLs in minutes. Default is 60 minutes.
    /// </summary>
    public int PresignedUrlExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// The age in hours after which cloud files are eligible for cleanup. Default is 48 hours.
    /// </summary>
    public int CleanupAgeHours { get; set; } = 48;

    /// <summary>
    /// Whether to automatically create the blob container on startup. Should only be enabled in development.
    /// </summary>
    public bool AutoCreateContainer { get; set; }
}
