namespace Geopilot.Api.Validation;

/// <summary>
/// Runs validation jobs in the background.
/// </summary>
public class ValidationRunner : BackgroundService
{
    private readonly ILogger<ValidationRunner> logger;
    private readonly IValidationJobStore jobStore;
    private readonly TimeSpan validatorTimeoutHours;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationRunner"/> class.
    /// </summary>
    public ValidationRunner(ILogger<ValidationRunner> logger, IValidationJobStore jobStore, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        this.logger = logger;
        this.jobStore = jobStore;

        var section = configuration.GetSection("Validation");
        validatorTimeoutHours = TimeSpan.FromHours(section.GetValue<double>("ValidatorTimeoutHours", 12));
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

            using var timeoutCts = new CancellationTokenSource(validatorTimeoutHours);
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
