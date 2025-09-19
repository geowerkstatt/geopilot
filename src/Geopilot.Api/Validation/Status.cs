namespace Geopilot.Api.Validation;

/// <summary>
/// The validator job statuses.
/// </summary>
public enum Status
{
    /// <summary>
    /// The job has been created but no file has been added yet.
    /// </summary>
    Created,

    /// <summary>
    /// The job is ready to be processed, a file has been added.
    /// </summary>
    Ready,

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
