using Geopilot.Api.Contracts;
using Geopilot.Api.Exceptions;
using Geopilot.Api.Processing;

namespace Geopilot.Api.Services;

/// <summary>
/// Orchestrates cloud upload sessions including initiation, preflight checks, and local staging.
/// </summary>
public interface ICloudOrchestrationService
{
    /// <summary>
    /// Initiates a cloud upload session by creating a job and generating presigned upload URLs.
    /// </summary>
    /// <param name="request">The upload request containing file metadata.</param>
    /// <returns>The upload response with presigned URLs and job information.</returns>
    Task<CloudUploadResponse> InitiateUploadAsync(CloudUploadRequest request);

    /// <summary>
    /// Runs preflight checks on uploaded cloud files including completeness verification and malware scanning.
    /// </summary>
    /// <param name="uploadId">The upload ID to run preflight checks for.</param>
    /// <exception cref="CloudUploadPreflightException">Thrown when preflight checks fail.</exception>
    Task RunPreflightChecksAsync(Guid uploadId);

    /// <summary>
    /// Downloads the upload's cloud files to local storage and registers them on the job.
    /// </summary>
    /// <param name="uploadId">The upload ID to stage files from.</param>
    /// <param name="jobId">The job ID to register the staged files on.</param>
    /// <returns>The updated processing job.</returns>
    Task<ProcessingJob> StageFilesLocallyAsync(Guid uploadId, Guid jobId);
}
