using Geopilot.Api.FileAccess;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Processing;

/// <summary>
/// Background worker that consumes pipelines from the <see cref="IProcessingJobStore.ProcessingQueue"/>
/// and runs them. A step's user-downloadable files (<see cref="OutputAction.Download"/>) and its visualization
/// configs (<see cref="OutputAction.MapVisualization"/>, <see cref="OutputAction.TreeVisualization"/>) are
/// extracted to disk as soon as that step finishes, via <see cref="IPipeline.OnStepCompleted"/>, so they are
/// available while later steps still run and regardless of whether the run ultimately succeeds. Delivery payload
/// files (<see cref="OutputAction.Delivery"/>) are extracted once, only when the run finished successfully and
/// delivery is allowed. They populate <see cref="IPipelineStep.Downloads"/>, <see cref="IPipelineStep.Visualizations"/>
/// and <see cref="IPipelineStep.DeliveryFiles"/> respectively.
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
        await Parallel.ForEachAsync(jobStore.ProcessingQueue.ReadAllAsync(stoppingToken), stoppingToken, async (workItem, cancellationToken) =>
        {
            var pipeline = workItem.Pipeline;
            using var timeoutCts = new CancellationTokenSource(processingOptions.JobTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Persist each step's downloadable files the moment the step finishes, so the supplier can
            // download them while later steps run (and so they survive a later step failing or timing out).
            pipeline.OnStepCompleted = (step, stepResult, stepCancellationToken) =>
            {
                ExtractStepDownloads(pipeline.JobId, step, stepResult);
                return Task.CompletedTask;
            };

            try
            {
                var pipelineContext = await pipeline.Run(workItem.Files, linkedCts.Token);

                // Stage the delivery payload only when the job is actually deliverable — the same gate the
                // submission endpoint enforces (DeliveryController.Create). This keeps incomplete or
                // non-deliverable payloads (a failed/aborted pipeline, or a matched delivery restriction)
                // out of the asset store.
                if (pipeline.State == ProcessingState.Success && pipeline.Delivery == PipelineDelivery.Allow)
                    ExtractDeliveryFiles(pipeline, pipelineContext);

                jobStore.PipelineFinished(pipeline.JobId, pipeline.State);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Pipeline state is already Cancelled (set by the running step). Downloads from steps that
                // completed before the timeout were already persisted by the per-step callback; only the
                // in-flight step's files are lost, and no delivery files are persisted (delivery is staged
                // only for a successful, deliverable run).
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

    /// <summary>
    /// Persists the user-downloadable files (<see cref="OutputAction.Download"/>) produced by a single step the
    /// moment it finishes and records them on <see cref="IPipelineStep.Downloads"/>. Called once per step via
    /// <see cref="IPipeline.OnStepCompleted"/>, so a step's downloads become available before later steps run.
    /// </summary>
    internal void ExtractStepDownloads(Guid jobId, IPipelineStep step, StepResult stepResult)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var downloadFileStore = scope.ServiceProvider.GetRequiredService<IDownloadFileStore>();

        // Names are deterministic per step so they're readable on disk; collisions within the same
        // step (rare — same originalFileName twice) get a numeric suffix to avoid silent overwrites.
        var stepIdPrefix = step.Id.SanitizeFileName();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var output in stepResult.Outputs.Values)
        {
            var isDownload = output.Action.Contains(OutputAction.Download);
            var visualizationKind = ResolveVisualizationKind(output.Action);
            if (!isDownload && !visualizationKind.HasValue)
                continue;

            // A visualization config is fetched by the frontend through the same download endpoint, so it is
            // persisted to the download store as well and additionally recorded as a visualization. The file is
            // written once even when an output is tagged as both a download and a visualization.
            foreach (var transferFile in ResolveFiles(output.Data))
            {
                var fileName = MakeUniqueStepFileName(stepIdPrefix, transferFile.OriginalFileName, usedNames);
                CopyTo(downloadFileStore, jobId, fileName, transferFile);

                if (isDownload)
                    step.AddDownload(new PersistedFile(transferFile.OriginalFileName, fileName));

                if (visualizationKind.HasValue)
                    step.Visualizations.Add(new StepVisualization(visualizationKind.Value, transferFile.OriginalFileName, fileName));
            }
        }
    }

    /// <summary>
    /// Persists the delivery payload files (<see cref="OutputAction.Delivery"/>) of every completed step to the
    /// asset store and records them on <see cref="IPipelineStep.DeliveryFiles"/>. Only called for a successfully
    /// completed, deliverable run (gated in <see cref="ExecuteAsync"/>). Download and delivery names are assigned
    /// independently; for a file tagged with both actions they coincide except in the rare case of two outputs
    /// sharing an original file name within one step, which is harmless because the download endpoint serves only
    /// from the download store.
    /// </summary>
    internal void ExtractDeliveryFiles(IPipeline pipeline, PipelineContext context)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var assetFileStore = scope.ServiceProvider.GetRequiredService<IAssetFileStore>();

        foreach (var step in pipeline.Steps)
        {
            if (!context.StepResults.TryGetValue(step.Id, out var stepResult))
                continue;

            var stepIdPrefix = step.Id.SanitizeFileName();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var output in stepResult.Outputs.Values)
            {
                if (!output.Action.Contains(OutputAction.Delivery))
                    continue;

                foreach (var transferFile in ResolveFiles(output.Data))
                {
                    var fileName = MakeUniqueStepFileName(stepIdPrefix, transferFile.OriginalFileName, usedNames);
                    CopyTo(assetFileStore, pipeline.JobId, fileName, transferFile);
                    step.AddDeliveryFile(new PersistedFile(transferFile.OriginalFileName, fileName));
                }
            }
        }
    }

    private static IEnumerable<IPipelineFile> ResolveFiles(object? data) => data switch
    {
        IPipelineFileList fileList => fileList.Files,
        IPipelineFile[] fileArray => fileArray,
        IPipelineFile singleFile => [singleFile],
        _ => [],
    };

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

        if (actions.Contains(OutputAction.MapVisualization))
            return VisualizationKind.Map;

        return null;
    }

    private static void CopyTo(IJobFileStore store, Guid jobId, string fileName, IPipelineFile source)
    {
        using var outStream = store.CreateFile(jobId, fileName);
        using var inStream = source.OpenReadFileStream();
        inStream.CopyTo(outStream);
    }
}
