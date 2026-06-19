using Geopilot.Pipeline;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

/// <summary>
/// Managed store for <see cref="ProcessingJob"/> instances. Provides create, read and update operations.
/// Implementations must be thread safe.
/// </summary>
public interface IProcessingJobStore
{
    /// <summary>
    /// Channel reader for the processing queue. Yields <see cref="IPipeline"/> instances ready for execution.
    /// The job's lifecycle state is reflected on the pipeline itself; consumers do not need to report a result back.
    /// </summary>
    ChannelReader<IPipeline> ProcessingQueue { get; }

    /// <summary>
    /// Retrieves a <see cref="ProcessingJob"/> by its id.
    /// </summary>
    /// <param name="jobId">The id of the processing job.</param>
    /// <returns>The job, or <see langword="null"/> when no job with the specified id exists.</returns>
    ProcessingJob? GetJob(Guid jobId);

    /// <summary>
    /// Creates and stores a new <see cref="ProcessingJob"/>.
    /// </summary>
    ProcessingJob CreateJob();

    /// <summary>
    /// Adds the specified file to the job.
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
    /// <exception cref="InvalidOperationException">If the job already has a pipeline or has been marked failed.</exception>
    ProcessingJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName);

    /// <summary>
    /// Marks the specified job as failed (e.g. cloud preflight failure before a pipeline could be created).
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
    /// <exception cref="InvalidOperationException">If the job is already in a terminal state.</exception>
    ProcessingJob MarkAsFailed(Guid jobId);

    /// <summary>
    /// Transitions the job to its terminal state based on the state the pipeline finished in.
    /// </summary>
    /// <param name="jobId">The job whose pipeline has finished.</param>
    /// <param name="pipelineState">
    /// The terminal state the pipeline ended in. Must be one of <see cref="ProcessingState.Success"/>,
    /// <see cref="ProcessingState.Failed"/>, or <see cref="ProcessingState.Cancelled"/>.
    /// </param>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pipelineState"/> is not a terminal state.</exception>
    /// <exception cref="InvalidOperationException">If the job is not in <see cref="ProcessingState.Running"/>.</exception>
    ProcessingJob PipelineFinished(Guid jobId, ProcessingState pipelineState);

    /// <summary>
    /// Records the pipeline id the job will run with, so consumers can render step display info before the
    /// pipeline is actually instantiated (cloud upload flow).
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
    ProcessingJob SetPipelineId(Guid jobId, string pipelineId);

    /// <summary>
    /// Associates the given <paramref name="pipeline"/> with the job and queues it for execution.
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found, or <paramref name="pipeline"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If the job already has a pipeline or has been marked failed.</exception>
    ProcessingJob StartJob(Guid jobId, IPipeline pipeline, int mandateId);

    /// <summary>
    /// Removes the job from the store and disposes its pipeline (if any).
    /// </summary>
    bool RemoveJob(Guid jobId);
}
