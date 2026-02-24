namespace Geopilot.Api.Validation;

/// <summary>
/// The validator job statuses.
/// </summary>
public enum Status
{
    /// <summary>
    /// The job has been created.
    /// </summary>
    Created,

    /// <summary>
    /// The job is awaiting file upload to cloud storage.
    /// </summary>
    AwaitingUpload,

    /// <summary>
    /// The job is verifying uploaded cloud files.
    /// </summary>
    VerifyingUpload,

    /// <summary>
    /// The job is ready to be processed.
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
