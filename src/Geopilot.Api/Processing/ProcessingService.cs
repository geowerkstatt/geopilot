using Geopilot.Api.Services;

namespace Geopilot.Api.Processing;

/// <summary>
/// Provides methods to start, check and access processing jobs.
/// </summary>
public class ProcessingService : IProcessingService
{
    private readonly IProcessingJobStore jobStore;
    private readonly IUploadStore uploadStore;
    private readonly IMandateService mandateService;
    private readonly JobLifecycle lifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingService"/> class.
    /// </summary>
    public ProcessingService(IProcessingJobStore jobStore, IUploadStore uploadStore, IMandateService mandateService, JobLifecycle lifecycle)
    {
        this.jobStore = jobStore;
        this.uploadStore = uploadStore;
        this.mandateService = mandateService;
        this.lifecycle = lifecycle;
    }

    /// <inheritdoc/>
    public async Task<ProcessingJob> StartJobAsync(Guid uploadId, int mandateId, Declarer? declarer, DeliveryFields? deliveryFields)
    {
        var upload = uploadStore.GetUpload(uploadId)
            ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

        var mandate = await mandateService.GetMandateForDeclarerAsync(mandateId, declarer);
        if (mandate?.PipelineId == null)
            throw new InvalidOperationException($"The upload <{uploadId}> could not be started with mandate <{mandateId}>.");

        var isDeclarable = await mandateService.IsDeclarableAsync(mandateId, declarer);
        return await lifecycle.StartAsync(upload, mandate, mandate.PipelineId, declarer, isDeclarable, deliveryFields);
    }

    /// <inheritdoc/>
    public ProcessingJob? GetJob(Guid jobId) => jobStore.GetJob(jobId);

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetSupportedFileExtensionsAsync()
    {
        var mandateFileExtensions = mandateService.GetFileExtensionsForMandates();
        return mandateFileExtensions.OrderBy(ext => ext).ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> IsFileExtensionSupportedAsync(string fileExtension)
    {
        var extensions = await GetSupportedFileExtensionsAsync();
        return IsExtensionSupported(extensions, fileExtension);
    }

    private static bool IsExtensionSupported(ICollection<string> supportedExtensions, string? fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
            return false;

        return supportedExtensions.Any(ext => ext == ".*" || string.Equals(ext, fileExtension, StringComparison.OrdinalIgnoreCase));
    }
}
