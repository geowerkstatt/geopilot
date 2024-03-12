namespace Geopilot.Api.Validation;

/// <summary>
/// The validator job statuses.
/// </summary>
public enum Status
{
    /// <summary>
    /// The job is processing.
    /// </summary>
    Processing,

    /// <summary>
    /// The job completed without errors.
    /// </summary>
    Completed,

    /// <summary>
    /// The job completed with errors.
    /// </summary>
    CompletedWithErrors,

    /// <summary>
    /// The job failed.
    /// </summary>
    Failed,
}
