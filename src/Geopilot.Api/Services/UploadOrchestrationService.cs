using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Options;
using System.Collections.Immutable;

namespace Geopilot.Api.Services;

/// <summary>
/// Orchestrates upload sessions including initiation, preflight checks, and handing the uploaded
/// files to a processing job.
/// </summary>
public class UploadOrchestrationService : IUploadOrchestrationService
{
    /// <summary>
    /// Subdirectory of the job's pipeline working directory that uploaded files are fetched into.
    /// It is removed together with the rest of the working directory when the pipeline is disposed.
    /// </summary>
    private const string UploadWorkingDirectoryName = "upload";

    private readonly IUploadStorage uploadStorage;
    private readonly IUploadScanService scanService;
    private readonly IProcessingJobStore jobStore;
    private readonly IUploadStore uploadStore;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IOptions<UploadOptions> options;
    private readonly ILogger<UploadOrchestrationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadOrchestrationService"/> class.
    /// </summary>
    public UploadOrchestrationService(
        IUploadStorage uploadStorage,
        IUploadScanService scanService,
        IProcessingJobStore jobStore,
        IUploadStore uploadStore,
        IDirectoryProvider directoryProvider,
        IOptions<UploadOptions> options,
        ILogger<UploadOrchestrationService> logger)
    {
        this.uploadStorage = uploadStorage;
        this.scanService = scanService;
        this.jobStore = jobStore;
        this.uploadStore = uploadStore;
        this.directoryProvider = directoryProvider;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<InitiateUploadResponse> InitiateUploadAsync(InitiateUploadRequest request)
    {
        ValidateRequest(request);

        var activeUploads = uploadStore.GetActiveUploadCount();
        if (activeUploads >= options.Value.MaxActiveJobs)
            throw new InvalidOperationException($"Maximum number of active uploads ({options.Value.MaxActiveJobs}) reached.");

        var declaredTotalSize = request.Files.Sum(f => f.Size);
        var maxGlobalBytes = (long)options.Value.MaxGlobalActiveSizeMB * 1024 * 1024;
        var currentSize = await uploadStorage.GetTotalSizeAsync("uploads/");
        if (currentSize + declaredTotalSize > maxGlobalBytes)
            throw new InvalidOperationException($"Global active upload size limit ({options.Value.MaxGlobalActiveSizeMB} MB) would be exceeded.");

        var uploadId = Guid.NewGuid();
        var keyPrefix = $"uploads/{uploadId}/";

        var uploadedFiles = new List<UploadedFileInfo>();
        var fileUploadInfos = new List<FileUploadInfo>();
        var expiresIn = TimeSpan.FromMinutes(options.Value.UploadUrlExpiryMinutes);

        foreach (var file in request.Files)
        {
            var sanitizedName = Path.GetFileName(file.FileName);
            var storageKey = $"{keyPrefix}{sanitizedName}";
            var presignedUrl = await uploadStorage.GenerateUploadUrlAsync(storageKey, null, expiresIn);

            uploadedFiles.Add(new UploadedFileInfo(sanitizedName, storageKey, file.Size));
            fileUploadInfos.Add(new FileUploadInfo(sanitizedName, presignedUrl));
        }

        uploadStore.CreateUpload(uploadId, uploadedFiles.ToImmutableList());

        logger.LogInformation("Initiated upload <{UploadId}> with {FileCount} file(s).", uploadId, request.Files.Count);

        return new InitiateUploadResponse(uploadId, fileUploadInfos, DateTime.UtcNow.Add(expiresIn));
    }

    /// <inheritdoc/>
    public async Task RunPreflightChecksAsync(Guid uploadId)
    {
        var upload = uploadStore.GetUpload(uploadId) ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

        if (upload.Files.Count == 0)
            throw new InvalidOperationException($"Upload <{uploadId}> has no files configured.");

        logger.LogInformation("Starting preflight checks for upload <{UploadId}>.", uploadId);

        var keyPrefix = $"uploads/{uploadId}/";
        var uploadedFiles = await uploadStorage.ListFilesAsync(keyPrefix);

        foreach (var expectedFile in upload.Files)
        {
            var uploaded = uploadedFiles.FirstOrDefault(f => f.Key == expectedFile.StorageKey);
            if (uploaded == default)
            {
                throw new UploadPreflightException(PreflightFailureReason.IncompleteUpload, $"File '{expectedFile.FileName}' was not uploaded.");
            }

            if (uploaded.Size < expectedFile.ExpectedSize)
            {
                throw new UploadPreflightException(PreflightFailureReason.IncompleteUpload, $"File '{expectedFile.FileName}' is incomplete.");
            }

            if (uploaded.Size > expectedFile.ExpectedSize)
            {
                logger.LogError("File '{FileName}' for upload <{UploadId}> exceeds declared size ({Actual} > {Expected}).", expectedFile.FileName, uploadId, uploaded.Size, expectedFile.ExpectedSize);
                throw new UploadPreflightException(PreflightFailureReason.SizeExceeded, "The uploaded files could not be processed.");
            }
        }

        var keys = upload.Files.Select(f => f.StorageKey).ToList();
        var scanResult = await scanService.CheckFilesAsync(keys);

        if (!scanResult.IsClean)
        {
            logger.LogError("Threat detected in files of upload <{UploadId}>: {ThreatDetails}", uploadId, scanResult.ThreatDetails);
            throw new UploadPreflightException(PreflightFailureReason.ThreatDetected, "The uploaded files could not be processed.");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IPipelineFile> RegisterJobFiles(Guid uploadId, Guid jobId)
    {
        var upload = uploadStore.GetUpload(uploadId) ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));
        var job = jobStore.GetJob(jobId) ?? throw new ArgumentException($"Job with id <{jobId}> not found.", nameof(jobId));
        if (job.State != Pipeline.ProcessingState.Pending)
            throw new InvalidOperationException($"Cannot register files for job <{jobId}> because it is not in the pending state.");

        if (upload.Files.Count == 0)
            throw new InvalidOperationException($"Upload <{uploadId}> has no files to register.");

        var materializationDirectory = Path.Combine(directoryProvider.GetPipelineDirectoryPath(jobId), UploadWorkingDirectoryName);

        // Case-insensitive, because the name becomes a file name on the host and in the asset store.
        // On Windows and macOS two names differing only in case address the same file.
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pipelineFiles = new List<IPipelineFile>(upload.Files.Count);

        foreach (var file in upload.Files)
        {
            var localName = UploadFileNaming.MakeUnique(file.FileName, usedNames);
            pipelineFiles.Add(new UploadPipelineFile(uploadStorage, file.StorageKey, file.FileName, materializationDirectory, localName));
        }

        logger.LogInformation("Registered {FileCount} file(s) of upload <{UploadId}> on job <{JobId}>.", pipelineFiles.Count, uploadId, jobId);

        return pipelineFiles;
    }

    /// <inheritdoc/>
    public async Task ReleaseUploadAsync(Guid uploadId)
    {
        try
        {
            await uploadStorage.DeletePrefixAsync($"uploads/{uploadId}/");
            uploadStore.RemoveUpload(uploadId);
            logger.LogInformation("Released the files of upload <{UploadId}>.", uploadId);
        }
        catch (Exception ex)
        {
            // The age-based sweep in UploadCleanupService picks the blobs up later.
            logger.LogError(ex, "Failed to release the files of upload <{UploadId}>.", uploadId);
        }
    }

    private void ValidateRequest(InitiateUploadRequest request)
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
