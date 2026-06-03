using Geopilot.Api.Enums;
using Geopilot.Pipeline;
using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Represents a processing job — a pipeline run scoped to one set of uploaded files and an optional mandate.
/// </summary>
public record class ProcessingJob(
    Guid Id,
    List<ProcessingJobFile> Files,
    int? MandateId,
    DateTime CreatedAt,
    UploadMethod UploadMethod = UploadMethod.Direct,
    ImmutableList<CloudFileInfo>? CloudFiles = null)
{
    /// <summary>
    /// The pipeline running (or already run) for this job. <see langword="null"/> until the job has been started.
    /// </summary>
    public IPipeline? Pipeline { get; init; }

    /// <summary>
    /// The id of the pipeline definition this job is associated with. Set as soon as the mandate is resolved
    /// (for cloud uploads this happens before <see cref="Pipeline"/> is instantiated, so consumers can show
    /// the pipeline's steps while preflight is still running).
    /// </summary>
    public string? PipelineId { get; init; }

    /// <summary>
    /// The lifecycle state of the job.
    /// </summary>
    public ProcessingState State { get; init; } = ProcessingState.Pending;
}
