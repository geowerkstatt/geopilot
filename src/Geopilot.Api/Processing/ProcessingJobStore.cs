using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

/// <summary>
/// Stores, retrieves and updates <see cref="ProcessingJob"/> instances in memory in a thread-safe manner.
/// </summary>
public class ProcessingJobStore : IProcessingJobStore
{
    private readonly ConcurrentDictionary<Guid, ProcessingJob> jobs = new();
    private readonly Channel<ProcessingWorkItem> processingQueue = Channel.CreateUnbounded<ProcessingWorkItem>();

    /// <inheritdoc/>
    public ChannelReader<ProcessingWorkItem> ProcessingQueue => processingQueue.Reader;

    /// <inheritdoc/>
    public ProcessingJob? GetJob(Guid jobId) => jobs.TryGetValue(jobId, out var job) ? job : null;

    /// <inheritdoc/>
    public ProcessingJob CreateJob()
    {
        var newJob = new ProcessingJob(
            Id: Guid.NewGuid(),
            Files: new List<ProcessingJobFile>(),
            MandateId: null,
            CreatedAt: DateTime.Now);

        jobs[newJob.Id] = newJob;
        return newJob;
    }

    /// <inheritdoc/>
    public ProcessingJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName)
    {
        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, currentJob) =>
            {
                EnsureJobIsPending(id, currentJob, "add file");
                currentJob.Files.Add(new ProcessingJobFile(originalFileName, tempFileName));
                return currentJob;
            });
    }

    /// <inheritdoc/>
    public ProcessingJob MarkAsFailed(Guid jobId)
    {
        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, currentJob) =>
            {
                if (currentJob.State is not (ProcessingState.Pending or ProcessingState.Running))
                {
                    throw new InvalidOperationException(
                        $"Cannot transition job <{id}> from <{currentJob.State}> to <{ProcessingState.Failed}>.");
                }

                return currentJob with { State = ProcessingState.Failed };
            });
    }

    /// <inheritdoc/>
    public ProcessingJob PipelineFinished(Guid jobId, ProcessingState pipelineState)
    {
        if (pipelineState is not (ProcessingState.Success or ProcessingState.Failed or ProcessingState.Cancelled))
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
                if (currentJob.State != ProcessingState.Running)
                {
                    throw new InvalidOperationException(
                        $"Cannot transition job <{id}> from <{currentJob.State}> to <{pipelineState}>.");
                }

                return currentJob with { State = pipelineState };
            });
    }

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
    public ProcessingJob EnqueueForProcessing(Guid jobId, IPipelineFileList files)
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

    private static void EnsureJobIsPrePipeline(Guid jobId, ProcessingJob job, string operation)
    {
        if (job.Pipeline != null)
            throw new InvalidOperationException($"Cannot {operation} for job <{jobId}> because a pipeline has already been associated.");
        if (job.State == ProcessingState.Failed)
            throw new InvalidOperationException($"Cannot {operation} for job <{jobId}> because the job has been marked as failed.");
    }

    private static void EnsureJobIsPending(Guid jobId, ProcessingJob job, string operation)
    {
        if (job.State != ProcessingState.Pending)
            throw new InvalidOperationException($"Cannot {operation} for job <{jobId}> because it is in state <{job.State}>.");
    }
}
