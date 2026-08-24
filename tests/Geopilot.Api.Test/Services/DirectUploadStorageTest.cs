using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;

namespace Geopilot.Api.Services;

[TestClass]
public class DirectUploadStorageTest
{
    private string rootDirectory;
    private DirectUploadStorage storage;

    [TestInitialize]
    public void Initialize()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), $"geopilot-direct-upload-test-{Guid.NewGuid():N}");
        storage = new DirectUploadStorage(
            Options.Create(new UploadDirectOptions { Directory = rootDirectory }),
            Mock.Of<ILogger<DirectUploadStorage>>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(rootDirectory))
            Directory.Delete(rootDirectory, true);
    }

    [TestMethod]
    public async Task GenerateUploadUrlPointsAtApiEndpoint()
    {
        var uploadId = Guid.NewGuid();

        var url = await storage.GenerateUploadUrlAsync($"uploads/{uploadId}/data.xtf", null, TimeSpan.FromMinutes(60));

        Assert.AreEqual($"/api/v2/upload/{uploadId}/data.xtf", url);
    }

    [TestMethod]
    public async Task GenerateUploadUrlEscapesFileName()
    {
        var uploadId = Guid.NewGuid();

        var url = await storage.GenerateUploadUrlAsync($"uploads/{uploadId}/über sicht.xtf", null, TimeSpan.FromMinutes(60));

        StringAssert.StartsWith(url, $"/api/v2/upload/{uploadId}/", StringComparison.Ordinal);
        Assert.IsFalse(url.Contains(' ', StringComparison.Ordinal));
        Assert.IsFalse(url.Contains('ü', StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GenerateUploadUrlThrowsForForeignKeyStructure()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => storage.GenerateUploadUrlAsync("somewhere/else.xtf", null, TimeSpan.FromMinutes(60)));
    }

    [TestMethod]
    public async Task WriteReadRoundtripKeepsContent()
    {
        var key = $"uploads/{Guid.NewGuid()}/data.xtf";

        var written = await storage.WriteAsync(key, ContentStream("hello geopilot"));

        Assert.AreEqual("hello geopilot".Length, written);
        using var readStream = await storage.OpenReadAsync(key);
        using var reader = new StreamReader(readStream);
        Assert.AreEqual("hello geopilot", await reader.ReadToEndAsync());
    }

    [TestMethod]
    public async Task DownloadCopiesContentToDestination()
    {
        var key = $"uploads/{Guid.NewGuid()}/data.xtf";
        await storage.WriteAsync(key, ContentStream("payload"));

        using var destination = new MemoryStream();
        await storage.DownloadAsync(key, destination);

        Assert.AreEqual("payload", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [TestMethod]
    public async Task ListFilesFiltersByPrefixAndReportsSize()
    {
        var uploadId = Guid.NewGuid();
        await storage.WriteAsync($"uploads/{uploadId}/one.xtf", ContentStream("11"));
        await storage.WriteAsync($"uploads/{uploadId}/two.pdf", ContentStream("2222"));
        await storage.WriteAsync($"uploads/{Guid.NewGuid()}/other.xtf", ContentStream("999999"));

        var files = await storage.ListFilesAsync($"uploads/{uploadId}/");

        Assert.HasCount(2, files);
        var one = files.Single(f => f.Key == $"uploads/{uploadId}/one.xtf");
        Assert.AreEqual(2, one.Size);
        Assert.AreEqual(DateTimeKind.Utc, one.LastModified.Kind);
    }

    [TestMethod]
    public async Task GetTotalSizeSumsAllFilesUnderPrefix()
    {
        await storage.WriteAsync($"uploads/{Guid.NewGuid()}/a.xtf", ContentStream("123"));
        await storage.WriteAsync($"uploads/{Guid.NewGuid()}/b.xtf", ContentStream("4567"));

        Assert.AreEqual(7, await storage.GetTotalSizeAsync("uploads/"));
    }

    [TestMethod]
    public async Task DeletePrefixRemovesFilesAndEmptyDirectory()
    {
        var uploadId = Guid.NewGuid();
        await storage.WriteAsync($"uploads/{uploadId}/one.xtf", ContentStream("1"));
        await storage.WriteAsync($"uploads/{uploadId}/two.xtf", ContentStream("2"));

        await storage.DeletePrefixAsync($"uploads/{uploadId}/");

        Assert.IsEmpty(await storage.ListFilesAsync($"uploads/{uploadId}/"));
        Assert.IsFalse(Directory.Exists(Path.Combine(rootDirectory, "uploads", uploadId.ToString())));
    }

    [TestMethod]
    public async Task DeleteIsIdempotentLikeTheCloudBackend()
    {
        await storage.DeleteAsync($"uploads/{Guid.NewGuid()}/missing.xtf");
    }

    [TestMethod]
    public async Task KeyEscapingTheRootDirectoryThrows()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => storage.WriteAsync("uploads/../../evil.xtf", ContentStream("x")));

        Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(rootDirectory)!, "evil.xtf")));
    }

    private static MemoryStream ContentStream(string content) => new(Encoding.UTF8.GetBytes(content));
}
