using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides functionality to record, delete and download asset files. Pipeline outputs
/// flagged for delivery already live in the asset directory; this handler only needs to
/// fetch the uploaded originals from cloud storage and write the corresponding
/// <see cref="Asset"/> rows.
/// </summary>
public class AssetHandler : IAssetHandler
{
    private readonly ILogger<AssetHandler> logger;
    private readonly IProcessingService processingService;
    private readonly ICloudStorageService cloudStorageService;
    private readonly IAssetFileStore assetFileStore;
    private readonly IDirectoryProvider directoryProvider;
    private readonly IContentTypeProvider fileContentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetHandler"/> class.
    /// </summary>
    public AssetHandler(ILogger<AssetHandler> logger, IProcessingService processingService, ICloudStorageService cloudStorageService, IAssetFileStore assetFileStore, IDirectoryProvider directoryProvider, IContentTypeProvider fileContentTypeProvider)
    {
        this.logger = logger;
        this.processingService = processingService;
        this.cloudStorageService = cloudStorageService;
        this.assetFileStore = assetFileStore;
        this.directoryProvider = directoryProvider;
        this.fileContentTypeProvider = fileContentTypeProvider;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Asset>> PersistJobAssetsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = processingService.GetJob(jobId);

        if (job is null)
            throw new InvalidOperationException($"Processing job with id {jobId} not found.");

        var assets = new List<Asset>();
        Directory.CreateDirectory(directoryProvider.GetAssetDirectoryPath(jobId));

        assets.AddRange(await PersistPrimaryJobAssetsAsync(job, cancellationToken));
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

    private async Task<List<Asset>> PersistPrimaryJobAssetsAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        if (job.Files == null || job.Files.Count == 0)
            throw new InvalidOperationException($"Processing job <{job.Id}> does not have a correctly defined primary data files.");

        var assets = new List<Asset>();
        foreach (var f in job.Files)
        {
            assets.Add(new Asset()
            {
                AssetType = AssetType.PrimaryData,
                OriginalFilename = f.OriginalFileName,
                SanitizedFilename = f.TempFileName,
                FileHash = await CopyUploadToAssetStoreAsync(job.Id, f, cancellationToken),
            });
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
                    AssetType = AssetType.ProcessedData,
                    OriginalFilename = $"{step.Id}_{persisted.OriginalFileName}",
                    SanitizedFilename = persisted.PersistedFileName,
                    FileHash = SHA256.HashData(stream),
                });
            }
        }

        return assets;
    }

    /// <summary>
    /// Streams an uploaded original from cloud storage into the asset store, hashing it on the way, so the
    /// delivery keeps its own durable copy. The originals live in cloud storage until the job is retired,
    /// so that is where the delivery has to fetch them from.
    /// </summary>
    /// <returns>The SHA256 hash of the file contents.</returns>
    private async Task<byte[]> CopyUploadToAssetStoreAsync(Guid jobId, ProcessingJobFile file, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Copying uploaded file <{OriginalFileName}> of job <{JobId}> into the asset store.", file.OriginalFileName, jobId);

            using var source = await cloudStorageService.OpenReadAsync(file.CloudKey, cancellationToken);
            using var target = assetFileStore.CreateFile(jobId, file.TempFileName);
            using var hashAlgorithm = SHA256.Create();
            using (var hashing = new CryptoStream(target, hashAlgorithm, CryptoStreamMode.Write, leaveOpen: true))
            {
                await source.CopyToAsync(hashing, cancellationToken);
            }

            return hashAlgorithm.Hash
                ?? throw new InvalidOperationException($"Hashing the uploaded file <{file.OriginalFileName}> of job <{jobId}> produced no result.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to copy uploaded file <{OriginalFileName}> of job <{JobId}> into the asset store.", file.OriginalFileName, jobId);
            throw;
        }
    }
}
