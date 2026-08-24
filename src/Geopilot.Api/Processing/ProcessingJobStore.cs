using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

/// <summary>
/// Stores, retrieves and updates <see cref="ProcessingJob"/> instances in memory in a thread-safe manner.
/// </summary>
/// <remarks>
/// Jobs and the processing queue are process-local, so the API has to run as exactly one instance. A second
/// instance does not see the jobs of the first one: a status request routed to the wrong instance answers 404,
/// so jobs appear to vanish at random while their pipeline runs where nobody asks for it. The state is not
/// persisted because a running <see cref="IPipeline"/> owns processor instances and temporary directories that
/// cannot be serialized, so recovery would mean re-running a job from its uploaded files rather than resuming
/// it. Running more than one instance therefore requires a persisted job store and shared file storage first.
/// </remarks>
public class ProcessingJobStore : IProcessingJobStore
{
    private readonly ConcurrentDictionary<Guid, ProcessingJob> jobs = new();
    private readonly Channel<ProcessingWorkItem> processingQueue = Channel.CreateUnbounded<ProcessingWorkItem>();

    /// <inheritdoc/>
    public ChannelReader<ProcessingWorkItem> ProcessingQueue => processingQueue.Reader;

    /// <inheritdoc/>
    public ProcessingJob? GetJob(Guid jobId) => jobs.TryGetValue(jobId, out var job) ? job : null;

    /// <inheritdoc/>
    public IReadOnlyCollection<Guid> GetJobIds() => jobs.Keys.ToList();

    /// <inheritdoc/>
    public ProcessingJob CreateJob(Guid uploadId)
    {
        var newJob = new ProcessingJob(
            Id: Guid.NewGuid(),
            UploadId: uploadId,
            MandateId: null,
            CreatedAt: DateTime.UtcNow);

        jobs[newJob.Id] = newJob;
        return newJob;
    }

    /// <inheritdoc/>
    public ProcessingJob MarkAsFailed(Guid jobId)
    {
        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, currentJob) =>
            {
                if (!CanMarkAsFailed(currentJob))
                {
                    throw new InvalidOperationException(
                        $"Cannot transition job <{id}> from <{currentJob.State}> to <{ProcessingState.Failed}>.");
                }

                return currentJob with { State = ProcessingState.Failed };
            });
    }

    /// <inheritdoc/>
    public bool TryMarkAsFailed(Guid jobId) =>
        TryTransition(jobId, CanMarkAsFailed, ProcessingState.Failed);

    /// <inheritdoc/>
    public ProcessingJob PipelineFinished(Guid jobId, ProcessingState pipelineState)
    {
        if (!IsTerminal(pipelineState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pipelineState),
                pipelineState,
                "Pipeline must have finished in a terminal state.");
        }

        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, currentJob) =>
            {
                if (!CanCompletePipeline(currentJob))
                {
                    throw new InvalidOperationException(
                        $"Cannot transition job <{id}> from <{currentJob.State}> to <{pipelineState}>.");
                }

                return currentJob with { State = pipelineState };
            });
    }

    /// <inheritdoc/>
    public bool TryPipelineFinished(Guid jobId, ProcessingState pipelineState) =>
        IsTerminal(pipelineState) && TryTransition(jobId, CanCompletePipeline, pipelineState);

    /// <inheritdoc/>
    public ProcessingJob AttachPipeline(Guid jobId, IPipeline pipeline, int mandateId)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, job) =>
            {
                EnsureJobIsPrePipeline(id, job, "attach pipeline");
                return job with
                {
                    MandateId = mandateId,
                    Pipeline = pipeline,
                };
            });
    }

    /// <inheritdoc/>
    public ProcessingJob EnqueueForProcessing(Guid jobId, IReadOnlyList<IPipelineFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var updatedJob = jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, job) =>
            {
                if (job.Pipeline == null)
                    throw new InvalidOperationException($"Cannot enqueue job <{id}> because no pipeline has been attached.");
                if (job.State != ProcessingState.Pending)
                    throw new InvalidOperationException($"Cannot enqueue job <{id}> because it is in state <{job.State}>.");

                return job with { State = ProcessingState.Running };
            });

        processingQueue.Writer.TryWrite(new ProcessingWorkItem(updatedJob.Pipeline!, files));
        return updatedJob;
    }

    /// <inheritdoc/>
    public bool RemoveJob(Guid jobId)
    {
        if (!jobs.TryRemove(jobId, out var removed))
            return false;

        // Idempotent dispose handles the case where the runner already disposed after extracting.
        removed.Pipeline?.Dispose();
        return true;
    }

    private static bool IsTerminal(ProcessingState state) =>
        state is ProcessingState.Success or ProcessingState.Warning or ProcessingState.DeliveryRestriction
            or ProcessingState.Failed or ProcessingState.Cancelled;

    private static bool CanMarkAsFailed(ProcessingJob job) =>
        job.State is ProcessingState.Pending or ProcessingState.Running;

    private static bool CanCompletePipeline(ProcessingJob job) =>
        job.State == ProcessingState.Running;

    /// <summary>
    /// Applies <paramref name="newState"/> when <paramref name="isAllowed"/> accepts the job's current state,
    /// retrying against the freshly read job whenever a concurrent write wins the compare-and-swap. Reports the
    /// outcome rather than throwing, so a caller that is already handling a failure cannot make it worse. The
    /// transition rules are shared with the throwing operations, so both variants can never drift apart.
    /// </summary>
    private bool TryTransition(Guid jobId, Func<ProcessingJob, bool> isAllowed, ProcessingState newState)
    {
        while (jobs.TryGetValue(jobId, out var currentJob))
        {
            if (!isAllowed(currentJob))
                return false;

            if (jobs.TryUpdate(jobId, currentJob with { State = newState }, currentJob))
                return true;
        }

        return false;
    }

    private static void EnsureJobIsPrePipeline(Guid jobId, ProcessingJob job, string operation)
    {
        if (job.Pipeline != null)
            throw new InvalidOperationException($"Cannot {operation} for job <{jobId}> because a pipeline has already been associated.");
        if (job.State == ProcessingState.Failed)
            throw new InvalidOperationException($"Cannot {operation} for job <{jobId}> because the job has been marked as failed.");
    }
}
