using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides functionality to record, delete and download asset files. Pipeline outputs
/// flagged for delivery already live in the asset directory; this handler only needs to
/// migrate the originals over from the upload directory and write the corresponding
/// <see cref="Asset"/> rows.
/// </summary>
public class AssetHandler : IAssetHandler
{
    private readonly ILogger<AssetHandler> logger;
    private readonly IProcessingService processingService;
    private readonly IUploadFileStore uploadFileStore;
    private readonly IAssetFileStore assetFileStore;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IContentTypeProvider fileContentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetHandler"/> class.
    /// </summary>
    public AssetHandler(ILogger<AssetHandler> logger, IProcessingService processingService, IUploadFileStore uploadFileStore, IAssetFileStore assetFileStore, IDirectoryProvider directoryProvider, IContentTypeProvider fileContentTypeProvider)
    {
        this.logger = logger;
        this.processingService = processingService;
        this.uploadFileStore = uploadFileStore;
        this.assetFileStore = assetFileStore;
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
        Directory.CreateDirectory(directoryProvider.GetAssetDirectoryPath(jobId));

        assets.AddRange(PersistPrimaryJobAsset(job));
        assets.AddRange(RecordStepDeliveryAssets(job));

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
            using (var stream = uploadFileStore.OpenFile(job.Id, f.TempFileName))
            {
                var asset = new Asset()
                {
                    AssetType = AssetType.PrimaryData,
                    OriginalFilename = f.OriginalFileName,
                    SanitizedFilename = f.TempFileName,
                    FileHash = SHA256.HashData(stream),
                };
                assets.Add(asset);
            }

            // Originals live in the volatile upload directory; move them over so they
            // survive cleanup alongside the rest of the asset payload.
            CopyUploadToAssetStore(job.Id, f.TempFileName);
        }

        return assets;
    }

    private List<Asset> RecordStepDeliveryAssets(ProcessingJob job)
    {
        var assets = new List<Asset>();
        if (job.Pipeline == null)
            return assets;

        foreach (var step in job.Pipeline.Steps)
        {
            foreach (var persisted in step.DeliveryFiles)
            {
                // Step delivery files were written directly into the asset store by the
                // pipeline runner, so we just hash them in place and create the row.
                using var stream = assetFileStore.OpenFile(job.Id, persisted.PersistedFileName);
                assets.Add(new Asset()
                {
                    AssetType = AssetType.ValidationReport,
                    OriginalFilename = $"{step.Id}_{persisted.OriginalFileName}",
                    SanitizedFilename = persisted.PersistedFileName,
                    FileHash = SHA256.HashData(stream),
                });
            }
        }

        return assets;
    }

    private void CopyUploadToAssetStore(Guid jobId, string fileName)
    {
        var sourceFileName = uploadFileStore.GetPath(jobId, fileName);
        var destFileName = Path.Combine(directoryProvider.GetAssetDirectoryPath(jobId), fileName);
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
