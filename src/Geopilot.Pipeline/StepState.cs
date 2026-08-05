namespace Geopilot.Pipeline;

/// <summary>
/// Specifies the possible states of a step in a pipeline workflow.
/// </summary>
/// <remarks>Use this enumeration to represent the current status of an individual step, such as in a multi-step
/// operation or task sequence. The values indicate whether the step is awaiting execution, currently running, completed
/// successfully, or has failed.</remarks>
public enum StepState
{
    /// <summary>
    /// Indicates that the operation or request is pending and has not yet completed.
    /// </summary>
    Pending,

    /// <summary>
    /// Indicates that the process or operation was skipped.
    /// </summary>
    Skipped,

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
    Error,

    /// <summary>
    /// Indicates that the process or operation was cancelled before completion
    /// (e.g. job timeout or host shutdown). Distinct from <see cref="Error"/> —
    /// the step did not fail by its own logic, it was interrupted.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Indicates that the process or operation completed but reported issues (warnings). The step ran to
    /// completion and the pipeline continues; distinct from <see cref="Success"/> (no issues reported) and
    /// from <see cref="Error"/> (the step failed and the pipeline stops).
    /// </summary>
    Warning,

    /// <summary>
    /// Indicates that the step completed but its result restricts delivery: the produced data may not be
    /// delivered. The step ran to completion and the pipeline continues; distinct from <see cref="Warning"/>
    /// (a non-blocking issue that still allows delivery) and from <see cref="Error"/> (the step failed and
    /// the pipeline stops).
    /// </summary>
    DeliveryRestriction,
}
