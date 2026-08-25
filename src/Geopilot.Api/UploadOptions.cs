using Geopilot.Api.Enums;

namespace Geopilot.Api;

/// <summary>
/// Backend-independent upload policy: size and count limits, cleanup cadence and rate limiting.
/// Backend-specific settings live in <see cref="UploadCloudOptions"/>.
/// </summary>
public class UploadOptions
{
    /// <summary>
    /// Gets the name of the configuration section that contains the upload settings.
    /// </summary>
    public static string SectionName => "Upload";

    /// <summary>
    /// The storage backend uploaded files are written to. Selects which backend section
    /// (<see cref="UploadCloudOptions"/> or <see cref="UploadDirectOptions"/>) is read and validated.
    /// </summary>
    public UploadBackend Backend { get; set; } = UploadBackend.Cloud;

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
    /// The maximum total size of all active uploads in megabytes.
    /// </summary>
    public int MaxGlobalActiveSizeMB { get; set; }

    /// <summary>
    /// The maximum number of active uploads.
    /// </summary>
    public int MaxActiveJobs { get; set; }

    /// <summary>
    /// How long the upload URLs issued for an upload session stay valid, in minutes.
    /// </summary>
    public int UploadUrlExpiryMinutes { get; set; }

    /// <summary>
    /// The age in hours after which uploaded files are eligible for cleanup.
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
}
