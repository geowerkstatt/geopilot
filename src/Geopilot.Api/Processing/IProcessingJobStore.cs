using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

/// <summary>
/// Managed store for <see cref="ProcessingJob"/> instances. Provides create, read and update operations.
/// Implementations must be thread safe.
/// </summary>
public interface IProcessingJobStore
{
    /// <summary>
    /// Channel reader for the processing queue. Yields <see cref="ProcessingWorkItem"/> instances (a pipeline plus
    /// its staged files) ready for execution. The job's lifecycle state is reflected on the pipeline itself;
    /// consumers do not need to report a result back.
    /// </summary>
    ChannelReader<ProcessingWorkItem> ProcessingQueue { get; }

    /// <summary>
    /// Retrieves a <see cref="ProcessingJob"/> by its id.
    /// </summary>
    /// <param name="jobId">The id of the processing job.</param>
    /// <returns>The job, or <see langword="null"/> when no job with the specified id exists.</returns>
    ProcessingJob? GetJob(Guid jobId);

    /// <summary>
    /// Returns the ids of all known jobs. Used by the cleanup service, which cannot rely on a job
    /// leaving a directory behind: an uploaded file is only fetched once a step reads it.
    /// </summary>
    IReadOnlyCollection<Guid> GetJobIds();

    /// <summary>
    /// Creates and stores a new <see cref="ProcessingJob"/> for the specified upload.
    /// </summary>
    /// <param name="uploadId">The upload whose files the job processes.</param>
    ProcessingJob CreateJob(Guid uploadId);

    /// <summary>
    /// Marks the specified job as failed (e.g. preflight failure before a pipeline could be created).
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
    /// <exception cref="InvalidOperationException">If the job is already in a terminal state.</exception>
    ProcessingJob MarkAsFailed(Guid jobId);

    /// <summary>
    /// Marks the job as failed and reports whether it happened, instead of throwing when the transition is
    /// not allowed. This is the variant for error paths: a caller that is itself handling a failure cannot
    /// afford a second exception, because it escapes to the hosting background service and stops the host
    /// instead of recording the failure. Use the throwing <see cref="MarkAsFailed"/> wherever an invalid
    /// transition is a programming error that has to surface.
    /// </summary>
    /// <param name="jobId">The job to mark as failed.</param>
    /// <returns><see langword="true"/> when the job was marked as failed; <see langword="false"/> when it is
    /// unknown or already in a terminal state, in which case it is left untouched.</returns>
    bool TryMarkAsFailed(Guid jobId);

    /// <summary>
    /// Transitions the job to its terminal state based on the state the pipeline finished in.
    /// </summary>
    /// <param name="jobId">The job whose pipeline has finished.</param>
    /// <param name="pipelineState">
    /// The terminal state the pipeline ended in. Must be one of <see cref="ProcessingState.Success"/>,
    /// <see cref="ProcessingState.Warning"/>, <see cref="ProcessingState.DeliveryRestriction"/>,
    /// <see cref="ProcessingState.Failed"/>, or <see cref="ProcessingState.Cancelled"/>.
    /// </param>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pipelineState"/> is not a terminal state.</exception>
    /// <exception cref="InvalidOperationException">If the job is not in <see cref="ProcessingState.Running"/>.</exception>
    ProcessingJob PipelineFinished(Guid jobId, ProcessingState pipelineState);

    /// <summary>
    /// Transitions the job to its terminal state and reports whether it happened, instead of throwing when
    /// the transition is not allowed. See <see cref="TryMarkAsFailed"/> for when to prefer this over
    /// <see cref="PipelineFinished"/>.
    /// </summary>
    /// <param name="jobId">The job whose pipeline has finished.</param>
    /// <param name="pipelineState">The terminal state the pipeline ended in.</param>
    /// <returns><see langword="true"/> when the job was transitioned; <see langword="false"/> when it is
    /// unknown or not in <see cref="ProcessingState.Running"/>, in which case it is left untouched.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pipelineState"/> is not a terminal
    /// state, which is a defect at the call site rather than a job that moved on.</exception>
    bool TryPipelineFinished(Guid jobId, ProcessingState pipelineState);

    /// <summary>
    /// Associates the given <paramref name="pipeline"/> with the job at creation time, without queuing it.
    /// The pipeline is started only later via <see cref="EnqueueForProcessing"/>, once its files have been staged.
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found, or <paramref name="pipeline"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If the job already has a pipeline or has been marked failed.</exception>
    ProcessingJob AttachPipeline(Guid jobId, IPipeline pipeline, int mandateId);

    /// <summary>
    /// Queues the job's already-attached pipeline for execution together with its staged <paramref name="files"/>,
    /// transitioning the job to <see cref="ProcessingState.Running"/>.
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found, or <paramref name="files"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If no pipeline is attached or the job is no longer pending.</exception>
    ProcessingJob EnqueueForProcessing(Guid jobId, IReadOnlyList<IPipelineFile> files);

    /// <summary>
    /// Removes the job from the store and disposes its pipeline (if any).
    /// </summary>
    bool RemoveJob(Guid jobId);
}
