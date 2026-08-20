using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides functionality to record, delete and download asset files. Pipeline outputs
/// flagged for delivery already live in the asset directory; this handler only needs to
/// fetch the uploaded originals from the upload storage and write the corresponding
/// <see cref="Asset"/> rows.
/// </summary>
public class AssetHandler : IAssetHandler
{
    private readonly ILogger<AssetHandler> logger;
    private readonly IProcessingService processingService;
    private readonly IAssetFileStore assetFileStore;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IContentTypeProvider fileContentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetHandler"/> class.
    /// </summary>
    public AssetHandler(ILogger<AssetHandler> logger, IProcessingService processingService, IAssetFileStore assetFileStore, IDirectoryProvider directoryProvider, IContentTypeProvider fileContentTypeProvider)
    {
        this.logger = logger;
        this.processingService = processingService;
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

        Directory.CreateDirectory(directoryProvider.GetAssetDirectoryPath(jobId));

        return RecordStepDeliveryAssets(job);
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
                // pipeline runner, so we just hash them in place and create the row. A file
                // that entered as an upload is primary data; one produced by a step is processed data.
                using var stream = assetFileStore.OpenFile(job.Id, persisted.PersistedFileName);
                assets.Add(new Asset()
                {
                    AssetType = persisted.FromUpload ? AssetType.PrimaryData : AssetType.ProcessedData,
                    OriginalFilename = persisted.OriginalFileName,
                    SanitizedFilename = persisted.PersistedFileName,
                    FileHash = SHA256.HashData(stream),
                });
            }
        }

        return assets;
    }
}
