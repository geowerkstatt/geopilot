using Geopilot.Api.FileAccess;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Processing;

/// <summary>
/// Background worker that consumes pipelines from the <see cref="IProcessingJobStore.ProcessingQueue"/>
/// and runs them. A step's user-downloadable files (<see cref="OutputAction.Download"/>) are extracted to the
/// download store, and its visualization configs (<see cref="OutputAction.Visualization"/>) are serialized to
/// JSON in the dedicated visualization store, as soon as that step finishes via <see cref="IPipeline.OnStepCompleted"/>,
/// so they are available while later steps still run and regardless of whether the run ultimately succeeds. Delivery
/// payload files (<see cref="OutputAction.Delivery"/>) are extracted once, only when the run finished successfully and
/// delivery is allowed. They populate <see cref="IPipelineStep.Downloads"/>, <see cref="IPipelineStep.Visualizations"/>
/// and <see cref="IPipelineStep.DeliveryFiles"/> respectively.
/// </summary>
public class ProcessingRunner : BackgroundService
{
    private readonly ILogger<ProcessingRunner> logger;
    private readonly IProcessingJobStore jobStore;
    private readonly ProcessingOptions processingOptions;
    private readonly IServiceScopeFactory serviceScopeFactory;

    // Internal so tests can pin the wire format the frontend depends on.
    internal static readonly JsonSerializerOptions VisualizationJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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
            pipeline.OnStepCompleted = (step, stepResult, stepCancellationToken)
                => ExtractStepDownloadsAsync(pipeline.JobId, step, stepResult, stepCancellationToken);

            try
            {
                var pipelineContext = await pipeline.Run(workItem.Files, linkedCts.Token);

                // Stage the delivery payload only when the job is actually deliverable — the same gate the
                // submission endpoint enforces (DeliveryController.Create). This keeps incomplete or
                // non-deliverable payloads (a failed/aborted pipeline, or a step that restricts delivery)
                // out of the asset store.
                if (pipeline.State.IsDeliverable())
                    await ExtractDeliveryFilesAsync(pipeline, pipelineContext, linkedCts.Token);

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
                // Both cleanup steps run unconditionally and each one is guarded on its own. This is the
                // body of a Parallel.ForEachAsync inside a BackgroundService: an exception escaping here
                // aborts the whole loop, so the queue stops draining, and by default it stops the host.
                // A shared guard would make releasing the upload depend on the disposal succeeding.
                try
                {
                    // Free process-owned resources (e.g. HttpClient) immediately. Pipeline state, step
                    // states, status-message dictionaries and PersistedDownloads survive disposal; what
                    // goes is the pipeline's temp directory, including the uploaded files fetched into it
                    // during the run.
                    pipeline.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to dispose pipeline <{Pipeline}>.", pipeline.Id);
                }

                try
                {
                    await ReleaseUploadIfNotDeliverableAsync(pipeline.JobId);
                }
                catch (Exception ex)
                {
                    // The upload store is remote, so releasing it is a network call. The age-based sweep
                    // in UploadCleanupService is the backstop for blobs left behind here.
                    logger.LogError(ex, "Failed to release the upload of job <{JobId}>.", pipeline.JobId);
                }
            }
        });
    }

    /// <summary>
    /// Persists a single step's outputs the moment it finishes, via <see cref="IPipeline.OnStepCompleted"/>,
    /// so they become available before later steps run. Files tagged <see cref="OutputAction.Download"/> are
    /// copied to the download store and recorded on <see cref="IPipelineStep.Downloads"/>; objects tagged
    /// <see cref="OutputAction.Visualization"/> are serialized to JSON in the visualization store and recorded
    /// on <see cref="IPipelineStep.Visualizations"/>.
    /// </summary>
    internal async Task ExtractStepDownloadsAsync(Guid jobId, IPipelineStep step, StepResult stepResult, CancellationToken cancellationToken = default)
    {
        // A skipped or pre-failed step produces no process result
        if (stepResult.Result is null)
            return;

        using var scope = serviceScopeFactory.CreateScope();
        var downloadFileStore = scope.ServiceProvider.GetRequiredService<IDownloadFileStore>();
        var visualizationFileStore = scope.ServiceProvider.GetRequiredService<IVisualizationFileStore>();

        // Names are deterministic per step so they're readable on disk; collisions within the same store get a
        // numeric suffix to avoid silent overwrites. Downloads and visualizations live in separate stores and
        // track their used names independently.
        var stepIdPrefix = step.Id.SanitizeFileName();
        var usedDownloadNames = new HashSet<string>(StringComparer.Ordinal);
        var usedVisualizationNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var outputAction in step.OutputActions)
        {
            var data = stepResult.ExtractProperty(outputAction.Property);

            if (outputAction.Actions.Contains(OutputAction.Download))
            {
                if (!(data is IPipelineFile || data is IEnumerable<IPipelineFile>))
                {
                    var errorMessage = $"job <{jobId}>, step <{step.Id}>: Download output action references property <{outputAction.Property}>. this has to be a IPipelineFile or a IEnumerable<IPipelineFile> but is <{data?.GetType()}>";
                    logger.LogError(errorMessage);
                    throw new PipelineRunException(errorMessage);
                }

                foreach (var transferFile in ResolveFiles(data))
                {
                    var fileName = MakeUniqueStepFileName(stepIdPrefix, transferFile.OriginalFileName, usedDownloadNames);
                    await CopyToAsync(downloadFileStore, jobId, fileName, transferFile, cancellationToken);
                    step.AddDownload(new PersistedFile(transferFile.OriginalFileName, fileName));
                }
            }

            if (outputAction.Actions.Contains(OutputAction.Visualization))
            {
                if (data is not IVisualization)
                {
                    var errorMessage = $"job <{jobId}>, step <{step.Id}>: Visualization output action references property <{outputAction.Property}>. this has to be a IVisualization but is <{data?.GetType()}>";
                    logger.LogError(errorMessage);
                    throw new PipelineRunException(errorMessage);
                }

                // The visualization output value is the config object itself (not a file): serialize it to JSON
                // in the dedicated visualization store. The frontend fetches it and renders the component the
                // config's own type discriminator selects.
                var originalFileName = $"{outputAction.Property}.json";
                var fileName = MakeUniqueStepFileName(stepIdPrefix, originalFileName, usedVisualizationNames);
                SerializeVisualization(visualizationFileStore, jobId, fileName, data);
                step.AddVisualization(new StepVisualization(originalFileName, fileName));
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
    internal async Task ExtractDeliveryFilesAsync(IPipeline pipeline, PipelineContext context, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var assetFileStore = scope.ServiceProvider.GetRequiredService<IAssetFileStore>();

        foreach (var step in pipeline.Steps)
        {
            if (!context.StepResults.TryGetValue(step.Id, out var stepResult))
                continue;

            var stepIdPrefix = step.Id.SanitizeFileName();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var outputAction in step.OutputActions)
            {
                if (!outputAction.Actions.Contains(OutputAction.Delivery))
                    continue;

                var data = stepResult.ExtractProperty(outputAction.Property);

                foreach (var transferFile in ResolveFiles(data))
                {
                    var fileName = MakeUniqueStepFileName(stepIdPrefix, transferFile.OriginalFileName, usedNames);
                    await CopyToAsync(assetFileStore, pipeline.JobId, fileName, transferFile, cancellationToken);
                    step.AddDeliveryFile(new PersistedFile(transferFile.OriginalFileName, fileName));
                }
            }
        }
    }

    /// <summary>
    /// Drops the job's uploaded blobs as soon as they can no longer be needed. A run that cannot be
    /// delivered will never archive its originals, so they go right away. A deliverable run keeps them
    /// until the job is retired, because declaring the delivery archives every original as primary data.
    /// A job still in <see cref="ProcessingState.Running"/> was interrupted by a host shutdown, so its
    /// blobs stay: the in-memory job does not survive the restart, and the age-based sweep in
    /// UploadCleanupService is what eventually collects them.
    /// </summary>
    private async Task ReleaseUploadIfNotDeliverableAsync(Guid jobId)
    {
        var job = jobStore.GetJob(jobId);
        if (job is null || job.State == ProcessingState.Running || job.State.IsDeliverable())
            return;

        using var scope = serviceScopeFactory.CreateScope();
        var orchestrationService = scope.ServiceProvider.GetRequiredService<IUploadOrchestrationService>();
        await orchestrationService.ReleaseUploadAsync(job.UploadId);
    }

    private static IEnumerable<IPipelineFile> ResolveFiles(object? data) => data switch
    {
        IEnumerable<IPipelineFile> files => files,
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

    private static void SerializeVisualization(IJobFileStore store, Guid jobId, string fileName, object? data)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(data, VisualizationJsonOptions);
        using var outStream = store.CreateFile(jobId, fileName);
        outStream.Write(json);
    }

    /// <summary>
    /// Copies a step output into a job file store. The source may still have to be fetched from remote
    /// storage (a matcher passes an uploaded file through unchanged, so a matcher output tagged for
    /// download or delivery can trigger the very first fetch here), which is why the job's token has to
    /// reach this far: the caller is awaited inside <see cref="IPipeline.Run"/>, so a fetch that cannot
    /// be cancelled would outlive both the job timeout and a host shutdown.
    /// </summary>
    private static async Task CopyToAsync(IJobFileStore store, Guid jobId, string fileName, IPipelineFile source, CancellationToken cancellationToken)
    {
        using var outStream = store.CreateFile(jobId, fileName);
        using var inStream = await source.OpenReadAsync(cancellationToken);
        await inStream.CopyToAsync(outStream, cancellationToken);
    }
}
