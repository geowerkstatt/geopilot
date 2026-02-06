namespace Geopilot.Api.Pipeline;

/// <summary>
/// Specifies the possible states of a pipeline in a workflow.
/// </summary>
/// <remarks>Use this enumeration to represent the current status of an individual pipeline, such as in a multi-step
/// operation or task sequence. The values indicate whether the pipeline is awaiting execution, currently running, completed
/// successfully, or has failed.</remarks>
public enum PipelineState
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
}
