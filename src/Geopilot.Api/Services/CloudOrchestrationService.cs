using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Validation;
using Microsoft.Extensions.Options;
using System.Collections.Immutable;

namespace Geopilot.Api.Services;

/// <summary>
/// Orchestrates cloud upload sessions including initiation, preflight checks, and local staging.
/// </summary>
public class CloudOrchestrationService : ICloudOrchestrationService
{
    private readonly ICloudStorageService cloudStorageService;
    private readonly ICloudScanService cloudScanService;
    private readonly IValidationJobStore jobStore;
    private readonly IFileProvider fileProvider;
    private readonly IOptions<CloudStorageOptions> options;
    private readonly ILogger<CloudOrchestrationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudOrchestrationService"/> class.
    /// </summary>
    public CloudOrchestrationService(
        ICloudStorageService cloudStorageService,
        ICloudScanService cloudScanService,
        IValidationJobStore jobStore,
        IFileProvider fileProvider,
        IOptions<CloudStorageOptions> options,
        ILogger<CloudOrchestrationService> logger)
    {
        this.cloudStorageService = cloudStorageService;
        this.cloudScanService = cloudScanService;
        this.jobStore = jobStore;
        this.fileProvider = fileProvider;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CloudUploadResponse> InitiateUploadAsync(CloudUploadRequest request)
    {
        ValidateRequest(request);

        var activeJobs = jobStore.GetActiveCloudJobCount();
        if (activeJobs >= options.Value.MaxActiveJobs)
            throw new InvalidOperationException($"Maximum number of active cloud upload jobs ({options.Value.MaxActiveJobs}) reached.");

        var declaredTotalSize = request.Files.Sum(f => f.Size);
        var maxGlobalBytes = (long)options.Value.MaxGlobalActiveSizeMB * 1024 * 1024;
        var currentSize = await cloudStorageService.GetTotalSizeAsync("uploads/");
        if (currentSize + declaredTotalSize > maxGlobalBytes)
            throw new InvalidOperationException($"Global active upload size limit ({options.Value.MaxGlobalActiveSizeMB} MB) would be exceeded.");

        var job = jobStore.CreateJob();
        var cloudPrefix = $"uploads/{job.Id}/";

        var cloudFiles = new List<CloudFileInfo>();
        var fileUploadInfos = new List<FileUploadInfo>();
        var expiresIn = TimeSpan.FromMinutes(options.Value.PresignedUrlExpiryMinutes);

        foreach (var file in request.Files)
        {
            var sanitizedName = Path.GetFileName(file.FileName);
            var cloudKey = $"{cloudPrefix}{sanitizedName}";
            var presignedUrl = await cloudStorageService.GeneratePresignedUploadUrlAsync(cloudKey, null, expiresIn);

            cloudFiles.Add(new CloudFileInfo(sanitizedName, cloudKey, file.Size));
            fileUploadInfos.Add(new FileUploadInfo(sanitizedName, presignedUrl));
        }

        jobStore.AddUploadInfoToJob(job.Id, UploadMethod.Cloud, cloudFiles.ToImmutableList());

        return new CloudUploadResponse(job.Id, fileUploadInfos, DateTime.UtcNow.Add(expiresIn));
    }

    /// <inheritdoc/>
    public async Task RunPreflightChecksAsync(Guid jobId)
    {
        var job = jobStore.GetJob(jobId) ?? throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId));

        if (job.UploadMethod != UploadMethod.Cloud)
            throw new InvalidOperationException($"Job <{jobId}> is not a cloud upload job.");

        if (job.CloudFiles == null || job.CloudFiles.Count == 0)
            throw new InvalidOperationException($"Job <{jobId}> has no cloud files configured.");

        jobStore.SetJobStatus(jobId, Status.VerifyingUpload);

        var cloudPrefix = $"uploads/{jobId}/";
        var uploadedFiles = await cloudStorageService.ListFilesAsync(cloudPrefix);

        foreach (var expectedFile in job.CloudFiles)
        {
            var uploaded = uploadedFiles.FirstOrDefault(f => f.Key == expectedFile.CloudKey);
            if (uploaded == default)
            {
                jobStore.SetJobStatus(jobId, Status.UploadIncomplete);
                throw new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, $"File '{expectedFile.FileName}' was not uploaded.");
            }

            if (uploaded.Size < expectedFile.ExpectedSize)
            {
                jobStore.SetJobStatus(jobId, Status.UploadIncomplete);
                throw new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, $"File '{expectedFile.FileName}' is incomplete.");
            }

            if (uploaded.Size > expectedFile.ExpectedSize)
            {
                logger.LogError("File '{FileName}' for job <{JobId}> exceeds declared size ({Actual} > {Expected}).", expectedFile.FileName, jobId, uploaded.Size, expectedFile.ExpectedSize);

                await cloudStorageService.DeletePrefixAsync(cloudPrefix);
                jobStore.RemoveJob(jobId);

                throw new CloudUploadPreflightException(PreflightFailureReason.SizeExceeded, "The uploaded files could not be processed.");
            }
        }

        var keys = job.CloudFiles.Select(f => f.CloudKey).ToList();
        var scanResult = await cloudScanService.CheckFilesAsync(keys);

        if (!scanResult.IsClean)
        {
            logger.LogError("Threat detected in cloud files for job <{JobId}>: {ThreatDetails}", jobId, scanResult.ThreatDetails);

            await cloudStorageService.DeletePrefixAsync(cloudPrefix);
            jobStore.RemoveJob(jobId);

            throw new CloudUploadPreflightException(PreflightFailureReason.ThreatDetected, "The uploaded files could not be processed.");
        }
    }

    /// <inheritdoc/>
    public async Task<ValidationJob> StageFilesLocallyAsync(Guid jobId)
    {
        var job = jobStore.GetJob(jobId) ?? throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId));

        if (job.UploadMethod != UploadMethod.Cloud)
            throw new InvalidOperationException($"Job <{jobId}> is not a cloud upload job.");

        if (job.CloudFiles == null || job.CloudFiles.Count == 0)
            throw new InvalidOperationException($"Job <{jobId}> has no cloud files to stage.");

        fileProvider.Initialize(jobId);

        ValidationJob updatedJob = job;
        foreach (var file in job.CloudFiles)
        {
            var extension = Path.GetExtension(file.FileName);

            using var fileHandle = fileProvider.CreateFileWithRandomName(extension);
            await cloudStorageService.DownloadAsync(file.CloudKey, fileHandle.Stream);

            updatedJob = jobStore.AddFileToJob(jobId, file.FileName, fileHandle.FileName);
        }

        var cloudPrefix = $"uploads/{jobId}/";
        await cloudStorageService.DeletePrefixAsync(cloudPrefix);

        return updatedJob;
    }

    private void ValidateRequest(CloudUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Files == null || request.Files.Count == 0)
            throw new ArgumentException("At least one file must be specified.", nameof(request));

        // TODO: Remove this limitation when multi-file upload support is added.
        if (request.Files.Count > 1)
            throw new ArgumentException("Only single file uploads are currently supported.", nameof(request));

        if (request.Files.Count > options.Value.MaxFilesPerJob)
            throw new ArgumentException($"Too many files. Maximum is {options.Value.MaxFilesPerJob}.", nameof(request));

        var maxFileSizeBytes = (long)options.Value.MaxFileSizeMB * 1024 * 1024;
        var maxJobSizeBytes = (long)options.Value.MaxJobSizeMB * 1024 * 1024;
        long totalSize = 0;

        foreach (var file in request.Files)
        {
            if (file.Size <= 0)
                throw new ArgumentException($"File '{file.FileName}' has invalid size.", nameof(request));

            if (file.Size > maxFileSizeBytes)
                throw new ArgumentException($"File '{file.FileName}' exceeds the maximum file size of {options.Value.MaxFileSizeMB} MB.", nameof(request));

            totalSize += file.Size;
        }

        if (totalSize > maxJobSizeBytes)
            throw new ArgumentException($"Total upload size exceeds the maximum of {options.Value.MaxJobSizeMB} MB.", nameof(request));
    }
}
