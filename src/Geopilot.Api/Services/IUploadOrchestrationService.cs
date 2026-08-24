using Geopilot.Api.Contracts;
using Geopilot.Api.Exceptions;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Services;

/// <summary>
/// Orchestrates upload sessions including initiation, preflight checks, and handing the uploaded
/// files to a processing job.
/// </summary>
public interface IUploadOrchestrationService
{
    /// <summary>
    /// Initiates a upload session by creating a job and generating presigned upload URLs.
    /// </summary>
    /// <param name="request">The upload request containing file metadata.</param>
    /// <returns>The upload response with presigned URLs and job information.</returns>
    Task<InitiateUploadResponse> InitiateUploadAsync(InitiateUploadRequest request);

    /// <summary>
    /// Runs preflight checks on uploaded files including completeness verification and malware scanning.
    /// </summary>
    /// <param name="uploadId">The upload ID to run preflight checks for.</param>
    /// <exception cref="UploadPreflightException">Thrown when preflight checks fail.</exception>
    Task RunPreflightChecksAsync(Guid uploadId);

    /// <summary>
    /// Registers the upload's files on the job and returns them as pipeline files. Nothing is
    /// transferred here: each file is fetched from the upload storage the first time a step reads it.
    /// </summary>
    /// <param name="uploadId">The upload ID to take the files from.</param>
    /// <param name="jobId">The job ID to register the files on.</param>
    /// <returns>The files of the upload, in upload order.</returns>
    IReadOnlyList<IPipelineFile> RegisterJobFiles(Guid uploadId, Guid jobId);

    /// <summary>
    /// Deletes the upload's blobs and forgets the upload session. Called once the files can no longer be
    /// needed: after a failed preflight, after a run that cannot be delivered, or when the job is retired.
    /// Errors are logged, not thrown, so a failed cleanup never breaks the caller's own flow.
    /// </summary>
    /// <param name="uploadId">The upload to release.</param>
    Task ReleaseUploadAsync(Guid uploadId);
}
