using GeoCop.Api.Models;
using GeoCop.Api.Validation;
using NetTopologySuite.Utilities;
using System.Security.Cryptography;

namespace GeoCop.Api;

/// <summary>
/// Migrates files deliverd for validation into a persistent storage.
/// </summary>
public class ValidationAssetPersistor : IValidationAssetPersistor
{
    private readonly ILogger<ValidationAssetPersistor> logger;
    private readonly IValidationService validationService;
    private readonly IFileProvider temporaryFileProvider;
    private readonly string uploadDirecory;
    private readonly string assetDicrectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationAssetPersistor"/> class.
    /// </summary>
    /// <param name="logger">The logger used for the instance.</param>
    /// <param name="validationService">Validation service to migrate files from.</param>
    /// <param name="temporaryFileProvider">The provider to access temporary file storage.</param>
    /// <param name="configuration">The service configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown if required configuration values are not defined.</exception>
    public ValidationAssetPersistor(ILogger<ValidationAssetPersistor> logger, IValidationService validationService, IFileProvider temporaryFileProvider, IConfiguration configuration)
    {
        this.logger = logger;
        this.validationService = validationService;
        this.temporaryFileProvider = temporaryFileProvider;
        uploadDirecory = configuration.GetValue<string>("Storage:UploadDirectory")
            ?? throw new InvalidOperationException("Missing root directory for file uploads, the value can be configured as \"Storage:UploadDirectory\"");
        assetDicrectory = configuration.GetValue<string>("Storage:AssetsDirectory")
            ?? throw new InvalidOperationException("Missing root directory for persited assets, the value can be configured as \"Storage:AssetsDirectory\"");
    }

     /// <inheritdoc/>
    public IEnumerable<Asset> PersistValidationJobAssets(Guid jobId)
    {
        var assets = new List<Asset>();
        temporaryFileProvider.Initialize(jobId);
        Directory.CreateDirectory(Path.Combine(assetDicrectory, jobId.ToString()));
        var job = validationService.GetJob(jobId);
        var jobStatus = validationService.GetJobStatus(jobId);

        if (jobStatus is null || job is null)
            throw new InvalidOperationException($"Validation job with id {jobId} not found.");

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
        var sourceFileName = Path.Combine(uploadDirecory, id.ToString(), asset.SanitizedFilename);
        var destFileName = Path.Combine(assetDicrectory, id.ToString(), asset.SanitizedFilename);
        logger.LogInformation("Copying file from {SourceFileName} to {DestFileName}", sourceFileName, destFileName);
        File.Copy(sourceFileName, destFileName);
    }
}
