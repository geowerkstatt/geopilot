using Geopilot.Api.FileAccess;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
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
                jobStore.PipelineFinished(pipeline.JobId, pipeline.State);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Pipeline state is already Cancelled (set by the running step). Pipeline.Run threw
                // before returning the context, so any partial step results are unreachable for
                // file extraction here; the pre-timeout files are not persisted.
                logger.LogError("Pipeline <{Pipeline}> timed out after {Timeout}.", pipeline.Id, processingOptions.JobTimeout);
                jobStore.PipelineFinished(pipeline.JobId, ProcessingState.Cancelled);
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
        var assetFileStore = scope.ServiceProvider.GetRequiredService<IAssetFileStore>();
        var downloadFileStore = scope.ServiceProvider.GetRequiredService<IDownloadFileStore>();

        foreach (var step in pipeline.Steps)
        {
            if (!context.StepResults.TryGetValue(step.Id, out var stepResult))
                continue;

            // Names are deterministic per step so they're readable on disk; collisions
            // within the same step (rare — same originalFileName twice) get a numeric
            // suffix to avoid silent overwrites.
            var stepIdPrefix = step.Id.SanitizeFileName();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var output in stepResult.Outputs.Values)
            {
                var isDownload = output.Action.Contains(OutputAction.Download);
                var isDelivery = output.Action.Contains(OutputAction.Delivery);
                var visualizationKind = ResolveVisualizationKind(output.Action);
                var isVisualization = visualizationKind.HasValue;
                if (!isDownload && !isDelivery && !isVisualization)
                    continue;

                IEnumerable<IPipelineFile> files = output.Data switch
                {
                    IPipelineFileList fileList => fileList.Files,
                    IPipelineFile[] fileArray => fileArray,
                    IPipelineFile singleFile => [singleFile],
                    _ => [],
                };

                // Both stores are filled independently so each can be cleaned on its own
                // retention. A file tagged with both actions is written to both under the
                // same name — the download endpoint can fall back to the asset copy after
                // the download retention expires. A file tagged with a visualization action
                // is also written to the download store (the frontend fetches it through the
                // existing download endpoint) and exposed as a visualization entry.
                foreach (var transferFile in files)
                {
                    var fileName = MakeUniqueStepFileName(stepIdPrefix, transferFile.OriginalFileName, usedNames);
                    var persisted = new PersistedFile(transferFile.OriginalFileName, fileName);

                    if (isDelivery)
                    {
                        CopyTo(assetFileStore, pipeline.JobId, fileName, transferFile);
                        step.DeliveryFiles.Add(persisted);
                    }

                    if (isDownload || isVisualization)
                    {
                        CopyTo(downloadFileStore, pipeline.JobId, fileName, transferFile);
                    }

                    if (isDownload)
                    {
                        step.Downloads.Add(persisted);
                    }

                    if (visualizationKind.HasValue)
                    {
                        step.Visualizations.Add(new StepVisualization(
                            visualizationKind.Value,
                            transferFile.OriginalFileName,
                            fileName));
                    }
                }
            }
        }
    }

    private string MakeUniqueStepFileName(string stepIdPrefix, string originalFileName, HashSet<string> usedNames)
    {
        var baseName = $"{stepIdPrefix}_{originalFileName.SanitizeFileName()}";
        if (usedNames.Add(baseName))
            return baseName;

        var stem = Path.GetFileNameWithoutExtension(baseName);
        var extension = Path.GetExtension(baseName);
        for (var counter = 2; counter < int.MaxValue; counter++)
        {
            var candidate = $"{stem}_{counter}{extension}";
            if (usedNames.Add(candidate))
            {
                logger.LogWarning(
                    "Duplicate output filename in step <{Step}>: <{Original}>. Persisting as <{Final}>.",
                    stepIdPrefix,
                    originalFileName,
                    candidate);
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not generate a unique on-disk name for <{originalFileName}> in step <{stepIdPrefix}>.");
    }

    private static VisualizationKind? ResolveVisualizationKind(IReadOnlyCollection<OutputAction> actions)
    {
        if (actions.Contains(OutputAction.TreeVisualization))
            return VisualizationKind.Tree;

        return null;
    }

    private static void CopyTo(IJobFileStore store, Guid jobId, string fileName, IPipelineFile source)
    {
        using var outStream = store.CreateFile(jobId, fileName);
        using var inStream = source.OpenReadFileStream();
        inStream.CopyTo(outStream);
    }
}
