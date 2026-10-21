using Geopilot.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// Represents a processing job, a pipeline run scoped to one set of uploaded files and an optional mandate.
/// The uploaded files stay in the upload storage under <paramref name="UploadId"/> until the job ends, or, when its
/// run was cancelled, until the upload session expires. Retiring the job never releases them.
/// </summary>
public record class ProcessingJob(
    Guid Id,
    Guid UploadId,
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

    /// <summary>
    /// When the job reached its terminal state, or <see langword="null"/> while it is pending or running. The
    /// retention of its downloads and visualizations counts from here, so a job that waited long in the queue
    /// keeps them for the full window.
    /// </summary>
    public DateTime? FinishedAt { get; init; }

    /// <summary>
    /// Whether the run can ever become a delivery: whoever started it is a member of an organisation of the mandate,
    /// and the mandate accepts deliveries. Decided once when the job starts; a run nobody can declare is not staged,
    /// and its delivery is refused. Declaring still checks the mandate on its own.
    /// </summary>
    public bool IsDeclarable { get; init; }
}
