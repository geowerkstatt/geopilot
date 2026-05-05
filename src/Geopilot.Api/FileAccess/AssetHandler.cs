using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides functionality to move, delete and download asset files.
/// </summary>
public class AssetHandler : IAssetHandler
{
    private readonly ILogger<AssetHandler> logger;
    private readonly IProcessingService processingService;
    private readonly IFileProvider temporaryFileProvider;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IContentTypeProvider fileContentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetHandler"/> class.
    /// </summary>
    public AssetHandler(ILogger<AssetHandler> logger, IProcessingService processingService, IFileProvider temporaryFileProvider, IDirectoryProvider directoryProvider, IContentTypeProvider fileContentTypeProvider)
    {
        this.logger = logger;
        this.processingService = processingService;
        this.temporaryFileProvider = temporaryFileProvider;
        this.directoryProvider = directoryProvider;
        this.fileContentTypeProvider = fileContentTypeProvider;
    }

    /// <inheritdoc/>
    public IEnumerable<Asset> PersistJobAssets(Guid jobId)
    {
        var job = processingService.GetJob(jobId);

        if (job is null)
            throw new InvalidOperationException($"Processing job with id {jobId} not found.");

        var assets = new List<Asset>();
        temporaryFileProvider.Initialize(jobId);
        Directory.CreateDirectory(directoryProvider.GetAssetDirectoryPath(jobId));

        assets.AddRange(PersistPrimaryJobAsset(job));
        assets.AddRange(PersistStepDownloadAssets(job));

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

    private List<Asset> PersistPrimaryJobAsset(ProcessingJob job)
    {
        if (job.Files == null || job.Files.Count == 0)
            throw new InvalidOperationException($"Processing job <{job.Id}> does not have a correctly defined primary data files.");

        var assets = new List<Asset>();
        foreach (var f in job.Files)
        {
            using var stream = temporaryFileProvider.Open(f.TempFileName);
            var asset = new Asset()
            {
                AssetType = AssetType.PrimaryData,
                OriginalFilename = f.OriginalFileName,
                SanitizedFilename = f.TempFileName,
                FileHash = SHA256.HashData(stream),
            };
            CopyAssetToPersistentStorage(job.Id, asset);
            assets.Add(asset);
        }

        return assets;
    }

    private List<Asset> PersistStepDownloadAssets(ProcessingJob job)
    {
        var assets = new List<Asset>();
        if (job.Pipeline == null)
            return assets;

        foreach (var step in job.Pipeline.Steps)
        {
            foreach (var persisted in step.DeliveryFiles)
            {
                using var stream = temporaryFileProvider.Open(persisted.PersistedFileName);
                var asset = new Asset()
                {
                    AssetType = AssetType.ValidationReport,
                    OriginalFilename = $"{step.Id}_{persisted.OriginalFileName}",
                    SanitizedFilename = persisted.PersistedFileName,
                    FileHash = SHA256.HashData(stream),
                };
                CopyAssetToPersistentStorage(job.Id, asset);
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
