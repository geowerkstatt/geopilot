using Geopilot.Api.Enums;
using Geopilot.Api.Pipeline;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

/// <summary>
/// Stores, retrieves and updates <see cref="ProcessingJob"/> instances in memory in a thread-safe manner.
/// </summary>
public class ProcessingJobStore : IProcessingJobStore
{
    private readonly ConcurrentDictionary<Guid, ProcessingJob> jobs = new();
    private readonly Channel<IPipeline> pipelineQueue = Channel.CreateUnbounded<IPipeline>();

    /// <inheritdoc/>
    public ChannelReader<IPipeline> ProcessingQueue => pipelineQueue.Reader;

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
    public ProcessingJob AddUploadInfoToJob(Guid jobId, UploadMethod uploadMethod, ImmutableList<CloudFileInfo> cloudFiles)
    {
        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, currentJob) =>
            {
                EnsureJobIsPrePipeline(id, currentJob, "add upload info");
                if (currentJob.Files.Count > 0)
                    throw new InvalidOperationException($"Cannot add upload info to job <{id}> because it already has files.");

                return currentJob with { UploadMethod = uploadMethod, CloudFiles = cloudFiles };
            });
    }

    /// <inheritdoc/>
    public ProcessingJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName)
    {
        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, currentJob) =>
            {
                EnsureJobIsPrePipeline(id, currentJob, "add file");
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
            (id, currentJob) => currentJob with { IsFailed = true });
    }

    /// <inheritdoc/>
    public ProcessingJob StartJob(Guid jobId, IPipeline pipeline, int mandateId)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var updatedJob = jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{id}> not found.", nameof(jobId)),
            (id, job) =>
            {
                EnsureJobIsPrePipeline(id, job, "start");
                return job with { MandateId = mandateId, Pipeline = pipeline };
            });

        pipelineQueue.Writer.TryWrite(pipeline);
        return updatedJob;
    }

    /// <inheritdoc/>
    public int GetActiveCloudJobCount() => jobs.Values.Count(j => j.UploadMethod == UploadMethod.Cloud);

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
        if (job.IsFailed)
            throw new InvalidOperationException($"Cannot {operation} for job <{jobId}> because the job has been marked as failed.");
    }
}
