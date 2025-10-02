using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;

namespace Geopilot.Api.Validation;

/// <summary>
/// Handles the business logic for validations and delegates the job management to an <see cref="IValidationJobStore"/>.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Creates a new <see cref="ValidationJob"/>.
    /// </summary>
    /// <returns>The created <see cref="ValidationJob"/>.</returns>
    ValidationJob CreateJob();

    /// <summary>
    /// Creates a file handle associated with the specified job and original file name.
    /// </summary>
    /// <param name="jobId">The id of the job for which the file handle is being created.</param>
    /// <param name="originalFileName">The name of the original file associated with the job.</param>
    /// <returns>A <see cref="FileHandle"/> object representing the created file handle.</returns>
    /// <exception cref="ArgumentException">If no job with the specified <paramref name="jobId"/> exists.</exception>
    FileHandle CreateFileHandleForJob(Guid jobId, string originalFileName);

    /// <summary>
    /// Adds the uploaded file to the specified job.
    /// </summary>
    /// <param name="jobId">The id of the job to add the file to.</param>
    /// <param name="originalFileName">The original file name of the uploaded file.</param>
    /// <param name="tempFileName">The temporary, sanitized, internal file name of the uploaded file.</param>
    /// <returns>The updated job, with the original and temporary file name set.</returns>
    /// <exception cref="InvalidOperationException">If the file could not be added to the job.</exception>
    ValidationJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName);

    /// <summary>
    /// Starts the validation job with all validators that support the type of the uploaded file.
    /// </summary>
    /// <remarks>
    /// The validation job is started without a reference to a <see cref="Mandate"/>." and therefore can not be delivered later/>.
    /// </remarks>
    /// <param name="jobId">The id of the validation job to start.</param>
    /// <returns>The started <see cref="ValidationJob"/>.</returns>
    Task<ValidationJob> StartJobAsync(Guid jobId);

    /// <summary>
    /// Start the validation job with all validators associated with the specified mandate.
    /// </summary>
    /// <param name="jobId">The id of the validation job to start.</param>
    /// <param name="mandateId">The id of the mandate the job is started for.</param>
    /// <param name="user">The user starting the job.</param>
    Task<ValidationJob> StartJobAsync(Guid jobId, int mandateId, User user);

    /// <summary>
    /// Gets the validation job.
    /// </summary>
    /// <param name="jobId">The id of the validation job.</param>
    /// <returns>Validation job with the specified <paramref name="jobId"/>, or <see langword="null"/> if no job with the specified id exists.</returns>
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
