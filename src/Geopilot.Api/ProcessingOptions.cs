namespace Geopilot.Api;

/// <summary>
/// Configuration options for running and managing processing jobs.
/// </summary>
public class ProcessingOptions
{
    /// <summary>
    /// The duration after which processing jobs are eligible for cleanup.
    /// </summary>
    public TimeSpan JobRetention { get; set; }

    /// <summary>
    /// The interval at which the cleanup service runs to remove old processing jobs.
    /// </summary>
    public TimeSpan JobCleanupInterval { get; set; }

    /// <summary>
    /// The duration after which a job should time out if it has not completed.
    /// </summary>
    public required TimeSpan JobTimeout { get; set; }
}
