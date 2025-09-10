using Geopilot.Api.FileAccess;

namespace Geopilot.Api.Validation;

/// <summary>
/// Provides methods to create, start, check and access validation jobs.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Creates a new <see cref="ValidationJob"/>.
    /// </summary>
    /// <param name="originalFileName">Name of the uploaded file.</param>
    /// <returns>The created <see cref="ValidationJob"/> and a <see cref="FileHandle"/> to store the file to validate.</returns>
    (ValidationJob ValidationJob, FileHandle FileHandle) CreateValidationJob(string originalFileName);

    /// <summary>
    /// Starts the validation job asynchronously.
    /// </summary>
    /// <param name="validationJob">The validation job to start.</param>
    Task StartValidationJobAsync(ValidationJob validationJob);

    /// <summary>
    /// Gets the validation job.
    /// </summary>
    /// <param name="jobId">The id of the validation job.</param>
    /// <returns>Validation job with the specified <paramref name="jobId"/>.</returns>
    ValidationJob? GetJob(Guid jobId);

    /// <summary>
    /// Gets all file extensions that are supported for upload.
    /// All entries start with a "." like ".txt", ".xml" and the collection can include ".*" (all files allowed).
    /// </summary>
    /// <returns>Supported file extensions.</returns>
    Task<ICollection<string>> GetSupportedFileExtensionsAsync();

    /// <summary>
    /// Checks if the specified <paramref name="fileExtension"/> is supported for upload.
    /// </summary>
    /// <param name="fileExtension">Extension of the uploaded file starting with ".".</param>
    /// <returns>True, if the <paramref name="fileExtension"/> is supported.</returns>
    Task<bool> IsFileExtensionSupportedAsync(string fileExtension);
}
