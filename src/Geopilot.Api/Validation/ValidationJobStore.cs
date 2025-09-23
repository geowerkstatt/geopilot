using Microsoft.EntityFrameworkCore.Diagnostics;
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
    private readonly Channel<IValidator> validationQueue = Channel.CreateUnbounded<IValidator>();
    private readonly ConcurrentDictionary<IValidator, Guid> validatorJobMap = new();

    /// <inheritdoc/>
    public ChannelReader<IValidator> ValidationQueue => validationQueue.Reader;

    /// <inheritdoc/>
    public ValidationJob? GetJob(Guid jobId) => jobs.TryGetValue(jobId, out var job) ? job : null;

    /// <inheritdoc/>
    public ValidationJob CreateJob()
    {
        var newJob = new ValidationJob(
            Id: Guid.NewGuid(),
            OriginalFileName: null,
            TempFileName: null,
            ValidatorResults: ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status: Status.Created);

        jobs[newJob.Id] = newJob; // Does not handle GUID collisions

        return newJob;
    }

    /// <inheritdoc/>
    public ValidationJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName)
    {
        var updateFunc = (Guid jobId, ValidationJob currentJob) =>
        {
            if (currentJob.Status != Status.Created)
                throw new InvalidOperationException($"Cannot add file to job <{jobId}> because its status is <{currentJob.Status}> instead of <{Status.Created}>.");

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
    public ValidationJob StartJob(Guid jobId, ICollection<IValidator> validators)
    {
        if (validators == null || validators.Count == 0)
            throw new ArgumentException("At least one validator must be specified to start the validation.", nameof(validators));

        // Create an immutable dictionary with all validator keys set and their result null
        var validatorResults = validators.ToImmutableDictionary(v => v.Name, v => (ValidatorResult?)null);

        var updateFunc = (Guid jobId, ValidationJob job) =>
        {
            if (job.Status != Status.Ready)
                throw new InvalidOperationException($"Cannot start job <{jobId}> because its status is <{job.Status}> instead of <{Status.Ready}>.");

            return job with { Status = Status.Processing, ValidatorResults = validatorResults };
        };

        var updatedJob = jobs.AddOrUpdate(jobId, id => throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId)), updateFunc);

        foreach (var validator in validators)
        {
            if (validationQueue.Writer.TryWrite(validator))
                validatorJobMap[validator] = jobId;
        }

        return updatedJob;
    }

    /// <inheritdoc/>
    public ValidationJob AddValidatorResult(IValidator validator, ValidatorResult result)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(result);

        if (!validatorJobMap.TryGetValue(validator, out var jobId))
            throw new ArgumentException("The specified validator is not associated with any job.", nameof(validator));

        var updateFunc = (Guid jobId, ValidationJob job) =>
        {
            if (job.Status != Status.Processing)
                throw new InvalidOperationException($"Cannot add validator result to job <{jobId}> because its status is <{job.Status}> instead of <{Status.Processing}>.");

            validatorJobMap.TryRemove(validator, out _);

            var updatedResults = job.ValidatorResults.SetItem(validator.Name, result);
            var updatedStatus = ValidationJob.GetStatusFromResults(updatedResults);
            return job with { ValidatorResults = updatedResults, Status = updatedStatus };
        };

        return jobs.AddOrUpdate(jobId, id => throw new ArgumentException($"Job with id <{jobId}> not found."), updateFunc);
    }
}
