namespace Geopilot.Api.Pipeline;

/// <summary>
/// Specifies the possible states of a step in a pipeline workflow.
/// </summary>
/// <remarks>Use this enumeration to represent the current status of an individual step, such as in a multi-step
/// operation or task sequence. The values indicate whether the step is awaiting execution, currently running, completed
/// successfully, or has failed.</remarks>
[Flags]
public enum StepState
{
    /// <summary>
    /// Indicates that the operation or request is pending and has not yet completed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Indicates that the process or operation is currently running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Indicates that the process or operation has completed successfully.
    /// </summary>
    Success = 2,

    /// <summary>
    /// Indicates that the process or operation has failed.
    /// </summary>
    Failed = 4,
}
