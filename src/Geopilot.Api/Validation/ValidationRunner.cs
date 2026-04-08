using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
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
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationRunner"/> class.
    /// </summary>
    public ValidationRunner(
        ILogger<ValidationRunner> logger,
        IValidationJobStore jobStore,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ValidationOptions> validationOptions)
    {
        ArgumentNullException.ThrowIfNull(validationOptions);

        this.logger = logger;
        this.jobStore = jobStore;
        this.serviceScopeFactory = serviceScopeFactory;
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
                result = MapToValidatorResult(pipeline, pipelineContext);
                jobStore.AddValidatorResult(pipeline, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while running pipeline <{Pipeline}>.", pipeline.Id);
                result = new ValidatorResult(ValidatorResultStatus.Failed, $"An unexpected error occured while running pipeline <{pipeline.Id}>.");
                jobStore.AddValidatorResult(pipeline, result);
            }
            finally
            {
                pipeline.Dispose();
            }
        });
    }

    private ValidatorResult MapToValidatorResult(IPipeline pipeline, PipelineContext context)
    {
        var status = MapPipelineStatusToValidatorResultStatus(pipeline, context);
        var files = ExtractPersistentFiles(pipeline.JobId, context);
        return new ValidatorResult(status, GetStatusMessage(pipeline, context), files.ToImmutableDictionary());
    }

    private string GetStatusMessage(IPipeline pipeline, PipelineContext context)
    {
        var statusMessages = context.StepResults
            .SelectMany(r =>
            {
                var messages = new List<string>();
                foreach (var output in r.Value.Outputs)
                {
                    var pipelineStep = pipeline.Steps
                        .Where(s => s.Id == r.Key)
                        .FirstOrDefault();
                    if (pipelineStep != null && output.Value.Action.Contains(OutputAction.StatusMessage) && output.Value.Data is Dictionary<string, string> statusMessage)
                        messages.Add(GetLocalizedName(pipelineStep.DisplayName) + ": " + GetLocalizedName(statusMessage));
                }

                return messages;
            })
            .ToList();
        if (statusMessages != null && statusMessages.Count > 0)
            return string.Join(" - ", statusMessages);
        return string.Empty;
    }

    private static string GetLocalizedName(Dictionary<string, string> displayName)
    {
        return displayName.TryGetValue("en", out string? name) ? name : displayName.FirstOrDefault().Value;
    }

    private static ValidatorResultStatus MapPipelineStatusToValidatorResultStatus(IPipeline pipeline, PipelineContext context)
    {
        if (pipeline.Delivery == PipelineDelivery.Prevent)
        {
            return ValidatorResultStatus.CompletedWithErrors;
        }

        var pipelineState = pipeline.State;
        return pipelineState switch
        {
            PipelineState.Success => ValidatorResultStatus.Completed,
            PipelineState.Failed => ValidatorResultStatus.Failed,
            _ => throw new InvalidOperationException($"Unexpected pipeline state: {pipelineState}"),
        };
    }

    private Dictionary<string, string> ExtractPersistentFiles(Guid jobId, PipelineContext context)
    {
        var downloadFiles = new Dictionary<string, string>();
        using var scope = serviceScopeFactory.CreateScope();
        var fileProvider = scope.ServiceProvider.GetRequiredService<IFileProvider>();
        fileProvider.Initialize(jobId);

        foreach (var stepResult in context.StepResults.Values)
        {
            foreach (var (outputKey, output) in stepResult.Outputs)
            {
                if ((output.Action.Contains(OutputAction.Download) || output.Action.Contains(OutputAction.Delivery)) && output.Data is IPipelineFile transferFile)
                {
                    using (FileHandle fileHandle = fileProvider.CreateFileWithRandomName(transferFile.FileExtension))
                    using (Stream inStream = transferFile.OpenReadFileStream())
                    {
                        inStream.CopyTo(fileHandle.Stream);
                        downloadFiles[outputKey] = fileHandle.FileName;
                    }
                }
            }
        }

        return downloadFiles;
    }
}
