using Geopilot.Api.Exceptions;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using System.Threading.Channels;

namespace Geopilot.Api.Services;

/// <summary>
/// Background worker that processes upload preflight checks.
/// Reads <see cref="PreflightRequest"/> messages from the channel and runs
/// verify, scan and queue for processing for each job. The uploaded files stay in the upload storage;
/// each one is fetched the first time a step reads it. A host shutdown ends the job whose preflight it cuts short
/// and every job still queued with <see cref="RunOutcome.ShutDown"/>, as it ends a run.
/// </summary>
public class PreflightBackgroundService : BackgroundService
{
    private readonly ChannelReader<PreflightRequest> preflightQueue;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<PreflightBackgroundService> logger;
    private readonly JobLifecycle lifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreflightBackgroundService"/> class.
    /// </summary>
    public PreflightBackgroundService(
        ChannelReader<PreflightRequest> preflightQueue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PreflightBackgroundService> logger,
        JobLifecycle lifecycle)
    {
        this.preflightQueue = preflightQueue;
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
        this.lifecycle = lifecycle;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endedByShutdown = new List<PreflightRequest>();
        try
        {
            await foreach (var request in preflightQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessRequestAsync(request, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    endedByShutdown.Add(request);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The shutdown ended the wait for the next request.
        }

        // No preflight begins once the host shuts down, so the queued requests end together with the one it cut short.
        while (preflightQueue.TryRead(out var request))
            endedByShutdown.Add(request);

        await Task.WhenAll(endedByShutdown.Select(request => EndByShutdownAsync(request, stoppingToken)));
    }

    /// <summary>
    /// Processes a single preflight request: runs the checks and queues the job for processing. A host shutdown that
    /// cuts it short ends in an <see cref="OperationCanceledException"/> and leaves the job pending for
    /// <see cref="ExecuteAsync"/> to end.
    /// </summary>
    internal async Task ProcessRequestAsync(PreflightRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = serviceScopeFactory.CreateScope();
        var jobStore = scope.ServiceProvider.GetRequiredService<IProcessingJobStore>();
        var orchestrationService = scope.ServiceProvider.GetRequiredService<IUploadOrchestrationService>();

        var job = jobStore.GetJob(request.JobId);
        if (job == null || job.State != ProcessingState.Pending)
        {
            logger.LogWarning("Skipping preflight for job <{JobId}>: job is null or no longer pending.", request.JobId);
            return;
        }

        ScanResult? scanResult = null;
        try
        {
            scanResult = await orchestrationService.RunPreflightChecksAsync(request.UploadId, cancellationToken);

            // Nothing is transferred here: each file is fetched from the upload storage the first time a step
            // reads it, so the job starts without waiting for the whole upload.
            var pipelineFiles = orchestrationService.RegisterJobFiles(request.UploadId, request.JobId);
            await lifecycle.PreflightPassedAsync(request.JobId, scanResult, pipelineFiles);

            logger.LogInformation("Preflight complete for job <{JobId}>. Pipeline queued.", request.JobId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown: nothing is wrong with the upload, so the preflight must not fail the job.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preflight failed for job <{JobId}>.", request.JobId);

            var failureReason = ex is UploadPreflightException preflightException
                ? $"{preflightException.FailureReason}: {preflightException.Message}"
                : ex.Message;

            await lifecycle.PreflightFailedAsync(request.JobId, failureReason, scanResult ?? (ex as UploadPreflightException)?.ScanResult);
        }
    }

    /// <summary>
    /// Ends the job of a request whose preflight the host shutdown cut short or never let begin, the way the shutdown
    /// ends a run: <see cref="ProcessingState.Cancelled"/>, with its upload kept for another start.
    /// </summary>
    private async Task EndByShutdownAsync(PreflightRequest request, CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<IProcessingJobStore>().GetJob(request.JobId);
        if (job is not { State: ProcessingState.Pending, Pipeline: { } pipeline })
        {
            logger.LogWarning("Not ending job <{JobId}> for the host shutdown: job is null, no longer pending or has no pipeline.", request.JobId);
            return;
        }

        logger.LogInformation("Preflight for job <{JobId}> cancelled due to host shutdown.", request.JobId);
        await lifecycle.RunFinishedAsync(pipeline, new RunOutcome.ShutDown(), stoppingToken);
    }
}
