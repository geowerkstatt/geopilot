using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Geopilot.Api.Validation;

/// <summary>
/// Runs validation jobs in the background and provides access to job status information.
/// </summary>
public class ValidationRunner : BackgroundService, IValidationRunner
{
    private readonly ILogger<ValidationRunner> logger;
    private readonly Channel<(ValidationJob Job, IValidator Validator)> queue;
    private readonly ConcurrentDictionary<Guid, ValidationJob> jobs = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationRunner"/> class.
    /// </summary>
    public ValidationRunner(ILogger<ValidationRunner> logger)
    {
        this.logger = logger;
        queue = Channel.CreateUnbounded<(ValidationJob, IValidator)>();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(queue.Reader.ReadAllAsync(stoppingToken), stoppingToken, async (item, cancellationToken) =>
        {
            var validatorName = item.Validator.Name;
            var jobId = item.Job.Id;
            try
            {
                UpdateJobStatus(jobId, validatorName, new ValidatorResult(Status.Processing, "Die Datei wird validiert..."));
                var result = await item.Validator.ExecuteAsync(item.Job, cancellationToken);
                UpdateJobStatus(jobId, validatorName, result);
            }
            catch (ValidationFailedException ex)
            {
                UpdateJobStatus(jobId, validatorName, new ValidatorResult(Status.Failed, ex.Message));
            }
            catch (Exception ex)
            {
                var traceId = Guid.NewGuid();
                UpdateJobStatus(jobId, validatorName, new ValidatorResult(Status.Failed, $"Unbekannter Fehler. Fehler-Id: <{traceId}>"));
                logger.LogError(ex, "Unhandled exception TraceId: <{TraceId}> Message: <{ErrorMessage}>", traceId, ex.Message);
            }
        });
    }

    /// <inheritdoc/>
    public async Task EnqueueJobAsync(ValidationJob validationJob, IEnumerable<IValidator> validators)
    {
        ArgumentNullException.ThrowIfNull(validationJob);
        ArgumentNullException.ThrowIfNull(validators);

        jobs[validationJob.Id] = validationJob;
        foreach (var validator in validators)
        {
            await queue.Writer.WriteAsync((validationJob, validator));
        }
    }

    /// <inheritdoc/>
    public ValidationJob? GetJob(Guid jobId)
    {
        return jobs.GetValueOrDefault(jobId);
    }

    /// <summary>
    /// Adds or updates the status for the given <paramref name="jobId"/>.
    /// </summary>
    /// <param name="jobId">The identifier of the job to update.</param>
    /// <param name="validatorName">The name of the validator.</param>
    /// <param name="validatorResult">The result of the validator.</param>
    private void UpdateJobStatus(Guid jobId, string validatorName, ValidatorResult validatorResult)
    {
        var job = jobs[jobId];
        job.ValidatorResults[validatorName] = validatorResult;
        job.UpdateJobStatusFromResults();
    }
}
