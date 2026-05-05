using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Processing;

/// <summary>
/// Background worker that consumes pipelines from the <see cref="IProcessingJobStore.ProcessingQueue"/>
/// and runs them. After each run, persistent download files produced by the pipeline are extracted to
/// disk and tracked on each step via <see cref="IPipelineStep.PersistedDownloads"/>.
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
                await pipeline.Run(linkedCts.Token);
                ExtractPersistentFiles(pipeline);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Pipeline state is already Cancelled (set by the running step). Persist whatever
                // partial outputs the pipeline produced before the timeout fired.
                logger.LogError("Pipeline <{Pipeline}> timed out after {Timeout}.", pipeline.Id, processingOptions.JobTimeout);
                ExtractPersistentFiles(pipeline);
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

    private void ExtractPersistentFiles(IPipeline pipeline)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var fileProvider = scope.ServiceProvider.GetRequiredService<IFileProvider>();
        fileProvider.Initialize(pipeline.JobId);

        foreach (var step in pipeline.Steps)
        {
            if (step.Result == null)
                continue;

            foreach (var output in step.Result.Outputs.Values)
            {
                if (!(output.Action.Contains(OutputAction.Download) || output.Action.Contains(OutputAction.Delivery)))
                    continue;

                if (output.Data is not IPipelineFile transferFile)
                    continue;

                using var fileHandle = fileProvider.CreateFileWithRandomName(transferFile.FileExtension);
                using var inStream = transferFile.OpenReadFileStream();
                inStream.CopyTo(fileHandle.Stream);
                step.PersistedDownloads.Add(new PersistedDownload(transferFile.OriginalFileName, fileHandle.FileName));
            }
        }
    }
}
