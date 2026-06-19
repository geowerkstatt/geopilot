using Geopilot.Api.Models;
using Geopilot.Api.Services;
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
    private readonly ChannelWriter<PreflightRequest> preflightQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingService"/> class.
    /// </summary>
    public ProcessingService(IProcessingJobStore jobStore, IUploadStore uploadStore, IMandateService mandateService, ChannelWriter<PreflightRequest> preflightQueue)
    {
        this.jobStore = jobStore;
        this.uploadStore = uploadStore;
        this.mandateService = mandateService;
        this.preflightQueue = preflightQueue;
    }

    /// <inheritdoc/>
    public async Task<ProcessingJob> StartJob(Guid uploadId, int mandateId, User? user)
    {
        if (uploadStore.GetUpload(uploadId) == null)
            throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

        var mandate = await mandateService.GetMandateForUser(mandateId, user);
        if (mandate?.PipelineId == null)
            throw new InvalidOperationException($"The upload <{uploadId}> could not be started with mandate <{mandateId}>.");

        var job = jobStore.CreateJob();

        // Record the pipeline id early so the response can render step display info while preflight runs
        // and before the actual pipeline is instantiated.
        jobStore.SetPipelineId(job.Id, mandate.PipelineId);

        await preflightQueue.WriteAsync(new PreflightRequest(job.Id, uploadId, mandateId, user?.AuthIdentifier));

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
