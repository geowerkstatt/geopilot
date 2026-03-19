using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Validation;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;

namespace Geopilot.Api.Services;

/// <summary>
/// Background worker that processes cloud upload preflight checks and staging.
/// Reads <see cref="PreflightRequest"/> messages from the channel and runs
/// verify → scan → stage → create pipeline for each job.
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

        using var scope = serviceScopeFactory.CreateScope();
        var jobStore = scope.ServiceProvider.GetRequiredService<IValidationJobStore>();
        var cloudOrchestrationService = scope.ServiceProvider.GetRequiredService<ICloudOrchestrationService>();
        var cloudStorageService = scope.ServiceProvider.GetRequiredService<ICloudStorageService>();
        var mandateService = scope.ServiceProvider.GetRequiredService<IMandateService>();
        var fileProvider = scope.ServiceProvider.GetRequiredService<IFileProvider>();
        var pipelineFactory = scope.ServiceProvider.GetRequiredService<IPipelineFactory>();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        var job = jobStore.GetJob(request.JobId);
        if (job == null || job.Status != Status.VerifyingUpload)
        {
            logger.LogWarning("Skipping preflight for job <{JobId}>: job is null or not in VerifyingUpload status.", request.JobId);
            return;
        }

        try
        {
            await cloudOrchestrationService.RunPreflightChecksAsync(request.JobId);
            var stagedJob = await cloudOrchestrationService.StageFilesLocallyAsync(request.JobId);

            Models.User? user = null;
            if (request.UserAuthId != null)
            {
                user = await context.Users.FirstOrDefaultAsync(u => u.AuthIdentifier == request.UserAuthId, cancellationToken);
            }

            var mandate = await mandateService.GetMandateForUser(request.MandateId, user);
            if (mandate?.PipelineId == null || stagedJob.TempFileName == null)
            {
                throw new InvalidOperationException($"The job <{request.JobId}> could not be started with mandate <{request.MandateId}>.");
            }

            fileProvider.Initialize(request.JobId);
            var filePath = fileProvider.GetFilePath(stagedJob.TempFileName);
            if (filePath == null)
            {
                throw new InvalidOperationException($"Could not resolve file path for job <{request.JobId}>.");
            }

            var file = new PipelineFile(filePath, stagedJob.OriginalFileName ?? "unknown");
            var pipeline = pipelineFactory.CreatePipeline(mandate.PipelineId, file, request.JobId);
            jobStore.StartJob(request.JobId, pipeline, request.MandateId);

            logger.LogInformation("Preflight complete for job <{JobId}>. Pipeline queued.", request.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preflight failed for job <{JobId}>.", request.JobId);

            try
            {
                await cloudStorageService.DeletePrefixAsync($"uploads/{request.JobId}/");
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx, "Failed to clean up cloud files for job <{JobId}>.", request.JobId);
            }

            try
            {
                jobStore.SetJobStatus(request.JobId, Status.Failed);
            }
            catch (Exception statusEx)
            {
                logger.LogError(statusEx, "Failed to set Failed status for job <{JobId}>.", request.JobId);
            }
        }
    }
}
