using GeoCop.Api.Models;
using GeoCop.Api.Validation;
using System.Security.Cryptography;

namespace GeoCop.Api.FileAccess;

/// <summary>
/// Migrates files delivered for validation into a persistent storage.
/// </summary>
public class ValidationAssetPersistor : IValidationAssetPersistor
{
    private readonly ILogger<ValidationAssetPersistor> logger;
    private readonly IValidationService validationService;
    private readonly IFileProvider temporaryFileProvider;

    private readonly IDirectoryProvider directoryProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationAssetPersistor"/> class.
    /// </summary>
    /// <param name="logger">The logger used for the instance.</param>
    /// <param name="validationService">Validation service to migrate files from.</param>
    /// <param name="temporaryFileProvider">The provider to access temporary file storage.</param>
    /// <param name="directoryProvider">The service configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown if required configuration values are not defined.</exception>
    public ValidationAssetPersistor(ILogger<ValidationAssetPersistor> logger, IValidationService validationService, IFileProvider temporaryFileProvider, IDirectoryProvider directoryProvider)
    {
        this.logger = logger;
        this.validationService = validationService;
        this.temporaryFileProvider = temporaryFileProvider;
        this.directoryProvider = directoryProvider;
    }

    /// <inheritdoc/>
    public IEnumerable<Asset> PersistJobAssets(Guid jobId)
    {
        var job = validationService.GetJob(jobId);
        var jobStatus = validationService.GetJobStatus(jobId);

        if (jobStatus is null || job is null)
            throw new InvalidOperationException($"Validation job with id {jobId} not found.");

        var assets = new List<Asset>();
        temporaryFileProvider.Initialize(jobId);
        Directory.CreateDirectory(directoryProvider.GetAssetDirectoryPath(jobId));

        assets.Add(PersistPrimaryValidationJobAsset(job));
        assets.AddRange(PersistValidationJobValidatorAssets(jobStatus));

        return assets;
    }

    /// <summary>
    /// Migrates the primary data file for a validation job into a persistent storage.
    /// </summary>
    /// <param name="job">The validation job created during upload.</param>
    /// <returns>Calculated Asset representing the file in persistent storage.</returns>
    private Asset PersistPrimaryValidationJobAsset(ValidationJob job)
    {
        using var stream = temporaryFileProvider.Open(job.TempFileName);
        var asset = new Asset()
        {
            AssetType = AssetType.PrimaryData,
            OriginalFilename = job.OriginalFileName,
            SanitizedFilename = job.TempFileName,
            FileHash = SHA256.HashData(stream),
        };
        CopyAssetToPersistentStorage(job.Id, asset);
        return asset;
    }

    /// <summary>
    /// Migrates all log files for a validation job into a persistent storage.
    /// </summary>
    /// <param name="jobStatus">The validation job status containing information about created validation assets.</param>
    /// <returns>List of Assets representing the log files in persistent storage.</returns>
    private List<Asset> PersistValidationJobValidatorAssets(ValidationJobStatus jobStatus)
    {
        var assets = new List<Asset>();

        foreach (var validator in jobStatus.ValidatorResults)
        {
            foreach (var logfile in validator.Value.LogFiles)
            {
                using var stream = temporaryFileProvider.Open(logfile.Value);
                var asset = new Asset()
                {
                    AssetType = AssetType.ValidationReport,
                    OriginalFilename = $"{validator.Key}_{logfile.Key}",
                    SanitizedFilename = logfile.Value,
                    FileHash = SHA256.HashData(stream),
                };
                CopyAssetToPersistentStorage(jobStatus.JobId, asset);
                assets.Add(asset);
            }
        }

        return assets;
    }

    private void CopyAssetToPersistentStorage(Guid id, Asset asset)
    {
        var sourceFileName = Path.Combine(directoryProvider.GetUploadDirectoryPath(id), asset.SanitizedFilename);
        var destFileName = Path.Combine(directoryProvider.GetAssetDirectoryPath(id), asset.SanitizedFilename);
        try
        {
            logger.LogInformation("Copying file from {SourceFileName} to {DestFileName}", sourceFileName, destFileName);
            File.Copy(sourceFileName, destFileName);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to copy <{SourceFileName}> to <{DestinationFileName}>.", sourceFileName, destFileName);
            throw;
        }
    }
}
