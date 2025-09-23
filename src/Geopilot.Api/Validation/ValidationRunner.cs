namespace Geopilot.Api.Validation;

/// <summary>
/// Runs validation jobs in the background.
/// </summary>
public class ValidationRunner : BackgroundService
{
    private readonly ILogger<ValidationRunner> logger;
    private readonly IValidationJobStore jobStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationRunner"/> class.
    /// </summary>
    public ValidationRunner(ILogger<ValidationRunner> logger, IValidationJobStore jobStore)
    {
        this.logger = logger;
        this.jobStore = jobStore;
    }

    /// <summary>
    /// Processes <see cref="IValidator"/> instances retrieved from the <see cref="ValidationJobStore"/> in parallel.
    /// </summary>
    /// <remarks>For every <see cref="IValidator"/> processed, a <see cref="ValidatorResult"/> is created and delivered to the <see cref="ValidationJobStore"/>.</remarks>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that is used to signal the operation should stop.</param>
    /// <returns>A task that represents the asynchronous execution of the validation process.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(jobStore.ValidationQueue.ReadAllAsync(stoppingToken), stoppingToken, async (validator, cancellationToken) =>
        {
            ValidatorResult? result = null;

            try
            {
                result = await validator.ExecuteAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is ValidationFailedException)
            {
                logger.LogError(ex, "Validator <{Validator}> failed.", validator.Name);
                result = new ValidatorResult(ValidatorResultStatus.Failed, ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Validator <{Validator}> failed.", validator.Name);
                result = new ValidatorResult(ValidatorResultStatus.Failed, $"An unexpected error occured while running validation with validator <{validator.Name}>.");
            }

            jobStore.AddValidatorResult(validator, result);
        });
    }
}
