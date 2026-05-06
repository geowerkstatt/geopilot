using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Processing;

/// <summary>
/// Background worker that consumes pipelines from the <see cref="IProcessingJobStore.ProcessingQueue"/>
/// and runs them. After each run, files produced by the pipeline are extracted to disk and split into
/// <see cref="IPipelineStep.Downloads"/> (user-downloadable) and <see cref="IPipelineStep.DeliveryFiles"/>
/// (delivery payload) according to each output's <see cref="OutputAction"/> tags.
/// </summary>
public class ProcessingRunner : BackgroundService
{
    private readonly ILogger<ProcessingRunner> logger;
    private readonly IProcessingJobStore jobStore;
    private readonly ProcessingOptions processingOptions;
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingRunner"/> class.
    /// </summary>
    public ProcessingRunner(
        ILogger<ProcessingRunner> logger,
        IProcessingJobStore jobStore,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.logger = logger;
        this.jobStore = jobStore;
        this.serviceScopeFactory = serviceScopeFactory;
        this.processingOptions = processingOptions.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(jobStore.ProcessingQueue.ReadAllAsync(stoppingToken), stoppingToken, async (pipeline, cancellationToken) =>
        {
            using var timeoutCts = new CancellationTokenSource(processingOptions.JobTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var pipelineContext = await pipeline.Run(linkedCts.Token);
                ExtractPersistentFiles(pipeline, pipelineContext);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Pipeline state is already Cancelled (set by the running step). Pipeline.Run threw
                // before returning the context, so any partial step results are unreachable for
                // file extraction here; the pre-timeout files are not persisted.
                logger.LogError("Pipeline <{Pipeline}> timed out after {Timeout}.", pipeline.Id, processingOptions.JobTimeout);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Host shutdown — leave the pipeline state as-is and let the cleanup service take over.
                logger.LogInformation("Pipeline <{Pipeline}> cancelled due to host shutdown.", pipeline.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while running pipeline <{Pipeline}>.", pipeline.Id);
                jobStore.MarkAsFailed(pipeline.JobId);
            }
            finally
            {
                // Free process-owned resources (e.g. HttpClient) immediately. Pipeline state, step
                // states, status-message dictionaries, and PersistedDownloads survive disposal —
                // only the pipeline's temp directory is removed, which we no longer need.
                pipeline.Dispose();
            }
        });
    }

    private void ExtractPersistentFiles(IPipeline pipeline, PipelineContext context)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var fileProvider = scope.ServiceProvider.GetRequiredService<IFileProvider>();
        fileProvider.Initialize(pipeline.JobId);

        foreach (var step in pipeline.Steps)
        {
            if (!context.StepResults.TryGetValue(step.Id, out var stepResult))
                continue;

            foreach (var output in stepResult.Outputs.Values)
            {
                var isDownload = output.Action.Contains(OutputAction.Download);
                var isDelivery = output.Action.Contains(OutputAction.Delivery);
                if (!isDownload && !isDelivery)
                    continue;

                if (output.Data is not IPipelineFile transferFile)
                    continue;

                // Persist the file once, then reference it from whichever lists apply. A file
                // tagged with both actions ends up in both Downloads and DeliveryFiles.
                using var fileHandle = fileProvider.CreateFileWithRandomName(transferFile.FileExtension);
                using var inStream = transferFile.OpenReadFileStream();
                inStream.CopyTo(fileHandle.Stream);
                var persisted = new PersistedFile(transferFile.OriginalFileName, fileHandle.FileName);

                if (isDownload)
                    step.Downloads.Add(persisted);
                if (isDelivery)
                    step.DeliveryFiles.Add(persisted);
            }
        }
    }
}
