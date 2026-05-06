using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;

namespace Geopilot.Api.Processing;

/// <summary>
/// Handles the business logic for processing jobs and delegates job storage to an <see cref="IProcessingJobStore"/>.
/// </summary>
public interface IProcessingService
{
    /// <summary>
    /// Creates a new <see cref="ProcessingJob"/>.
    /// </summary>
    ProcessingJob CreateJob();

    /// <summary>
    /// Creates a file handle associated with the specified job and original file name.
    /// </summary>
    /// <exception cref="ArgumentException">If no job with the specified <paramref name="jobId"/> exists.</exception>
    FileHandle CreateFileHandleForJob(Guid jobId, string originalFileName);

    /// <summary>
    /// Adds the uploaded file to the specified job.
    /// </summary>
    ProcessingJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName);

    /// <summary>
    /// Starts the processing job with the pipeline associated with the specified mandate.
    /// </summary>
    Task<ProcessingJob> StartJobAsync(Guid jobId, int mandateId, User? user);

    /// <summary>
    /// Gets the processing job.
    /// </summary>
    ProcessingJob? GetJob(Guid jobId);

    /// <summary>
    /// All file extensions supported for upload.
    /// All entries start with a "." like ".txt", ".xml" and may include ".*" (all files allowed).
    /// </summary>
    Task<ICollection<string>> GetSupportedFileExtensionsAsync();

    /// <summary>
    /// Whether the specified <paramref name="fileExtension"/> is supported for upload.
    /// </summary>
    Task<bool> IsFileExtensionSupportedAsync(string fileExtension);
}
