namespace Geopilot.Api;

/// <summary>
/// Configuration options for running and managing processing jobs.
/// </summary>
public class ProcessingOptions
{
    /// <summary>
    /// The duration after which uploads, deliveries, and the in-memory job entry are
    /// eligible for cleanup.
    /// </summary>
    public TimeSpan JobRetention { get; set; }

    /// <summary>
    /// The duration after which the user-facing pipeline downloads (logs, reports) are
    /// eligible for cleanup. Typically much shorter than <see cref="JobRetention"/>
    /// since downloads are only useful while the user is interacting with the result.
    /// </summary>
    public TimeSpan DownloadRetention { get; set; }

    /// <summary>
    /// The duration after which visualization configs are eligible for cleanup. Typically the
    /// shortest retention, since a visualization is only needed while the user views the job
    /// result in the browser.
    /// </summary>
    public TimeSpan VisualizationRetention { get; set; }

    /// <summary>
    /// The interval at which the cleanup service runs to remove old processing jobs.
    /// </summary>
    public TimeSpan JobCleanupInterval { get; set; }

    /// <summary>
    /// The duration after which a job should time out if it has not completed.
    /// </summary>
    public required TimeSpan JobTimeout { get; set; }
}
