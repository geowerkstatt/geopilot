using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Geopilot.Api.Test.Pipeline;
using Geopilot.Api.Validation.Interlis;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Geopilot.Api.Validation;

/// <summary>
/// Provides methods to create, start, check and access validation jobs.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IValidationJobStore jobStore;
    private readonly IMandateService mandateService;

    private readonly IFileProvider fileProvider;
    private readonly IPipelineFactory pipelineFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    public ValidationService(IValidationJobStore validationJobStore, IMandateService mandateService, IFileProvider fileProvider, IPipelineFactory pipelineFactory)
    {
        this.jobStore = validationJobStore;
        this.mandateService = mandateService;
        this.pipelineFactory = pipelineFactory;

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

        // Check if the user is allowed to start the job with the specified mandate
        var mandate = await mandateService.GetMandateForUser(mandateId, user);
        if (mandate != null && mandate.PipelineId != null && validationJob.TempFileName != null)
        {
            fileProvider.Initialize(jobId);
            var filePath = fileProvider.GetFilePath(validationJob.TempFileName);
            if (filePath != null)
            {
                var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(validationJob.OriginalFileName ?? string.Empty);
                var file = new PipelineTransferFile(originalFileNameWithoutExtension, filePath);
                var pipeline = pipelineFactory.CreatePipeline(mandate.PipelineId, file);
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
