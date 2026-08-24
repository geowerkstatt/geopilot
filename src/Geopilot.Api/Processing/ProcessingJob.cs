using Geopilot.Pipeline;
using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Represents a processing job, a pipeline run scoped to one set of uploaded files and an optional mandate.
/// The uploaded files stay in the upload storage under <paramref name="UploadId"/> until the job is retired, or
/// until the run ends in a state that cannot be delivered.
/// </summary>
public record class ProcessingJob(
    Guid Id,
    Guid UploadId,
    int? MandateId,
    DateTime CreatedAt)
{
    /// <summary>
    /// The uploaded files staged for this job, in the order they were added. Files are only added while the
    /// job is still pending, but the collection is read while the run is under way, so it is immutable:
    /// adding a file replaces it instead of mutating it, and a reader iterates a snapshot that cannot change
    /// underneath it.
    /// </summary>
    public ImmutableList<ProcessingJobFile> Files { get; init; } = ImmutableList<ProcessingJobFile>.Empty;

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
