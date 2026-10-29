using Geopilot.Api.FileAccess;
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
/// payload files (<see cref="OutputAction.Delivery"/>) are staged once, only when the run finished successfully and
/// delivery is allowed; declaring the delivery promotes them into the asset store. They populate
/// <see cref="IPipelineStep.Downloads"/>, <see cref="IPipelineStep.Visualizations"/> and
/// <see cref="IPipelineStep.DeliveryFiles"/> respectively. However a run ends, the runner reports its outcome to
/// <see cref="JobLifecycle"/>, which carries out what follows for the job.
/// </summary>
public class ProcessingRunner : BackgroundService
{
    private readonly ILogger<ProcessingRunner> logger;
    private readonly IProcessingJobStore jobStore;
    private readonly JobLifecycle lifecycle;
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
        JobLifecycle lifecycle,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.logger = logger;
        this.jobStore = jobStore;
        this.lifecycle = lifecycle;
        this.serviceScopeFactory = serviceScopeFactory;
        this.processingOptions = processingOptions.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The stopping token only ends the reading of new work. Running jobs get the drain window on top before
        // they are cancelled; with the default of zero that happens the moment the shutdown begins.
        using var drainCts = new CancellationTokenSource();
        using var drainRegistration = stoppingToken.Register(() => drainCts.CancelAfter(processingOptions.ShutdownDrainTimeout));
        var cancellationToken = drainCts.Token;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = processingOptions.MaxConcurrentJobs,
            CancellationToken = stoppingToken,
        };

        // The body ignores the token Parallel.ForEachAsync hands it: that one is cancelled as soon as reading the
        // queue stops, while ForEachAsync still awaits the bodies in flight, which is what gives them the window.
        await Parallel.ForEachAsync(jobStore.ProcessingQueue.ReadAllAsync(stoppingToken), parallelOptions, async (workItem, _) =>
        {
            var pipeline = workItem.Pipeline;
            using var timeoutCts = new CancellationTokenSource(processingOptions.JobTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Record each step in the execution protocol the moment it is reached, so an interrupted
            // run shows which step was in flight.
            pipeline.OnStepStarted = (step, stepCancellationToken)
                => RecordProtocolAsync(pipeline.JobId, recorder => recorder.RecordStepStartedAsync(pipeline.JobId, step, pipeline.Steps.IndexOf(step)));

            // Persist each step's downloadable files the moment the step finishes, so the supplier can
            // download them while later steps run (and so they survive a later step failing or timing out).
            // Recorded afterwards, so the protocol sees the persisted artifact names.
            pipeline.OnStepCompleted = async (step, stepResult, stepCancellationToken) =>
            {
                await ExtractStepDownloadsAsync(pipeline.JobId, step, stepResult, stepCancellationToken);
                await RecordProtocolAsync(pipeline.JobId, recorder => recorder.RecordStepCompletedAsync(pipeline.JobId, step, pipeline.Steps.IndexOf(step)));
            };

            // The outcome is collected per branch and reported once below. A hard kill leaves the run without
            // terminal state on purpose: that is how restart victims stay countable.
            RunOutcome outcome;
            try
            {
                var pipelineContext = await pipeline.Run(workItem.Files, linkedCts.Token);

                // Stage the delivery payload only when the run is deliverable and somebody can declare it, by the
                // same rules the declaration applies to the job (DeliveryDeclarationService). The job only turns
                // deliverable once RunFinishedAsync below sets its state, so a declaration never promotes a partial
                // staging. It goes to the staging store, not the asset store: the asset directory holds deliveries
                // only, and declaring one promotes the staged files (AssetHandler.RecordJobAssetsAsync).
                if (pipeline.State.IsDeliverable() && workItem.IsDeclarable)
                    await ExtractDeliveryFilesAsync(pipeline, pipelineContext, linkedCts.Token);

                outcome = new RunOutcome.Completed(pipeline.State);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // The timeout can hit a step or the delivery staging, so the pipeline may still report Success;
                // the job ends Cancelled either way, which keeps a partial staging undeliverable. Downloads of
                // steps finished before the timeout were already persisted by the per-step callback.
                logger.LogError("Pipeline <{Pipeline}> timed out after {Timeout}.", pipeline.Id, processingOptions.JobTimeout);
                outcome = new RunOutcome.TimedOut();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Host shutdown. The protocol write in RunFinishedAsync is best effort: it distinguishes a graceful
                // stop (Cancelled, "host shutdown") from a hard kill (no terminal state at all).
                logger.LogInformation("Pipeline <{Pipeline}> cancelled due to host shutdown.", pipeline.Id);
                outcome = new RunOutcome.ShutDown();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while running pipeline <{Pipeline}>.", pipeline.Id);
                outcome = new RunOutcome.Crashed(ex.Message);
            }

            // On the shutdown token, not the linked one: the job timeout bounds the run, and what follows starts
            // once the run is over, so a nearly exhausted timeout must not cut the declaration short. It never
            // throws, which matters here: an exception escaping the body of a Parallel.ForEachAsync stops the
            // queue from draining and, by default, the host.
            await lifecycle.RunFinishedAsync(pipeline, outcome, cancellationToken);
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
    /// Stages the delivery payload files (<see cref="OutputAction.Delivery"/>) of every completed step in the
    /// staging store, from where declaring the delivery promotes them into the asset store, and records them on
    /// <see cref="IPipelineStep.DeliveryFiles"/>. Only called for a successfully
    /// completed, deliverable run (gated in <see cref="ExecuteAsync"/>). A delivery file keeps its original name,
    /// without the step prefix downloads carry, because the names of a delivery are often agreed with the data
    /// supplier. The names are unique across the whole delivery, since all of its files share one directory: in the
    /// order of the steps and their outputs, the first file keeps its name and every further one gets a counter.
    /// Each delivery file's origin is traced back via <see cref="PipelineExtensions.UnwrapOrigin"/>
    /// so a file that entered as an upload can be told apart from one produced by a step.
    /// </summary>
    internal async Task ExtractDeliveryFilesAsync(IPipeline pipeline, PipelineContext context, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var stagingFileStore = scope.ServiceProvider.GetRequiredService<IAssetStagingFileStore>();

        // Case-insensitive, because File.pdf and file.pdf address the same file on Windows and macOS.
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in pipeline.Steps)
        {
            if (!context.StepResults.TryGetValue(step.Id, out var stepResult))
                continue;

            foreach (var outputAction in step.OutputActions)
            {
                if (!outputAction.Actions.Contains(OutputAction.Delivery))
                    continue;

                var data = stepResult.ExtractProperty(outputAction.Property);

                foreach (var deliveryFile in ResolveFiles(data))
                {
                    var fileName = MakeUniqueDeliveryFileName(step.Id, deliveryFile.OriginalFileName, usedNames);
                    await CopyToAsync(stagingFileStore, pipeline.JobId, fileName, deliveryFile, cancellationToken);
                    var fromUpload = context.Upload.Contains(deliveryFile.UnwrapOrigin(), ReferenceEqualityComparer.Instance);

                    // The stored name twice on purpose: a delivered file is shown under the name it is stored under.
                    step.AddDeliveryFile(new PersistedFile(fileName, fileName, fromUpload));
                }
            }
        }
    }

    /// <summary>
    /// Runs one protocol write against a fresh scope. The recorder swallows its own write failures;
    /// this additionally guards scope creation and resolution, because a protocol problem must never
    /// tear down the running pipeline.
    /// </summary>
    private async Task RecordProtocolAsync(Guid jobId, Func<IPipelineRunRecorder, Task> record)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var recorder = scope.ServiceProvider.GetRequiredService<IPipelineRunRecorder>();
            await record(recorder);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Writing the execution protocol for job <{JobId}> failed; the job continues.", jobId);
        }
    }

    private static IEnumerable<IPipelineFile> ResolveFiles(object? data) => data switch
    {
        IEnumerable<IPipelineFile> files => files,
        IPipelineFile singleFile => [singleFile],
        _ => [],
    };

    private string MakeUniqueDeliveryFileName(string stepId, string originalFileName, HashSet<string> usedNames)
    {
        var fileName = OriginalFileNaming.MakeUnique(originalFileName, usedNames);
        if (fileName != originalFileName.SanitizeFileName())
        {
            logger.LogWarning(
                "Duplicate delivery filename <{Original}> in step <{Step}>. Persisting as <{Final}>.",
                originalFileName,
                stepId,
                fileName);
        }

        return fileName;
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
