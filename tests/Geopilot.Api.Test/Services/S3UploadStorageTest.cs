using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Geopilot.Api.Enums;
using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class S3UploadStorageTest
{
    private const string SeaweedFsServiceUrl = "http://localhost:8333";
    private const string SeaweedFsAccessKeyId = "geopilot";
    private const string SeaweedFsSecretAccessKey = "geopilot-dev-secret";
    private const string FrontendOrigin = "https://localhost:5173";

    private AmazonS3Client client;
    private S3UploadStorage service;
    private string bucketName;

    [TestInitialize]
    public async Task Initialize()
    {
        bucketName = $"test-{Guid.NewGuid():N}";
        client = new AmazonS3Client(
            new BasicAWSCredentials(SeaweedFsAccessKeyId, SeaweedFsSecretAccessKey),
            new AmazonS3Config { ServiceURL = SeaweedFsServiceUrl, AuthenticationRegion = "us-east-1", ForcePathStyle = true });
        await client.PutBucketAsync(bucketName);
        service = CreateService(bucketName);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await service.DeletePrefixAsync(string.Empty);
        await client.DeleteBucketAsync(bucketName);
        service.Dispose();
        client.Dispose();
    }

    [TestMethod]
    public async Task GenerateUploadUrlAsyncReturnsWorkingPresignedUrl()
    {
        var key = $"uploads/{Guid.NewGuid()}/test-file.xtf";

        var url = await service.GenerateUploadUrlAsync(key, null, TimeSpan.FromMinutes(10));

        StringAssert.StartsWith(url, $"{SeaweedFsServiceUrl}/{bucketName}/", StringComparison.Ordinal);
        Assert.Contains("X-Amz-Signature=", url);

        using var httpClient = new HttpClient();
        using var content = new ByteArrayContent("test content"u8.ToArray());
        var response = await httpClient.PutAsync(url, content);
        Assert.IsTrue(response.IsSuccessStatusCode, $"Upload via presigned URL failed: {response.StatusCode}");

        var results = await service.ListFilesAsync(key);
        Assert.HasCount(1, results);
        Assert.AreEqual(12, results[0].Size);
    }

    [TestMethod]
    public async Task PresignedUrlAcceptsUploadWithAzureHeader()
    {
        var key = $"uploads/{Guid.NewGuid()}/test-file.xtf";
        var url = await service.GenerateUploadUrlAsync(key, null, TimeSpan.FromMinutes(10));

        using var httpClient = new HttpClient();
        using var content = new ByteArrayContent("test content"u8.ToArray());
        content.Headers.Add("x-ms-blob-type", "BlockBlob");
        var response = await httpClient.PutAsync(url, content);

        Assert.IsTrue(response.IsSuccessStatusCode, $"Upload with Azure header failed: {response.StatusCode}");
    }

    [TestMethod]
    public void UploadHeadersAreEmpty()
    {
        Assert.IsEmpty(service.UploadHeaders);
    }

    [TestMethod]
    public void StorageLocationNamesEndpointAndBucket()
    {
        Assert.AreEqual($"{SeaweedFsServiceUrl}/{bucketName}", service.StorageLocation);
    }

    [TestMethod]
    public async Task ListFilesAsyncReturnsMatchingObjects()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        await UploadTestObjectAsync($"{prefix}file1.xtf", "content1");
        await UploadTestObjectAsync($"{prefix}file2.xtf", "content two");

        var results = await service.ListFilesAsync(prefix);

        Assert.HasCount(2, results);
        Assert.IsTrue(results.Any(r => r.Key == $"{prefix}file1.xtf" && r.Size == 8));
        Assert.IsTrue(results.Any(r => r.Key == $"{prefix}file2.xtf" && r.Size == 11));
        Assert.IsTrue(results.All(r => r.LastModified > DateTime.MinValue && r.LastModified.Kind == DateTimeKind.Utc));
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
        var expectedContent = "hello from seaweedfs"u8.ToArray();
        await UploadTestObjectAsync(key, expectedContent);

        using var destination = new MemoryStream();
        await service.DownloadAsync(key, destination);

        CollectionAssert.AreEqual(expectedContent, destination.ToArray());
    }

    [TestMethod]
    public async Task OpenReadAsyncReturnsContent()
    {
        var key = $"uploads/{Guid.NewGuid()}/test.xtf";
        var expectedContent = "read me"u8.ToArray();
        await UploadTestObjectAsync(key, expectedContent);

        await using var stream = await service.OpenReadAsync(key);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        CollectionAssert.AreEqual(expectedContent, copy.ToArray());
    }

    [TestMethod]
    public async Task DeleteAsyncRemovesSingleObject()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        var key = $"{prefix}file.xtf";
        await UploadTestObjectAsync(key, "data");

        await service.DeleteAsync(key);

        var results = await service.ListFilesAsync(prefix);
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public async Task DeletePrefixAsyncRemovesAllMatchingObjects()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        await UploadTestObjectAsync($"{prefix}file1.xtf", "a");
        await UploadTestObjectAsync($"{prefix}file2.xtf", "b");

        await service.DeletePrefixAsync(prefix);

        var results = await service.ListFilesAsync(prefix);
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public async Task DeletePrefixAsyncDoesNotDeleteUnrelatedObjects()
    {
        var prefix1 = $"uploads/{Guid.NewGuid()}/";
        var prefix2 = $"uploads/{Guid.NewGuid()}/";
        await UploadTestObjectAsync($"{prefix1}file.xtf", "delete me");
        await UploadTestObjectAsync($"{prefix2}file.xtf", "keep me");

        await service.DeletePrefixAsync(prefix1);

        var remaining = await service.ListFilesAsync(prefix2);
        Assert.HasCount(1, remaining);
    }

    [TestMethod]
    public async Task GetTotalSizeAsyncReturnsSumOfSizes()
    {
        var prefix = $"uploads/{Guid.NewGuid()}/";
        await UploadTestObjectAsync($"{prefix}file1.xtf", new byte[100]);
        await UploadTestObjectAsync($"{prefix}file2.xtf", new byte[250]);

        var total = await service.GetTotalSizeAsync(prefix);

        Assert.AreEqual(350, total);
    }

    [TestMethod]
    public async Task StartAsyncCreatesMissingBucketAndAcceptsExistingBucket()
    {
        var newBucketName = $"test-{Guid.NewGuid():N}";
        using var autoCreateService = CreateService(newBucketName, autoCreateContainer: true);

        try
        {
            await autoCreateService.StartAsync(CancellationToken.None);
            await autoCreateService.StartAsync(CancellationToken.None);

            var buckets = await client.ListBucketsAsync();
            Assert.IsTrue(buckets.Buckets.Any(b => b.BucketName == newBucketName));
        }
        finally
        {
            await client.DeleteBucketAsync(newBucketName);
        }
    }

    [TestMethod]
    public async Task StartAsyncAllowsBrowserUploadFromAllowedOrigin()
    {
        using var corsService = CreateService(bucketName, allowedOrigins: [FrontendOrigin]);
        await corsService.StartAsync(CancellationToken.None);

        using var preflight = new HttpRequestMessage(HttpMethod.Options, $"{SeaweedFsServiceUrl}/{bucketName}/uploads/file.xtf");
        preflight.Headers.Add("Origin", FrontendOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "PUT");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var httpClient = new HttpClient();
        using var response = await httpClient.SendAsync(preflight);

        Assert.IsTrue(response.IsSuccessStatusCode, $"CORS preflight failed: {response.StatusCode}");
        Assert.IsTrue(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigin));
        Assert.AreEqual(FrontendOrigin, allowedOrigin.Single());
    }

    private static S3UploadStorage CreateService(string bucketName, bool autoCreateContainer = false, List<string>? allowedOrigins = null)
    {
        var cloudOptions = Options.Create(new UploadCloudOptions
        {
            Provider = CloudStorageProvider.S3,
            BucketName = bucketName,
            AutoCreateContainer = autoCreateContainer,
            AllowedOrigins = allowedOrigins ?? [],
        });
        var s3Options = Options.Create(new UploadS3Options
        {
            ServiceUrl = SeaweedFsServiceUrl,
            Region = "us-east-1",
            AccessKeyId = SeaweedFsAccessKeyId,
            SecretAccessKey = SeaweedFsSecretAccessKey,
            ForcePathStyle = true,
        });

        return new S3UploadStorage(cloudOptions, s3Options, Mock.Of<ILogger<S3UploadStorage>>());
    }

    private async Task UploadTestObjectAsync(string key, string content)
    {
        await UploadTestObjectAsync(key, System.Text.Encoding.UTF8.GetBytes(content));
    }

    private async Task UploadTestObjectAsync(string key, byte[] content)
    {
        using var stream = new MemoryStream(content);
        await client.PutObjectAsync(new PutObjectRequest { BucketName = bucketName, Key = key, InputStream = stream });
    }
}
