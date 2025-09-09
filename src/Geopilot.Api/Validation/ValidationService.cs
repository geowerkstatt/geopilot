using Geopilot.Api.FileAccess;

namespace Geopilot.Api.Validation;

/// <summary>
/// Provides methods to start validation jobs and access status information for a specific job.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IFileProvider fileProvider;
    private readonly IValidationRunner validationRunner;
    private readonly IEnumerable<IValidator> validators;
    private readonly Context context;
    private readonly IValidationManager validationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    public ValidationService(IFileProvider fileProvider, IValidationRunner validationRunner, IEnumerable<IValidator> validators, Context context, IValidationManager validationManager)
    {
        this.fileProvider = fileProvider;
        this.validationRunner = validationRunner;
        this.validators = validators;
        this.context = context;
        this.validationManager = validationManager;
    }

    /// <inheritdoc/>
    public (ValidationJob ValidationJob, FileHandle FileHandle) CreateValidationJob(string originalFileName)
    {
        var id = Guid.NewGuid();
        fileProvider.Initialize(id);

        var extension = Path.GetExtension(originalFileName);
        var fileHandle = fileProvider.CreateFileWithRandomName(extension);
        var validationJob = new ValidationJob(id, originalFileName, fileHandle.FileName);
        return (validationJob, fileHandle);
    }

    /// <inheritdoc/>
    public async Task StartValidationJobAsync(ValidationJob validationJob)
    {
        ArgumentNullException.ThrowIfNull(validationJob);

        var fileExtension = Path.GetExtension(validationJob.TempFileName);
        var supportedValidators = new List<IValidator>();
        foreach (var validator in validators)
        {
            var supportedExtensions = await validator.GetSupportedFileExtensionsAsync();
            if (IsExtensionSupported(supportedExtensions, fileExtension))
            {
                supportedValidators.Add(validator);
            }
        }

        validationManager.AddJob(validationJob);
        await validationRunner.EnqueueJobAsync(validationJob, supportedValidators);
    }

    /// <inheritdoc/>
    public ValidationJob? GetJob(Guid jobId)
    {
        return validationManager.GetJob(jobId);
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
