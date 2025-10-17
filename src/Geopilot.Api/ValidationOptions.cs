using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api;

/// <summary>
/// Configuration options for running and managing validation jobs.
/// </summary>
public class ValidationOptions
{
    /// <summary>
    /// The duration after which validation jobs are eligible for cleanup.
    /// </summary>
    public TimeSpan JobRetention { get; set; }

    /// <summary>
    /// The interval at which the cleanup service runs to remove old validation jobs.
    /// </summary>
    public TimeSpan JobCleanupInterval { get; set; }

    /// <summary>
    /// The timeout durations for each validator.
    /// </summary>
    public required Dictionary<string, TimeSpan> ValidatorTimeouts { get; set; }
}
