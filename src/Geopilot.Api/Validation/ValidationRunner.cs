using Geopilot.Api.Pipeline;
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
    /// Processes <see cref="IPipeline"/> instances retrieved from the <see cref="ValidationJobStore"/> in parallel.
    /// </summary>
    /// <remarks>For every <see cref="IPipeline"/> processed, a <see cref="ValidatorResult"/> is created and delivered to the <see cref="ValidationJobStore"/>.</remarks>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that is used to signal the operation should stop.</param>
    /// <returns>A task that represents the asynchronous execution of the validation process.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(jobStore.ValidationQueue.ReadAllAsync(stoppingToken), stoppingToken, async (pipeline, cancellationToken) =>
        {
            ValidatorResult? result = null;

            var validatorTimeout = validationOptions.PipelineTimeouts.GetValueOrDefault(pipeline.Id, TimeSpan.FromHours(12));
            using var timeoutCts = new CancellationTokenSource(validatorTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var pipelineContext = await pipeline.Run(linkedCts.Token);
                result = CreateValidatorResult(pipeline, pipelineContext);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogWarning("Pipeline <{Pipeline}> timed out.", pipeline.Id);
                result = new ValidatorResult(ValidatorResultStatus.Failed, $"Validation timed out.");
            }
            catch (Exception ex) when (ex is ValidationFailedException)
            {
                logger.LogError(ex, "Pipeline <{Pipeline}> failed.", pipeline.Id);
                result = new ValidatorResult(ValidatorResultStatus.Failed, ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while running pipeline <{Pipeline}>.", pipeline.Id);
                result = new ValidatorResult(ValidatorResultStatus.Failed, $"An unexpected error occured while running pipeline <{pipeline.Id}>.");
            }

            try
            {
                jobStore.AddValidatorResult(pipeline, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add validation result for pipeline <{Pipeline}>.", pipeline.Id);
            }
        });
    }

    private ValidatorResult CreateValidatorResult(IPipeline pipeline, PipelineContext context)
    {
        return new ValidatorResult
        {
            Status = PipelineStatusToValidatorResultStatus(pipeline.State),
            StatusMessage = $"Pipeline completed with state: {pipeline.State}",
        };
    }

    private ValidatorResultStatus PipelineStatusToValidatorResultStatus(PipelineState pipelineState)
    {
        return pipelineState switch
        {
            PipelineState.Success => ValidatorResultStatus.Completed,
            PipelineState.Failed => ValidatorResultStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(pipelineState), $"Unexpected pipeline state: {pipelineState}"),
        };
    }
}
