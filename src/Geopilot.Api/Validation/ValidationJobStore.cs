using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Geopilot.Api.Validation;

/// <summary>
/// Stores, retrieves and updates <see cref="ValidationJob"/> instances in memory in a thread-safe manner.
/// </summary>
public class ValidationJobStore : IValidationJobStore
{
    private readonly ConcurrentDictionary<Guid, ValidationJob> jobs = new();
    private readonly Channel<IPipeline> pipelineQueue = Channel.CreateUnbounded<IPipeline>();
    private readonly ConcurrentDictionary<IPipeline, Guid> pipelineJobMap = new();
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <inheritdoc/>
    public ChannelReader<IPipeline> ValidationQueue => pipelineQueue.Reader;

    /// <summary>
    /// Initializes a new instance of the ValidationJobStore class using the specified service scope factory.
    /// </summary>
    public ValidationJobStore(IServiceScopeFactory serviceScopeFactory)
    {
        this.serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc/>
    public ValidationJob? GetJob(Guid jobId) => jobs.TryGetValue(jobId, out var job) ? job : null;

    /// <inheritdoc/>
    public ValidationJob CreateJob()
    {
        var newJob = new ValidationJob(
            Id: Guid.NewGuid(),
            Files: new List<ValidationJobFile>(),
            MandateId: null,
            ValidatorResults: ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status: Status.Created,
            DateTime.Now);

        jobs[newJob.Id] = newJob; // Does not handle GUID collisions

        return newJob;
    }

    /// <inheritdoc/>
    public ValidationJob AddUploadInfoToJob(Guid jobId, UploadMethod uploadMethod, ImmutableList<CloudFileInfo> cloudFiles)
    {
        var updateFunc = (Guid jobId, ValidationJob currentJob) =>
        {
            if (currentJob.Status != Status.Created)
                throw new InvalidOperationException($"Cannot add upload info to job <{jobId}> because its status is <{currentJob.Status}> instead of <{Status.Created}>.");

            return currentJob with
            {
                UploadMethod = uploadMethod,
                CloudFiles = cloudFiles,
            };
        };

        return jobs.AddOrUpdate(jobId, id => throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId)), updateFunc);
    }

    private ValidationJob SetJobStatus(Guid jobId, Status status)
    {
        return jobs.AddOrUpdate(
            jobId,
            id => throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId)),
            (id, currentJob) => currentJob with { Status = status });
    }

    /// <inheritdoc/>
    public ValidationJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName)
    {
        var updateFunc = (Guid jobId, ValidationJob currentJob) =>
        {
            if (currentJob.Status != Status.Created && currentJob.Status != Status.VerifyingUpload)
                throw new InvalidOperationException($"Cannot add file to job <{jobId}> because its status is <{currentJob.Status}> instead of <{Status.Created}> or <{Status.VerifyingUpload}>.");

            var validationJobFile = new ValidationJobFile(originalFileName, tempFileName);
            if (currentJob.Files == null)
                currentJob = currentJob with { Files = new List<ValidationJobFile>() };

            currentJob.Files.Add(validationJobFile);
            return currentJob;
        };

        return jobs.AddOrUpdate(jobId, id => throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId)), updateFunc);
    }

    /// <inheritdoc/>
    public ValidationJob FinishUpload(Guid jobId)
    {
        return SetJobStatus(jobId, Status.Ready);
    }

    /// <inheritdoc/>
    public ValidationJob VerifyUpload(Guid jobId)
    {
        return SetJobStatus(jobId, Status.VerifyingUpload);
    }

    /// <inheritdoc/>
    public ValidationJob Failed(Guid jobId)
    {
        return SetJobStatus(jobId, Status.Failed);
    }

    /// <inheritdoc/>
    public ValidationJob StartJob(Guid jobId, IPipeline pipeline, int mandateId)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        // Create an immutable dictionary with a single entry for the pipeline of the mandate. Temporary solution until pipelines are fully integrated.
        var validatorResults = ImmutableDictionary.Create<string, ValidatorResult?>().Add("INTERLIS", null);

        var updateFunc = (Guid jobId, ValidationJob job) =>
        {
            if (job.Status != Status.Ready)
                throw new InvalidOperationException($"Cannot start job <{jobId}> because its status is <{job.Status}> instead of <{Status.Ready}>.");

            return job with
            {
                Status = Status.Processing,
                MandateId = mandateId,
                ValidatorResults = validatorResults,
            };
        };

        var updatedJob = jobs.AddOrUpdate(jobId, id => throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId)), updateFunc);

        if (pipelineQueue.Writer.TryWrite(pipeline))
            pipelineJobMap[pipeline] = jobId;

        return updatedJob;
    }

    /// <inheritdoc/>
    public ValidationJob AddValidatorResult(IPipeline pipeline, ValidatorResult result)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(result);

        if (!pipelineJobMap.TryGetValue(pipeline, out var jobId))
            throw new ArgumentException("The specified pipeline is not associated with any job.", nameof(pipeline));

        result = result with { LogFiles = result.LogFiles };

        var updateFunc = (Guid jobId, ValidationJob job) =>
        {
            if (job.Status != Status.Processing)
                throw new InvalidOperationException($"Cannot add validator result to job <{jobId}> because its status is <{job.Status}> instead of <{Status.Processing}>.");

            pipelineJobMap.TryRemove(pipeline, out _);

            var updatedResults = job.ValidatorResults.SetItem("INTERLIS", result);
            var updatedStatus = ValidationJob.GetStatusFromResults(updatedResults);
            return job with { ValidatorResults = updatedResults, Status = updatedStatus };
        };

        return jobs.AddOrUpdate(jobId, id => throw new ArgumentException($"Job with id <{jobId}> not found."), updateFunc);
    }

    /// <inheritdoc/>
    public int GetActiveCloudJobCount() => jobs.Values.Count(j => j.UploadMethod == UploadMethod.Cloud);

    /// <inheritdoc/>
    public bool RemoveJob(Guid jobId)
    {
        var job = GetJob(jobId);
        if (job == null)
            return false;

        // Remove all pipeline associations for the job being removed. Temporary solution until pipelines are fully integrated.
        var pipelinesToRemove = pipelineJobMap
            .Where(kvp => kvp.Value == jobId)
            .Select(kvp => kvp.Key);

        foreach (var pipelineToRemove in pipelinesToRemove)
        {
            var removed = pipelineJobMap.TryRemove(pipelineToRemove, out _);
            if (removed && pipelineToRemove != null)
                pipelineToRemove.Dispose();
        }

        return jobs.TryRemove(jobId, out _);
    }
}
