using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
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
    private readonly IProcessingJobStore jobStore;
    private readonly IUploadStore uploadStore;
    private readonly IUploadFileStore uploadFileStore;
    private readonly IOptions<CloudStorageOptions> options;
    private readonly ILogger<CloudOrchestrationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudOrchestrationService"/> class.
    /// </summary>
    public CloudOrchestrationService(
        ICloudStorageService cloudStorageService,
        ICloudScanService cloudScanService,
        IProcessingJobStore jobStore,
        IUploadStore uploadStore,
        IUploadFileStore uploadFileStore,
        IOptions<CloudStorageOptions> options,
        ILogger<CloudOrchestrationService> logger)
    {
        this.cloudStorageService = cloudStorageService;
        this.cloudScanService = cloudScanService;
        this.jobStore = jobStore;
        this.uploadStore = uploadStore;
        this.uploadFileStore = uploadFileStore;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CloudUploadResponse> InitiateUploadAsync(CloudUploadRequest request)
    {
        ValidateRequest(request);

        var activeUploads = uploadStore.GetActiveUploadCount();
        if (activeUploads >= options.Value.MaxActiveJobs)
            throw new InvalidOperationException($"Maximum number of active cloud uploads ({options.Value.MaxActiveJobs}) reached.");

        var declaredTotalSize = request.Files.Sum(f => f.Size);
        var maxGlobalBytes = (long)options.Value.MaxGlobalActiveSizeMB * 1024 * 1024;
        var currentSize = await cloudStorageService.GetTotalSizeAsync("uploads/");
        if (currentSize + declaredTotalSize > maxGlobalBytes)
            throw new InvalidOperationException($"Global active upload size limit ({options.Value.MaxGlobalActiveSizeMB} MB) would be exceeded.");

        var uploadId = Guid.NewGuid();
        var cloudPrefix = $"uploads/{uploadId}/";

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

        uploadStore.CreateUpload(uploadId, cloudFiles.ToImmutableList());

        logger.LogInformation("Initiated cloud upload <{UploadId}> with {FileCount} file(s).", uploadId, request.Files.Count);

        return new CloudUploadResponse(uploadId, fileUploadInfos, DateTime.UtcNow.Add(expiresIn));
    }

    /// <inheritdoc/>
    public async Task RunPreflightChecksAsync(Guid uploadId)
    {
        var upload = uploadStore.GetUpload(uploadId) ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

        if (upload.Files.Count == 0)
            throw new InvalidOperationException($"Upload <{uploadId}> has no cloud files configured.");

        logger.LogInformation("Starting preflight checks for upload <{UploadId}>.", uploadId);

        var cloudPrefix = $"uploads/{uploadId}/";
        var uploadedFiles = await cloudStorageService.ListFilesAsync(cloudPrefix);

        foreach (var expectedFile in upload.Files)
        {
            var uploaded = uploadedFiles.FirstOrDefault(f => f.Key == expectedFile.CloudKey);
            if (uploaded == default)
            {
                throw new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, $"File '{expectedFile.FileName}' was not uploaded.");
            }

            if (uploaded.Size < expectedFile.ExpectedSize)
            {
                throw new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, $"File '{expectedFile.FileName}' is incomplete.");
            }

            if (uploaded.Size > expectedFile.ExpectedSize)
            {
                logger.LogError("File '{FileName}' for upload <{UploadId}> exceeds declared size ({Actual} > {Expected}).", expectedFile.FileName, uploadId, uploaded.Size, expectedFile.ExpectedSize);
                throw new CloudUploadPreflightException(PreflightFailureReason.SizeExceeded, "The uploaded files could not be processed.");
            }
        }

        var keys = upload.Files.Select(f => f.CloudKey).ToList();
        var scanResult = await cloudScanService.CheckFilesAsync(keys);

        if (!scanResult.IsClean)
        {
            logger.LogError("Threat detected in cloud files for upload <{UploadId}>: {ThreatDetails}", uploadId, scanResult.ThreatDetails);
            throw new CloudUploadPreflightException(PreflightFailureReason.ThreatDetected, "The uploaded files could not be processed.");
        }
    }

    /// <inheritdoc/>
    public async Task<ProcessingJob> StageFilesLocallyAsync(Guid uploadId, Guid jobId)
    {
        var upload = uploadStore.GetUpload(uploadId) ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));
        var job = jobStore.GetJob(jobId) ?? throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId));

        if (upload.Files.Count == 0)
            throw new InvalidOperationException($"Upload <{uploadId}> has no cloud files to stage.");

        logger.LogInformation("Staging cloud files for upload <{UploadId}> locally on job <{JobId}>.", uploadId, jobId);

        ProcessingJob updatedJob = job;
        foreach (var file in upload.Files)
        {
            var stagedName = UploadFileNaming.MakeUnique(jobId, file.FileName, uploadFileStore);

            using (var stream = uploadFileStore.CreateFile(jobId, stagedName))
            {
                await cloudStorageService.DownloadAsync(file.CloudKey, stream);
            }

            updatedJob = jobStore.AddFileToJob(jobId, file.FileName, stagedName);
        }

        await cloudStorageService.DeletePrefixAsync($"uploads/{uploadId}/");
        uploadStore.RemoveUpload(uploadId);

        logger.LogInformation("Cloud files for upload <{UploadId}> staged on job <{JobId}> and cleaned up.", uploadId, jobId);

        return updatedJob;
    }

    private void ValidateRequest(CloudUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Files == null || request.Files.Count == 0)
            throw new ArgumentException("At least one file must be specified.", nameof(request));

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
