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
    /// The job is verifying and staging uploaded cloud files.
    /// </summary>
    VerifyingUpload,

    /// <summary>
    /// The upload was incomplete and the client may retry.
    /// </summary>
    UploadIncomplete,

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
