using Geopilot.Api.FileAccess;
using Geopilot.Api.Validation.Interlis;

namespace Geopilot.Api.Validation;

/// <summary>
/// Provides methods to create, start, check and access validation jobs.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IFileProvider fileProvider;
    private readonly IEnumerable<IValidator> validators;
    private readonly Context context;
    private readonly IValidationJobStore jobStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    public ValidationService(IFileProvider fileProvider, IEnumerable<IValidator> validators, Context context, IValidationJobStore validationJobStore)
    {
        this.fileProvider = fileProvider;
        this.validators = validators;
        this.context = context;
        this.jobStore = validationJobStore;
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
    public async Task<ValidationJob> StartJobAsync(Guid jobId)
    {
        var validationJob = jobStore.GetJob(jobId) ?? throw new ArgumentException($"Validation job with id <{jobId}> not found.", nameof(jobId));

        var fileExtension = Path.GetExtension(validationJob.TempFileName);
        var supportedValidators = new List<IValidator>();
        foreach (var validator in validators)
        {
            var supportedExtensions = await validator.GetSupportedFileExtensionsAsync();
            if (IsExtensionSupported(supportedExtensions, fileExtension))
            {
                ConfigureValidator(validator, validationJob);
                supportedValidators.Add(validator);
            }
        }

        return jobStore.StartJob(jobId, supportedValidators);
    }

    private void ConfigureValidator(IValidator validator, ValidationJob validationJob)
    {
        switch (validator)
        {
            case InterlisValidator interlisValidator:
                interlisValidator.Configure(fileProvider, validationJob.TempFileName);
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
        var mandateFileExtensions = GetFileExtensionsForMandates();
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

    private HashSet<string> GetFileExtensionsForMandates()
    {
        return context.Mandates
            .Select(mandate => mandate.FileTypes)
            .AsEnumerable()
            .SelectMany(ext => ext)
            .Select(ext => ext.ToLowerInvariant())
            .ToHashSet();
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

    private static bool IsExtensionSupported(ICollection<string> supportedExtensions, string fileExtension)
    {
        return supportedExtensions.Any(ext => ext == ".*" || string.Equals(ext, fileExtension, StringComparison.OrdinalIgnoreCase));
    }
}
