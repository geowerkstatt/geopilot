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
    public Guid? GetJobId(IValidator validator) => validatorJobMap.TryGetValue(validator, out var jobId) ? jobId : null;

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
    public bool TryAddFileToJob(Guid jobId, string originalFileName, string tempFileName, out ValidationJob updatedJob)
    {
        var job = GetJob(jobId) ?? throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId));

        if (job.Status != Status.Created)
            throw new InvalidOperationException($"Cannot add file to job <{jobId}> because its status is <{job.Status}> instead of <{Status.Created}>.");

        var updateFunc = (ValidationJob job) =>
        {
            return job with
            {
                OriginalFileName = originalFileName,
                TempFileName = tempFileName,
                Status = Status.Ready,
            };
        };

        var maxRetries = 5; // Arbitrary retry limit
        if (!jobs.TryUpdateWithRetry(job.Id, updateFunc, maxRetries, out updatedJob))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public bool TryStartJob(Guid jobId, ICollection<IValidator> validators, out ValidationJob updatedJob)
    {
        var job = GetJob(jobId) ?? throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId));

        if (validators == null || validators.Count == 0)
            throw new ArgumentException("At least one validator must be specified to start the validation.", nameof(validators));

        if (job.Status != Status.Ready)
            throw new InvalidOperationException($"Cannot start job <{jobId}> because its status is <{job.Status}> instead of <{Status.Ready}>.");

        // Create an immutable dictionary with all validator keys set and their result null
        var validatorResults = validators.ToImmutableDictionary(v => v.Name, v => (ValidatorResult?)null);

        var updateFunc = (ValidationJob job) => job with { Status = Status.Processing, ValidatorResults = validatorResults };

        var maxRetries = 5; // Arbitrary retry limit
        if (!jobs.TryUpdateWithRetry(job.Id, updateFunc, maxRetries, out updatedJob))
            return false;

        foreach (var validator in validators)
        {
            validationQueue.Writer.TryWrite(validator);
            validatorJobMap[validator] = job.Id;
        }

        return true;
    }

    /// <inheritdoc/>
    public bool TryAddValidatorResult(IValidator validator, ValidatorResult result, out ValidationJob updatedJob)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(result);

        var jobId = GetJobId(validator) ?? throw new ArgumentException("The specified validator is not associated with any job.", nameof(validator));
        var job = GetJob(jobId) ?? throw new InvalidOperationException($"Job with id <{jobId}> not found.");

        if (job.Status != Status.Processing)
            throw new InvalidOperationException($"Cannot add validator result to job <{jobId}> because its status is <{job.Status}> instead of <{Status.Processing}>.");

        var updateFunc = (ValidationJob job) =>
        {
            var updatedResults = job.ValidatorResults.SetItem(validator.Name, result);
            var updatedStatus = ValidationJob.GetStatusFromResults(updatedResults);
            return job with { ValidatorResults = updatedResults, Status = updatedStatus };
        };

        validatorJobMap.TryRemove(validator, out _);

        var maxRetries = job.ValidatorResults.Count * 2; // Arbitrary retry limit based on number of validators
        return jobs.TryUpdateWithRetry(jobId, updateFunc, maxRetries, out updatedJob);
    }
}
