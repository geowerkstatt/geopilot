using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
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
            OriginalFileName: null,
            TempFileName: null,
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

    /// <inheritdoc/>
    public ValidationJob SetJobStatus(Guid jobId, Status status)
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

            return currentJob with
            {
                OriginalFileName = originalFileName,
                TempFileName = tempFileName,
                Status = Status.Ready,
            };
        };

        return jobs.AddOrUpdate(jobId, id => throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId)), updateFunc);
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

        var logFiles = CopyAndMapLogFiles(jobId, result.LogFiles);

        result = result with { LogFiles = logFiles.ToImmutableDictionary() };

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

    /// <summary>
    /// Copies all the files created by the pipeline run to the job's directory.
    /// </summary>
    /// <remarks>This is temporary code, only required because this version tries to integrate the new pipeline architecture into the legacy code.</remarks>
    /// <returns>A new dictionary with the same keys, but the values are adjusted to the names of the files in the job's directory, instead of the temp paths created by the pipeline.</returns>
    private Dictionary<string, string> CopyAndMapLogFiles(Guid jobId, ImmutableDictionary<string, string> logFiles)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var fileProvider = scope.ServiceProvider.GetRequiredService<IFileProvider>();
        fileProvider.Initialize(jobId);

        var mappedLogFiles = new Dictionary<string, string>();
        foreach (var logFile in logFiles)
        {
            using var fileHandle = fileProvider.CreateFileWithRandomName(Path.GetExtension(logFile.Value));
            using var stream = File.OpenRead(logFile.Value);
            stream.CopyTo(fileHandle.Stream);
            mappedLogFiles[logFile.Key] = fileHandle.FileName;
        }

        return mappedLogFiles;
    }

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
