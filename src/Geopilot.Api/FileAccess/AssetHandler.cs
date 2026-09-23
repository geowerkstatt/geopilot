using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides functionality to record, delete and download asset files. The delivery consists of exactly
/// the pipeline outputs the definition tagged for delivery: the runner staged those files below the asset
/// directory, so this handler promotes the job's staged directory into place, hashes the files there and
/// writes the corresponding <see cref="Asset"/> rows. Its asset type is derived from the file's origin: a
/// file that entered as an upload becomes <see cref="AssetType.PrimaryData"/>, one produced by a step
/// becomes <see cref="AssetType.ProcessedData"/>.
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
    public async Task<IEnumerable<Asset>> RecordJobAssetsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = processingService.GetJob(jobId);

        if (job is null)
            throw new InvalidOperationException($"Processing job with id {jobId} not found.");

        return await RecordStepDeliveryAssetsAsync(job, cancellationToken);
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

    private async Task<List<Asset>> RecordStepDeliveryAssetsAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var assets = new List<Asset>();
        if (job.Pipeline == null)
            return assets;

        // The declaration is what turns the staged payload into assets: one rename of the job directory,
        // before anything is read from the asset store.
        assetFileStore.PromoteStagedFiles(job.Id);

        foreach (var step in job.Pipeline.Steps)
        {
            foreach (var persisted in step.DeliveryFiles)
            {
                // The promoted files are hashed in place, nothing is copied. A file that entered as an
                // upload is primary data; one produced by a step is processed data.
                using var stream = assetFileStore.OpenFile(job.Id, persisted.PersistedFileName);
                assets.Add(new Asset()
                {
                    AssetType = persisted.FromUpload ? AssetType.PrimaryData : AssetType.ProcessedData,
                    OriginalFilename = persisted.OriginalFileName,
                    SanitizedFilename = persisted.PersistedFileName,
                    FileHash = await SHA256.HashDataAsync(stream, cancellationToken),
                });
            }
        }

        return assets;
    }
}
