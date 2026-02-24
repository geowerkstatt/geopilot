using Geopilot.Api.Contracts;
using Geopilot.Api.Validation;

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
    /// <param name="jobId">The job ID to run preflight checks for.</param>
    /// <returns>The preflight check result.</returns>
    Task<PreflightResponse> RunPreflightChecksAsync(Guid jobId);

    /// <summary>
    /// Downloads cloud files to local storage and updates the job with local file information.
    /// </summary>
    /// <param name="jobId">The job ID to stage files for.</param>
    /// <returns>The updated validation job.</returns>
    Task<ValidationJob> StageFilesLocallyAsync(Guid jobId);
}
