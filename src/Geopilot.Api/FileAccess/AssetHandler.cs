using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides functionality to move, delete and download asset files.
/// </summary>
public class AssetHandler : IAssetHandler
{
    private readonly ILogger<AssetHandler> logger;
    private readonly IValidationService validationService;
    private readonly IFileProvider temporaryFileProvider;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IContentTypeProvider fileContentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger used for the instance.</param>
    /// <param name="validationService">Validation service to migrate files from.</param>
    /// <param name="temporaryFileProvider">The provider to access temporary file storage.</param>
    /// <param name="directoryProvider">The service configuration.</param>
    /// <param name="fileContentTypeProvider">The file extension content type provider.</param>
    /// <exception cref="InvalidOperationException">Thrown if required configuration values are not defined.</exception>
    public AssetHandler(ILogger<AssetHandler> logger, IValidationService validationService, IFileProvider temporaryFileProvider, IDirectoryProvider directoryProvider, IContentTypeProvider fileContentTypeProvider)
    {
        this.logger = logger;
        this.validationService = validationService;
        this.temporaryFileProvider = temporaryFileProvider;
        this.directoryProvider = directoryProvider;
        this.fileContentTypeProvider = fileContentTypeProvider;
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

    /// <inheritdoc/>
    public void DeleteJobAssets(Guid jobId)
    {
        try
        {
            Directory.Delete(directoryProvider.GetAssetDirectoryPath(jobId), true);
        }
        catch (Exception e)
        {
            var message = $"Failed to delete assets for job <{jobId}>.";
            logger.LogError(e, message);
            throw new InvalidOperationException(message, e);
        }
    }

    /// <inheritdoc/>
    public async Task<(byte[], string)> DownloadAssetAsync(Guid jobId, string assetName)
    {
        try
        {
            var filePath = Path.Combine(directoryProvider.GetAssetDirectoryPath(jobId), assetName);
            if (!File.Exists(filePath)) throw new FileNotFoundException($"File {filePath} not found.");
            var stream = await File.ReadAllBytesAsync(filePath);
            return (stream, fileContentTypeProvider.GetContentTypeAsString(assetName));
        }
        catch (Exception e)
        {
            var message = $"Failed to download asset <{assetName}>.";
            logger.LogError(e, message);
            throw new InvalidOperationException(message, e);
        }
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
                    OriginalFilename = $"{validator.Key}_{logfile.Key}{Path.GetExtension(logfile.Value)}",
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
