using Azure.Storage.Blobs;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class CloudCleanupServiceIntegrationTest
{
    private static readonly string AzuriteConnectionString = BuildAzuriteConnectionString();

    private BlobContainerClient containerClient;
    private AzureBlobStorageService storageService;
    private ValidationJobStore jobStore;
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

        storageService = new AzureBlobStorageService(optionsMock.Object);
        jobStore = new ValidationJobStore();

        cleanupService = new CloudCleanupService(
            storageService,
            jobStore,
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
    public async Task RunCleanupAsyncDeletesOversizedBlobsAndRemovesJob()
    {
        var job = jobStore.CreateJob();
        var oversizedContent = new byte[(1 * 1024 * 1024) + 1];
        await UploadTestBlobAsync($"uploads/{job.Id}/large.xtf", oversizedContent);

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{job.Id}/");
        Assert.IsEmpty(remaining);
        Assert.IsNull(jobStore.GetJob(job.Id));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOrphanedBlobsWithNoJob()
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
        var job = jobStore.CreateJob();
        await UploadTestBlobAsync($"uploads/{job.Id}/valid.xtf", "small valid file");

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{job.Id}/");
        Assert.HasCount(1, remaining);
        Assert.IsNotNull(jobStore.GetJob(job.Id));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesBlobsOutsideUploadsPrefix()
    {
        var job = jobStore.CreateJob();
        await UploadTestBlobAsync($"uploads/{job.Id}/valid.xtf", "keep me");
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

    private static string BuildAzuriteConnectionString()
    {
        var protocol = Environment.GetEnvironmentVariable("AZURITE_PROTOCOL") ?? "https";
        return $"DefaultEndpointsProtocol={protocol};AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            $"BlobEndpoint={protocol}://localhost:10000/devstoreaccount1;";
    }
}
