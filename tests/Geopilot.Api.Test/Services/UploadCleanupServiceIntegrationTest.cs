using Azure.Storage.Blobs;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.Extensions.DependencyInjection;
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
    private Context context;
    private UploadStore uploadStore;
    private UploadOptions uploadOptions;
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

        uploadOptions = new UploadOptions
        {
            CleanupAgeHours = 48,
            MaxFileSizeMB = 1,
        };

        var blobServiceClient = new BlobServiceClient(AzuriteConnectionString);
        containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();

        storageService = new AzureBlobUploadStorage(Options.Create(cloudOptions), Mock.Of<ILogger<AzureBlobUploadStorage>>());
        context = AssemblyInitialize.DbFixture.GetTestContext();
        uploadStore = new UploadStore(context);
        cleanupService = CreateCleanupService(uploadStore);
    }

    [TestCleanup]
    public void Cleanup()
    {
        cleanupService.Dispose();
        containerClient.DeleteIfExists();
        context.Dispose();
    }

    [TestMethod]
    public async Task RunCleanupAsyncKeepsTheFilesOfAnUploadAfterARestart()
    {
        var uploadId = Guid.NewGuid();
        var storageKey = $"uploads/{uploadId}/valid.xtf";
        await new UploadStore(context).CreateUploadAsync(uploadId, UploadedFiles("valid.xtf", storageKey));
        await UploadTestBlobAsync(storageKey, "initiated before the restart");

        // A new service with a new store, as after a restart of the process.
        using var restarted = CreateCleanupService(new UploadStore(context));
        await restarted.RunCleanupAsync();

        Assert.HasCount(1, await storageService.ListFilesAsync($"uploads/{uploadId}/"));
        Assert.IsNotNull(await new UploadStore(context).GetUploadAsync(uploadId));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOversizedBlobsAndRemovesUpload()
    {
        var uploadId = Guid.NewGuid();
        var storageKey = $"uploads/{uploadId}/large.xtf";
        var upload = await uploadStore.CreateUploadAsync(uploadId, UploadedFiles("large.xtf", storageKey));
        var oversizedContent = new byte[(1 * 1024 * 1024) + 1];
        await UploadTestBlobAsync(storageKey, oversizedContent);

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{upload.Id}/");
        Assert.IsEmpty(remaining);
        Assert.IsNull(await uploadStore.GetUploadAsync(upload.Id));
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
        var upload = await uploadStore.CreateUploadAsync(uploadId, UploadedFiles("valid.xtf", storageKey));
        await UploadTestBlobAsync(storageKey, "small valid file");

        await cleanupService.RunCleanupAsync();

        var remaining = await storageService.ListFilesAsync($"uploads/{upload.Id}/");
        Assert.HasCount(1, remaining);
        Assert.IsNotNull(await uploadStore.GetUploadAsync(upload.Id));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesBlobsOutsideUploadsPrefix()
    {
        var uploadId = Guid.NewGuid();
        var storageKey = $"uploads/{uploadId}/valid.xtf";
        await uploadStore.CreateUploadAsync(uploadId, UploadedFiles("valid.xtf", storageKey));
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

    private UploadCleanupService CreateCleanupService(IUploadStore store)
    {
        // The service resolves the store per run from a scope, as it does against the scoped database context.
        var scopeFactory = new ServiceCollection().AddSingleton(store).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new UploadCleanupService(
            storageService,
            scopeFactory,
            new Mock<ILogger<UploadCleanupService>>().Object,
            Options.Create(uploadOptions),
            Options.Create(new ProcessingOptions { JobRetention = TimeSpan.FromHours(24), JobTimeout = TimeSpan.FromMinutes(30) }));
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
