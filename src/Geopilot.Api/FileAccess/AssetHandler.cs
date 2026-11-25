using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides functionality to record, delete and download asset files. The delivery consists of exactly
/// the pipeline outputs the definition tagged for delivery: the runner staged those files below the asset
/// directory, so this handler hashes them there into <see cref="Asset"/> rows and, once the delivery is
/// saved, promotes the job's staged directory into place. Its asset type is derived from the file's origin: a
/// file that entered as an upload becomes <see cref="AssetType.PrimaryData"/>, one produced by a step
/// becomes <see cref="AssetType.ProcessedData"/>.
/// </summary>
public class AssetHandler : IAssetHandler
{
    private readonly ILogger<AssetHandler> logger;
    private readonly Context context;
    private readonly IAssetFileStore assetFileStore;
    private readonly IAssetStagingFileStore assetStagingFileStore;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IContentTypeProvider fileContentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetHandler"/> class.
    /// </summary>
    public AssetHandler(ILogger<AssetHandler> logger, Context context, IAssetFileStore assetFileStore, IAssetStagingFileStore assetStagingFileStore, IDirectoryProvider directoryProvider, IContentTypeProvider fileContentTypeProvider)
    {
        this.logger = logger;
        this.context = context;
        this.assetFileStore = assetFileStore;
        this.assetStagingFileStore = assetStagingFileStore;
        this.directoryProvider = directoryProvider;
        this.fileContentTypeProvider = fileContentTypeProvider;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Asset>> RecordJobAssetsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!await context.Recordings.AnyAsync(r => r.JobId == jobId, cancellationToken))
            throw new InvalidOperationException($"Processing job with id {jobId} not found.");

        var deliveryFiles = await context.RecordingArtifacts
            .AsNoTracking()
            .Where(a => a.Kind == ArtifactKind.Delivery && a.Step.Recording.JobId == jobId)
            .OrderBy(a => a.Step.Order)
            .ThenBy(a => a.Id)
            .Select(a => new { a.PersistedFileName, a.FromUpload })
            .ToListAsync(cancellationToken);

        var assets = new List<Asset>();
        foreach (var file in deliveryFiles)
        {
            // The files are hashed in staging, nothing is copied: they move into the delivery directory only once
            // the delivery is saved. A file that entered as an upload is primary data; one produced by a step is
            // processed data.
            using var stream = assetStagingFileStore.OpenFile(jobId, file.PersistedFileName);
            assets.Add(new Asset()
            {
                AssetType = file.FromUpload == true ? AssetType.PrimaryData : AssetType.ProcessedData,
                OriginalFilename = file.PersistedFileName,
                SanitizedFilename = file.PersistedFileName,
                FileHash = await SHA256.HashDataAsync(stream, cancellationToken),
            });
        }

        return assets;
    }

    /// <inheritdoc/>
    public void PromoteJobAssets(Guid jobId) => assetFileStore.PromoteStagedFiles(jobId);

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
}
