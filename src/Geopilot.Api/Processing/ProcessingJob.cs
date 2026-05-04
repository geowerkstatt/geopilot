using Geopilot.Api.Enums;
using Geopilot.Api.Pipeline;
using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Represents a processing job — a pipeline run scoped to one set of uploaded files and an optional mandate.
/// </summary>
/// <remarks>
/// The job's <see cref="Pipeline.ProcessingState"/> is computed from the underlying <see cref="Pipeline"/> if one has
/// been associated; otherwise it is <see cref="Pipeline.ProcessingState.Pending"/>, or
/// <see cref="Pipeline.ProcessingState.Failed"/> when <see cref="IsFailed"/> is set (e.g. cloud preflight failure
/// before a pipeline could be created).
/// </remarks>
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
    /// Indicates that the job failed before a pipeline could complete (e.g. cloud preflight failure, security scan).
    /// </summary>
    public bool IsFailed { get; init; }
}
