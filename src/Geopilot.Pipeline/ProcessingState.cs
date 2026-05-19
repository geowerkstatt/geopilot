namespace Geopilot.Api.Pipeline;

/// <summary>
/// Specifies the possible states of a processing job in a workflow.
/// </summary>
/// <remarks>Use this enumeration to represent the current status of a processing job, such as in a multi-step
/// operation or task sequence. The values indicate whether the job is awaiting execution, currently running, completed
/// successfully, or has failed.</remarks>
public enum ProcessingState
{
    /// <summary>
    /// Indicates that the operation or request is pending and has not yet completed.
    /// </summary>
    Pending,

    /// <summary>
    /// Indicates that the process or operation is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates that the process or operation has completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Indicates that the process or operation has failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates that the processing job was cancelled before completion
    /// (e.g. job timeout or host shutdown). Distinct from <see cref="Failed"/> —
    /// the job did not fail by its own logic, it was interrupted.
    /// </summary>
    Cancelled,
}
