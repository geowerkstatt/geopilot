using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Validation;

/// <summary>
/// Provides methods to create, start, check and access validation jobs.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IValidationJobStore jobStore;
    private readonly IMandateService mandateService;
    private readonly ICloudOrchestrationService? cloudOrchestrationService;

    private readonly IFileProvider fileProvider;
    private readonly IPipelineFactory pipelineFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    public ValidationService(IValidationJobStore validationJobStore, IMandateService mandateService, IFileProvider fileProvider, IPipelineFactory pipelineFactory, ICloudOrchestrationService? cloudOrchestrationService = null)
    {
        this.jobStore = validationJobStore;
        this.mandateService = mandateService;
        this.pipelineFactory = pipelineFactory;
        this.cloudOrchestrationService = cloudOrchestrationService;

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
            if (cloudOrchestrationService == null)
                throw new InvalidOperationException("Cloud storage is not enabled.");

            await cloudOrchestrationService.RunPreflightChecksAsync(jobId);
            validationJob = await cloudOrchestrationService.StageFilesLocallyAsync(jobId);
        }

        // Check if the user is allowed to start the job with the specified mandate
        var mandate = await mandateService.GetMandateForUser(mandateId, user);
        if (mandate != null && mandate.PipelineId != null && validationJob.TempFileName != null)
        {
            fileProvider.Initialize(jobId);
            var filePath = fileProvider.GetFilePath(validationJob.TempFileName);
            if (filePath != null)
            {
                var file = new PipelineFile(filePath, validationJob.OriginalFileName ?? "unknown");
                var pipeline = pipelineFactory.CreatePipeline(mandate.PipelineId, file, jobId);
                return jobStore.StartJob(jobId, pipeline, mandateId);
            }
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
