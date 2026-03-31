using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Geopilot.PipelineCore.Pipeline;
using System.Threading.Channels;

namespace Geopilot.Api.Validation;

/// <summary>
/// Provides methods to create, start, check and access validation jobs.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IValidationJobStore jobStore;
    private readonly IMandateService mandateService;
    private readonly ICloudOrchestrationService? cloudOrchestrationService;
    private readonly ChannelWriter<PreflightRequest>? preflightQueue;
    private readonly IFileProvider fileProvider;
    private readonly IPipelineFactory pipelineFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    public ValidationService(IValidationJobStore validationJobStore, IMandateService mandateService, IFileProvider fileProvider, IPipelineFactory pipelineFactory, ICloudOrchestrationService? cloudOrchestrationService = null, ChannelWriter<PreflightRequest>? preflightQueue = null)
    {
        this.jobStore = validationJobStore;
        this.mandateService = mandateService;
        this.pipelineFactory = pipelineFactory;
        this.cloudOrchestrationService = cloudOrchestrationService;
        this.preflightQueue = preflightQueue;
        this.fileProvider = fileProvider;
    }

    /// <inheritdoc/>
    public ValidationJob CreateJob()
    {
        return jobStore.CreateJob();
    }

    /// <inheritdoc/>
    public FileHandle CreateFileHandleForJob(Guid jobId, string originalFileName)
    {
        if (jobStore.GetJob(jobId) == null) throw new ArgumentException($"Validation job with id <{jobId}> not found.", nameof(jobId));

        var extension = Path.GetExtension(originalFileName);
        fileProvider.Initialize(jobId);
        return fileProvider.CreateFileWithRandomName(extension);
    }

    /// <inheritdoc/>
    public ValidationJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName)
    {
        return jobStore.AddFileToJob(jobId, originalFileName, tempFileName);
    }

    /// <inheritdoc/>
    public async Task<ValidationJob> StartJobAsync(Guid jobId, int mandateId, User? user)
    {
        var validationJob = jobStore.GetJob(jobId) ?? throw new ArgumentException($"Validation job with id <{jobId}> not found.", nameof(jobId));

        if (validationJob.UploadMethod == UploadMethod.Cloud)
        {
            if (cloudOrchestrationService == null || preflightQueue == null)
                throw new InvalidOperationException("Cloud storage is not enabled.");

            var cloudMandate = await mandateService.GetMandateForUser(mandateId, user);
            if (cloudMandate?.PipelineId == null)
                throw new InvalidOperationException($"The job <{jobId}> could not be started with mandate <{mandateId}>.");

            jobStore.SetJobStatus(jobId, Status.VerifyingUpload);
            await preflightQueue.WriteAsync(new PreflightRequest(jobId, mandateId, user?.AuthIdentifier));
            return jobStore.GetJob(jobId)!;
        }

        // Check if the user is allowed to start the job with the specified mandate
        var mandate = await mandateService.GetMandateForUser(mandateId, user);
        if (mandate != null && mandate.PipelineId != null && validationJob.Files != null && validationJob.Files.Count > 0)
        {
            fileProvider.Initialize(jobId);
            var pipelineFiles = validationJob.Files
                .Select(f =>
                {
                    var path = fileProvider.GetFilePath(f.TempFileName);
                    if (path == null)
                        return null;
                    return new PipelineFile(path, f.OriginalFileName ?? "unknown");
                })
                .Where(f => f != null)
                .Cast<IPipelineFile>()
                .ToList();

            var pipeline = pipelineFactory.CreatePipeline(mandate.PipelineId, pipelineFiles, jobId);
            return jobStore.StartJob(jobId, pipeline, mandateId);
        }

        throw new InvalidOperationException($"The job <{jobId}> could not be started with mandate <{mandateId}>.");
    }

    /// <inheritdoc/>
    public ValidationJob? GetJob(Guid jobId)
    {
        return jobStore.GetJob(jobId);
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetSupportedFileExtensionsAsync()
    {
        var mandateFileExtensions = mandateService.GetFileExtensionsForMandates();
        return mandateFileExtensions
            .OrderBy(ext => ext)
            .ToList();
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
