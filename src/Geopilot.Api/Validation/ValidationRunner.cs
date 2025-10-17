using Microsoft.Extensions.Options;

namespace Geopilot.Api.Validation;

/// <summary>
/// Runs validation jobs in the background.
/// </summary>
public class ValidationRunner : BackgroundService
{
    private readonly ILogger<ValidationRunner> logger;
    private readonly IValidationJobStore jobStore;
    private readonly ValidationOptions validationOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationRunner"/> class.
    /// </summary>
    public ValidationRunner(ILogger<ValidationRunner> logger, IValidationJobStore jobStore, IOptions<ValidationOptions> validationOptions)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);

        this.logger = logger;
        this.jobStore = jobStore;
        this.validationOptions = validationOptions.Value;
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

            var validatorTimeout = validationOptions.ValidatorTimeouts.GetValueOrDefault(validator.Name, TimeSpan.FromHours(12));
            using var timeoutCts = new CancellationTokenSource(validatorTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                result = await validator.ExecuteAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogWarning("Validator <{Validator}> timed out.", validator.Name);
                result = new ValidatorResult(ValidatorResultStatus.Failed, $"Validation timed out.");
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

            try
            {
                jobStore.AddValidatorResult(validator, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add validation result for validator <{Validator}>.", validator.Name);
            }
        });
    }
}
