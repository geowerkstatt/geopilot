using Azure.Storage.Blobs;
using Geopilot.Api.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class AzureBlobStorageServiceTest
{
    private const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://localhost:10000/devstoreaccount1;";

    private BlobContainerClient containerClient;
    private AzureBlobStorageService service;
    private string containerName;

    [TestInitialize]
    public void Initialize()
    {
        containerName = $"test-{Guid.NewGuid():N}";

        var options = new CloudStorageOptions
        {
            ConnectionString = AzuriteConnectionString,
            BucketName = containerName,
        };

        var optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var blobServiceClient = new BlobServiceClient(AzuriteConnectionString);
        containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();

        service = new AzureBlobStorageService(optionsMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        containerClient.DeleteIfExists();
    }

    [TestMethod]
    public void ConstructorThrowsWhenConnectionStringMissing()
    {
        var options = new CloudStorageOptions { ConnectionString = "", BucketName = "test" };
        var optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        Assert.ThrowsExactly<InvalidOperationException>(() => new AzureBlobStorageService(optionsMock.Object));
    }

    [TestMethod]
    public void ConstructorThrowsWhenBucketNameMissing()
    {
        var options = new CloudStorageOptions { ConnectionString = AzuriteConnectionString, BucketName = "" };
        var optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        Assert.ThrowsExactly<InvalidOperationException>(() => new AzureBlobStorageService(optionsMock.Object));
    }

    [TestMethod]
    public async Task GeneratePresignedUploadUrlReturnsValidSasUrl()
    {
        var key = "uploads/test-file.xtf";
        var expiresIn = TimeSpan.FromMinutes(10);

        var url = await service.GeneratePresignedUploadUrlAsync(key, null, expiresIn);

        Assert.IsNotNull(url);
        Assert.Contains(key, url);
        Assert.Contains("sig=", url);

        // Verify the presigned URL actually works by uploading through it.
        using var httpClient = new HttpClient();
        using var content = new ByteArrayContent("test content"u8.ToArray());
        content.Headers.Add("x-ms-blob-type", "BlockBlob");
        var response = await httpClient.PutAsync(url, content);
        Assert.IsTrue(response.IsSuccessStatusCode, $"Upload via presigned URL failed: {response.StatusCode}");
    }

    [TestMethod]
    public async Task ListFilesAsyncReturnsMatchingBlobs()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        await UploadTestBlobAsync($"{prefix}file1.xtf", "content1");
        await UploadTestBlobAsync($"{prefix}file2.xtf", "content two");

        var results = await service.ListFilesAsync(prefix);

        Assert.HasCount(2, results);
        Assert.IsTrue(results.Any(r => r.Key == $"{prefix}file1.xtf" && r.Size == 8));
        Assert.IsTrue(results.Any(r => r.Key == $"{prefix}file2.xtf" && r.Size == 11));
        Assert.IsTrue(results.All(r => r.LastModified > DateTime.MinValue));
    }

    [TestMethod]
    public async Task ListFilesAsyncReturnsEmptyForNonexistentPrefix()
    {
        var results = await service.ListFilesAsync("nonexistent/prefix/");

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public async Task DownloadAsyncStreamsContentCorrectly()
    {
        var key = $"uploads/{Guid.NewGuid()}/test.xtf";
        var expectedContent = "hello from azurite"u8.ToArray();
        await UploadTestBlobAsync(key, expectedContent);

        using var destination = new MemoryStream();
        await service.DownloadAsync(key, destination);

        CollectionAssert.AreEqual(expectedContent, destination.ToArray());
    }

    [TestMethod]
    public async Task DeleteAsyncRemovesSingleBlob()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        var key = $"{prefix}file.xtf";
        await UploadTestBlobAsync(key, "data");

        await service.DeleteAsync(key);

        var results = await service.ListFilesAsync(prefix);
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public async Task DeletePrefixAsyncRemovesAllMatchingBlobs()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        await UploadTestBlobAsync($"{prefix}file1.xtf", "a");
        await UploadTestBlobAsync($"{prefix}file2.xtf", "b");

        await service.DeletePrefixAsync(prefix);

        var results = await service.ListFilesAsync(prefix);
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public async Task DeletePrefixAsyncDoesNotDeleteUnrelatedBlobs()
    {
        var prefix1 = $"uploads/{Guid.NewGuid()}/";
        var prefix2 = $"uploads/{Guid.NewGuid()}/";
        await UploadTestBlobAsync($"{prefix1}file.xtf", "delete me");
        await UploadTestBlobAsync($"{prefix2}file.xtf", "keep me");

        await service.DeletePrefixAsync(prefix1);

        var remaining = await service.ListFilesAsync(prefix2);
        Assert.HasCount(1, remaining);
    }

    [TestMethod]
    public async Task GetTotalSizeAsyncReturnsSumOfSizes()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        await UploadTestBlobAsync($"{prefix}file1.xtf", new byte[100]);
        await UploadTestBlobAsync($"{prefix}file2.xtf", new byte[250]);

        var total = await service.GetTotalSizeAsync(prefix);

        Assert.AreEqual(350, total);
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
}
