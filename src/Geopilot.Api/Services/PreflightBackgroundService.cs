using Geopilot.Api.Exceptions;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using System.Threading.Channels;

namespace Geopilot.Api.Services;

/// <summary>
/// Background worker that processes upload preflight checks.
/// Reads <see cref="PreflightRequest"/> messages from the channel and runs
/// verify, scan and queue for processing for each job. The uploaded files stay in the upload storage;
/// each one is fetched the first time a step reads it.
/// </summary>
public class PreflightBackgroundService : BackgroundService
{
    private readonly ChannelReader<PreflightRequest> preflightQueue;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<PreflightBackgroundService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreflightBackgroundService"/> class.
    /// </summary>
    public PreflightBackgroundService(
        ChannelReader<PreflightRequest> preflightQueue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PreflightBackgroundService> logger)
    {
        this.preflightQueue = preflightQueue;
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in preflightQueue.ReadAllAsync(stoppingToken))
        {
            await ProcessRequestAsync(request, stoppingToken);
        }
    }

    /// <summary>
    /// Processes a single preflight request: runs the checks and queues the job for processing.
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

        try
        {
            await orchestrationService.RunPreflightChecksAsync(request.UploadId);

            // Nothing is transferred here: each file is fetched from the upload storage the first time a step
            // reads it, so the job starts without waiting for the whole upload.
            var pipelineFiles = orchestrationService.RegisterJobFiles(request.UploadId, request.JobId);

            jobStore.EnqueueForProcessing(request.JobId, pipelineFiles);

            logger.LogInformation("Preflight complete for job <{JobId}>. Pipeline queued.", request.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preflight failed for job <{JobId}>.", request.JobId);

            // The forensically relevant case: a rejected upload must leave its trace. The recorder
            // swallows its own failures, so this cannot mask the cleanup below.
            var recorder = scope.ServiceProvider.GetRequiredService<IPipelineRunRecorder>();
            var failureReason = ex is UploadPreflightException preflightException
                ? $"{preflightException.FailureReason}: {preflightException.Message}"
                : ex.Message;
            await recorder.RecordPreflightFailedAsync(request.JobId, failureReason);

            try
            {
                jobStore.MarkAsFailed(request.JobId);

                // The pipeline was instantiated up front but never queued, so the runner will not dispose it.
                job.Pipeline?.Dispose();
            }
            catch (Exception statusEx)
            {
                logger.LogError(statusEx, "Failed to mark job <{JobId}> as failed.", request.JobId);
            }

            try
            {
                await orchestrationService.ReleaseUploadAsync(request.UploadId);
            }
            catch (Exception cleanupEx)
            {
                // This method is the body of the channel loop: letting anything escape here would stop
                // the background service and leave every later upload stuck in Pending.
                logger.LogError(cleanupEx, "Failed to release the files of upload <{UploadId}>.", request.UploadId);
            }
        }
    }
}
