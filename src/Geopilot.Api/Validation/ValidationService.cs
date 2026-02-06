using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
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
    private readonly IEnumerable<IValidator> validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    public ValidationService(IValidationJobStore validationJobStore, IMandateService mandateService, IFileProvider fileProvider, IEnumerable<IValidator> validators)
    {
        this.jobStore = validationJobStore;
        this.mandateService = mandateService;

        this.fileProvider = fileProvider;
        this.validators = validators;
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
        var mandate = await mandateService.GetMandateAsUser(mandateId, user);
        if (mandate != null)
        {
            // Check if the mandate supports the job file type
            var jobFileType = Path.GetExtension(validationJob.OriginalFileName);
            var mandateSupportsJobFileType = IsExtensionSupported(mandate.FileTypes, jobFileType);

            if (mandateSupportsJobFileType)
            {
                var jobValidators = await GetConfiguredValidators(validationJob, mandate);
                return jobStore.StartJob(jobId, jobValidators, mandateId);
            }
        }

        throw new InvalidOperationException($"The job <{jobId}> could not be started with mandate <{mandateId}>.");
    }

    private async Task<List<IValidator>> GetConfiguredValidators(ValidationJob validationJob, Mandate? mandate)
    {
        // Get all supported validators and configure them for the job
        var fileExtension = Path.GetExtension(validationJob.TempFileName);
        var jobValidators = new List<IValidator>();
        foreach (var validator in validators)
        {
            var supportedExtensions = await validator.GetSupportedFileExtensionsAsync();
            if (IsExtensionSupported(supportedExtensions, fileExtension))
            {
                ConfigureValidator(validator, validationJob, mandate);
                jobValidators.Add(validator);
            }
        }

        return jobValidators;
    }

    private void ConfigureValidator(IValidator validator, ValidationJob validationJob, Mandate? mandate)
    {
        switch (validator)
        {
            case InterlisValidator interlisValidator:
                fileProvider.Initialize(validationJob.Id);
                interlisValidator.Configure(fileProvider, validationJob.TempFileName ?? string.Empty, mandate?.InterlisValidationProfile);
                break;
        }
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
        var validatorFileExtensions = await GetFileExtensionsForValidatorsAsync();

        return mandateFileExtensions
            .Union(validatorFileExtensions)
            .OrderBy(ext => ext)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> IsFileExtensionSupportedAsync(string fileExtension)
    {
        var extensions = await GetSupportedFileExtensionsAsync();
        return IsExtensionSupported(extensions, fileExtension);
    }

    private async Task<HashSet<string>> GetFileExtensionsForValidatorsAsync()
    {
        var tasks = validators.Select(validator => validator.GetSupportedFileExtensionsAsync());

        var validatorFileExtensions = await Task.WhenAll(tasks);

        return validatorFileExtensions
            .SelectMany(ext => ext)
            .Select(ext => ext.ToLowerInvariant())
            .ToHashSet();
    }

    private static bool IsExtensionSupported(ICollection<string> supportedExtensions, string? fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
            return false;

        return supportedExtensions.Any(ext => ext == ".*" || string.Equals(ext, fileExtension, StringComparison.OrdinalIgnoreCase));
    }
}
