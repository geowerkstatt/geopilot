using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

/// <summary>
/// Provides methods to start, check and access processing jobs.
/// </summary>
public class ProcessingService : IProcessingService
{
    private readonly IProcessingJobStore jobStore;
    private readonly IUploadStore uploadStore;
    private readonly IMandateService mandateService;
    private readonly IPipelineFactory pipelineFactory;
    private readonly IPipelineRunRecorder runRecorder;
    private readonly ChannelWriter<PreflightRequest> preflightQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingService"/> class.
    /// </summary>
    public ProcessingService(IProcessingJobStore jobStore, IUploadStore uploadStore, IMandateService mandateService, IPipelineFactory pipelineFactory, IPipelineRunRecorder runRecorder, ChannelWriter<PreflightRequest> preflightQueue)
    {
        this.jobStore = jobStore;
        this.uploadStore = uploadStore;
        this.mandateService = mandateService;
        this.pipelineFactory = pipelineFactory;
        this.runRecorder = runRecorder;
        this.preflightQueue = preflightQueue;
    }

    /// <inheritdoc/>
    public async Task<ProcessingJob> StartJobAsync(Guid uploadId, int mandateId, User? user)
    {
        var upload = uploadStore.GetUpload(uploadId)
            ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

        var mandate = await mandateService.GetMandateForUser(mandateId, user);
        if (mandate?.PipelineId == null)
            throw new InvalidOperationException($"The upload <{uploadId}> could not be started with mandate <{mandateId}>.");

        var job = jobStore.CreateJob(uploadId);

        // Instantiate the pipeline up front (without files) and attach it to the job, so the status response
        // can render the pipeline's steps while preflight runs. The pipeline is started once preflight passes.
        var pipeline = pipelineFactory.CreatePipeline(mandate.PipelineId, job.Id);
        var jobWithPipeline = jobStore.AttachPipeline(job.Id, pipeline, mandateId);

        try
        {
            // Deliberately hard: accepting a job is a promise, and we make none we cannot account for.
            // The record must exist before anything can crash the job (see IPipelineRunRecorder).
            await runRecorder.RecordJobStartedAsync(jobWithPipeline, mandate, user, upload);
        }
        catch
        {
            // Without the record there is no job: remove it again (disposing the pipeline) instead of
            // leaving a pending job that preflight would happily run unaccounted.
            jobStore.RemoveJob(job.Id);
            throw;
        }

        await preflightQueue.WriteAsync(new PreflightRequest(job.Id, uploadId));

        return jobStore.GetJob(job.Id)!;
    }

    /// <inheritdoc/>
    public ProcessingJob? GetJob(Guid jobId) => jobStore.GetJob(jobId);

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetSupportedFileExtensionsAsync()
    {
        var mandateFileExtensions = mandateService.GetFileExtensionsForMandates();
        return mandateFileExtensions.OrderBy(ext => ext).ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> IsFileExtensionSupportedAsync(string fileExtension)
    {
        var extensions = await GetSupportedFileExtensionsAsync();
        return IsExtensionSupported(extensions, fileExtension);
    }

    private static bool IsExtensionSupported(ICollection<string> supportedExtensions, string? fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
            return false;

        return supportedExtensions.Any(ext => ext == ".*" || string.Equals(ext, fileExtension, StringComparison.OrdinalIgnoreCase));
    }
}
