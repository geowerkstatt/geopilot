using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using System.Threading.Channels;

namespace Geopilot.Api.Services;

/// <summary>
/// Background worker that processes cloud upload preflight checks and staging.
/// Reads <see cref="PreflightRequest"/> messages from the channel and runs
/// verify → scan → stage → queue for processing for each job.
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
    /// Processes a single preflight request: runs checks, stages files, and creates the pipeline.
    /// </summary>
    internal async Task ProcessRequestAsync(PreflightRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = serviceScopeFactory.CreateScope();
        var jobStore = scope.ServiceProvider.GetRequiredService<IProcessingJobStore>();
        var uploadStore = scope.ServiceProvider.GetRequiredService<IUploadStore>();
        var cloudOrchestrationService = scope.ServiceProvider.GetRequiredService<ICloudOrchestrationService>();
        var cloudStorageService = scope.ServiceProvider.GetRequiredService<ICloudStorageService>();
        var uploadFileStore = scope.ServiceProvider.GetRequiredService<IUploadFileStore>();

        var job = jobStore.GetJob(request.JobId);
        if (job == null || job.State != ProcessingState.Pending)
        {
            logger.LogWarning("Skipping preflight for job <{JobId}>: job is null or no longer pending.", request.JobId);
            return;
        }

        try
        {
            await cloudOrchestrationService.RunPreflightChecksAsync(request.UploadId);
            var stagedJob = await cloudOrchestrationService.StageFilesLocallyAsync(request.UploadId, request.JobId);

            if (stagedJob.Files == null || stagedJob.Files.Count == 0)
            {
                throw new InvalidOperationException($"No files were staged for job <{request.JobId}>.");
            }

            var pipelineFiles = stagedJob.Files
                .Select(f =>
                {
                    if (!uploadFileStore.Exists(request.JobId, f.TempFileName))
                        return null;
                    var path = uploadFileStore.GetPath(request.JobId, f.TempFileName);
                    return new PipelineFile(path, f.OriginalFileName ?? "unknown");
                })
                .Where(f => f != null)
                .Cast<IPipelineFile>()
                .ToList();

            jobStore.EnqueueForProcessing(request.JobId, new PipelineFileList(pipelineFiles));

            logger.LogInformation("Preflight complete for job <{JobId}>. Pipeline queued.", request.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preflight failed for job <{JobId}>.", request.JobId);

            try
            {
                await cloudStorageService.DeletePrefixAsync($"uploads/{request.UploadId}/");
                uploadStore.RemoveUpload(request.UploadId);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx, "Failed to clean up cloud files for upload <{UploadId}>.", request.UploadId);
            }

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
        }
    }
}
