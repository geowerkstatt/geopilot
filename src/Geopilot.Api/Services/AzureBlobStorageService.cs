using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// Azure Blob Storage implementation of <see cref="ICloudStorageService"/>.
/// </summary>
public class AzureBlobStorageService : ICloudStorageService
{
    private readonly BlobContainerClient containerClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobStorageService"/> class.
    /// </summary>
    public AzureBlobStorageService(IOptions<CloudStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
            throw new InvalidOperationException("CloudStorage:ConnectionString is not configured.");
        if (string.IsNullOrWhiteSpace(config.BucketName))
            throw new InvalidOperationException("CloudStorage:BucketName is not configured.");

        var serviceClient = new BlobServiceClient(config.ConnectionString);
        containerClient = serviceClient.GetBlobContainerClient(config.BucketName);
    }

    /// <inheritdoc/>
    public Task<string> GeneratePresignedUploadUrlAsync(string key, string? contentType, TimeSpan expiresIn)
    {
        var blobClient = containerClient.GetBlobClient(key);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerClient.Name,
            BlobName = key,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiresIn),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        if (!string.IsNullOrEmpty(contentType))
            sasBuilder.ContentType = contentType;

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(string Key, long Size, DateTime LastModified)>> ListFilesAsync(string prefix)
    {
        var results = new List<(string Key, long Size, DateTime LastModified)>();

        await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix))
        {
            results.Add((blob.Name, blob.Properties.ContentLength ?? 0, blob.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(string key, Stream destination)
    {
        var blobClient = containerClient.GetBlobClient(key);
        await blobClient.DownloadToAsync(destination);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key)
    {
        var blobClient = containerClient.GetBlobClient(key);
        await blobClient.DeleteIfExistsAsync();
    }

    /// <inheritdoc/>
    public async Task DeletePrefixAsync(string prefix)
    {
        await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix))
        {
            await containerClient.DeleteBlobIfExistsAsync(blob.Name);
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetTotalSizeAsync(string prefix)
    {
        long total = 0;

        await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix))
        {
            total += blob.Properties.ContentLength ?? 0;
        }

        return total;
    }
}
