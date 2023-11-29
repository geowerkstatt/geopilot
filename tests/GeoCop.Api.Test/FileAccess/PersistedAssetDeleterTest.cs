using GeoCop.Api.Test;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api.FileAccess;

[TestClass]
public class PersistedAssetDeleterTest
{
    private Mock<ILogger<PersistedAssetDeleter>> loggerMock;
    private PersistedAssetDeleter deleter;
    private Guid jobId;
    private string assetDirectory;

    [TestInitialize]
    public void Setup()
    {
        jobId = Guid.NewGuid();
        assetDirectory = AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(jobId);
        loggerMock = new Mock<ILogger<PersistedAssetDeleter>>();
        deleter = new PersistedAssetDeleter(loggerMock.Object, AssemblyInitialize.TestDirectoryProvider);
    }

    [TestMethod]
    public void DeleteJobAssets()
    {
        Directory.CreateDirectory(assetDirectory);
        File.WriteAllText(Path.Combine(assetDirectory, "TempFileName"), "Some Content");

        deleter.DeleteJobAssets(jobId);

        Assert.IsFalse(Directory.Exists(assetDirectory));
    }
}
