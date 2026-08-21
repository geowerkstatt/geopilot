using Azure.Storage.Blobs;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class UploadCleanupServiceIntegrationTest
{
    private const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=https://localhost:10000/devstoreaccount1;";

    private BlobContainerClient containerClient;
    private AzureBlobUploadStorage storageService;
    private UploadStore uploadStore;
    private UploadCleanupService cleanupService;
    private string containerName;

    [TestInitialize]
    public void Initialize()
    {
        containerName = $"test-{Guid.NewGuid():N}";

        var cloudOptions = new UploadCloudOptions
        {
            ConnectionString = AzuriteConnectionString,
            BucketName = containerName,
        };

        var uploadOptions = new UploadOptions
        {
            CleanupAgeHours = 48,
            MaxFileSizeMB = 1,
        };

        var blobServiceClient = new BlobServiceClient(AzuriteConnectionString);
        containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();

        storageService = new AzureBlobUploadStorage(Options.Create(cloudOptions), Mock.Of<ILogger<AzureBlobUploadStorage>>());
        uploadStore = new UploadStore();

        cleanupService = new UploadCleanupService(
            storageService,
            uploadStore,
            new Mock<ILogger<UploadCleanupService>>().Object,
            Options.Create(uploadOptions),
            Options.Create(new ProcessingOptions { JobRetention = TimeSpan.FromHours(24), JobTimeout = TimeSpan.FromMinutes(30) }));
    }

    [TestCleanup]
    public void Cleanup()
    {
        cleanupService.Dispose();
        containerClient.DeleteIfExists();
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOversizedBlobsAndRemovesUpload()
    {
        var uploadId = Guid.NewGuid();
        var storageKey = $"uploads/{uploadId}/large.xtf";
        var upload = uploadStore.CreateUpload(uploadId, UploadedFiles("large.xtf", storageKey));
        var oversizedContent = new byte[(1 * 1024 * 1024) + 1];
        await UploadTestBlobAsync(storageKey, oversizedContent);

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{upload.Id}/");
        Assert.IsEmpty(remaining);
        Assert.IsNull(uploadStore.GetUpload(upload.Id));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOrphanedBlobsWithNoUpload()
    {
        var orphanId = Guid.NewGuid();
        await UploadTestBlobAsync($"uploads/{orphanId}/orphan.xtf", "orphaned data");

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{orphanId}/");
        Assert.IsEmpty(remaining);
    }

    [TestMethod]
    public async Task RunCleanupAsyncPreservesRecentValidBlobs()
    {
        var uploadId = Guid.NewGuid();
        var storageKey = $"uploads/{uploadId}/valid.xtf";
        var upload = uploadStore.CreateUpload(uploadId, UploadedFiles("valid.xtf", storageKey));
        await UploadTestBlobAsync(storageKey, "small valid file");

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{upload.Id}/");
        Assert.HasCount(1, remaining);
        Assert.IsNotNull(uploadStore.GetUpload(upload.Id));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesBlobsOutsideUploadsPrefix()
    {
        var uploadId = Guid.NewGuid();
        var storageKey = $"uploads/{uploadId}/valid.xtf";
        uploadStore.CreateUpload(uploadId, UploadedFiles("valid.xtf", storageKey));
        await UploadTestBlobAsync(storageKey, "keep me");
        await UploadTestBlobAsync("rogue/file.txt", "delete me");

        await cleanupService.RunCleanupAsync();

        var allFiles = await storageService.ListFilesAsync(string.Empty);
        Assert.IsTrue(allFiles.All(f => f.Key.StartsWith("uploads/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesInvalidPrefixBlobs()
    {
        await UploadTestBlobAsync("uploads/not-a-guid/file.xtf", "invalid path");

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync("uploads/not-a-guid/");
        Assert.IsEmpty(remaining);
    }

    private static ImmutableList<UploadedFileInfo> UploadedFiles(string fileName, string storageKey) =>
        ImmutableList.Create(new UploadedFileInfo(fileName, storageKey, 0));

    private async Task UploadTestBlobAsync(string key, string content)
    {
        await UploadTestBlobAsync(key, System.Text.Encoding.UTF8.GetBytes(content));
    }

    private async Task UploadTestBlobAsync(string key, byte[] content)
    {
        var blobClient = containerClient.GetBlobClient(key);
        using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, overwrite: true);
    }
}
