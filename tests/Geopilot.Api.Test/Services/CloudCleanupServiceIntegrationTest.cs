using Azure.Storage.Blobs;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class CloudCleanupServiceIntegrationTest
{
    private const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=https://localhost:10000/devstoreaccount1;";

    private BlobContainerClient containerClient;
    private AzureBlobStorageService storageService;
    private UploadStore uploadStore;
    private CloudCleanupService cleanupService;
    private string containerName;

    [TestInitialize]
    public void Initialize()
    {
        containerName = $"test-{Guid.NewGuid():N}";

        var storageOptions = new CloudStorageOptions
        {
            ConnectionString = AzuriteConnectionString,
            BucketName = containerName,
            CleanupAgeHours = 48,
            MaxFileSizeMB = 1,
        };

        var optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(storageOptions);

        var blobServiceClient = new BlobServiceClient(AzuriteConnectionString);
        containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();

        storageService = new AzureBlobStorageService(optionsMock.Object, Mock.Of<ILogger<AzureBlobStorageService>>());
        uploadStore = new UploadStore();

        cleanupService = new CloudCleanupService(
            storageService,
            uploadStore,
            new Mock<ILogger<CloudCleanupService>>().Object,
            optionsMock.Object);
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
        var cloudKey = $"uploads/{uploadId}/large.xtf";
        var upload = uploadStore.CreateUpload(uploadId, CloudFiles("large.xtf", cloudKey));
        var oversizedContent = new byte[(1 * 1024 * 1024) + 1];
        await UploadTestBlobAsync(cloudKey, oversizedContent);

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
        var cloudKey = $"uploads/{uploadId}/valid.xtf";
        var upload = uploadStore.CreateUpload(uploadId, CloudFiles("valid.xtf", cloudKey));
        await UploadTestBlobAsync(cloudKey, "small valid file");

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{upload.Id}/");
        Assert.HasCount(1, remaining);
        Assert.IsNotNull(uploadStore.GetUpload(upload.Id));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesBlobsOutsideUploadsPrefix()
    {
        var uploadId = Guid.NewGuid();
        var cloudKey = $"uploads/{uploadId}/valid.xtf";
        uploadStore.CreateUpload(uploadId, CloudFiles("valid.xtf", cloudKey));
        await UploadTestBlobAsync(cloudKey, "keep me");
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

    private static ImmutableList<CloudFileInfo> CloudFiles(string fileName, string cloudKey) =>
        ImmutableList.Create(new CloudFileInfo(fileName, cloudKey, 0));

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
