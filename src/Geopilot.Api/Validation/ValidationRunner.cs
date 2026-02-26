using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Microsoft.Extensions.Options;
using System.Collections.Immutable;

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

            using var timeoutCts = new CancellationTokenSource(validationOptions.JobTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var pipelineContext = await pipeline.Run(linkedCts.Token);

                if (pipeline.State == PipelineState.Failed)
                {
                    throw new InvalidOperationException($"Pipeline <{pipeline.Id}> failed during execution.");
                }

                result = CreateValidatorResult(pipeline, pipelineContext);
                jobStore.AddValidatorResult(pipeline, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while running pipeline <{Pipeline}>.", pipeline.Id);
                result = new ValidatorResult(ValidatorResultStatus.Failed, $"An unexpected error occured while running pipeline <{pipeline.Id}>.");
                jobStore.AddValidatorResult(pipeline, result);
            }
        });
    }

    private static ValidatorResult CreateValidatorResult(IPipeline pipeline, PipelineContext context)
    {
        var status = PipelineStatusToValidatorResultStatus(pipeline.State, context);
        var statusMessage = context.StepResults["validation"].Outputs["status_message"].Data as string;
        var logFiles = ExtractLogFiles(context);
        return new ValidatorResult(status, statusMessage, logFiles.ToImmutableDictionary());
    }

    private static ValidatorResultStatus PipelineStatusToValidatorResultStatus(PipelineState pipelineState, PipelineContext context)
    {
        if (context.StepResults["validation"].Outputs["validation_successful"].Data is bool isSuccessful && !isSuccessful)
        {
            return ValidatorResultStatus.CompletedWithErrors;
        }

        return pipelineState switch
        {
            PipelineState.Success => ValidatorResultStatus.Completed,
            PipelineState.Failed => ValidatorResultStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(pipelineState), $"Unexpected pipeline state: {pipelineState}"),
        };
    }

    private static Dictionary<string, string> ExtractLogFiles(PipelineContext context)
    {
        var logFiles = new Dictionary<string, string>();

        foreach (var stepResult in context.StepResults.Values)
        {
            foreach (var (outputKey, output) in stepResult.Outputs)
            {
                if (output.Action.Contains(OutputAction.Download) && output.Data is IPipelineTransferFile transferFile)
                {
                    logFiles[outputKey] = transferFile.FilePath;
                }
            }
        }

        return logFiles;
    }
}
