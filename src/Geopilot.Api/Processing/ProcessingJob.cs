using Geopilot.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// Represents a processing job, a pipeline run scoped to one set of uploaded files and an optional mandate.
/// The uploaded files stay in cloud storage under <paramref name="UploadId"/> until the job is retired, or
/// until the run ends in a state that cannot be delivered.
/// </summary>
public record class ProcessingJob(
    Guid Id,
    Guid UploadId,
    List<ProcessingJobFile> Files,
    int? MandateId,
    DateTime CreatedAt)
{
    /// <summary>
    /// The pipeline associated with this job. Instantiated when the job is created (before its files are staged)
    /// and started once staging completes, so consumers can render the pipeline's steps while preflight runs.
    /// </summary>
    public IPipeline? Pipeline { get; init; }

    /// <summary>
    /// The lifecycle state of the job.
    /// </summary>
    public ProcessingState State { get; init; } = ProcessingState.Pending;
}
