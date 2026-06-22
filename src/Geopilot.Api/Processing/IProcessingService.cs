using Geopilot.Api.Models;

namespace Geopilot.Api.Processing;

/// <summary>
/// Handles the business logic for processing jobs and delegates job storage to an <see cref="IProcessingJobStore"/>.
/// </summary>
public interface IProcessingService
{
    /// <summary>
    /// Creates a processing job for the given upload, starts the asynchronous preflight, and queues the pipeline.
    /// </summary>
    /// <param name="uploadId">The id of the upload whose files should be processed.</param>
    /// <param name="mandateId">The mandate selecting the pipeline to run.</param>
    /// <param name="user">The user starting the job, or <see langword="null"/> for an anonymous public mandate.</param>
    /// <exception cref="ArgumentException">If no upload with the specified <paramref name="uploadId"/> exists.</exception>
    /// <exception cref="InvalidOperationException">If the job could not be started with the given mandate.</exception>
    Task<ProcessingJob> StartJobAsync(Guid uploadId, int mandateId, User? user);

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
