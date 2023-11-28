using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoCop.Api;

[TestClass]
public class PersistedAssetDeleterTest
{
    private Mock<ILogger<PersistedAssetDeleter>> loggerMock;
    private IConfigurationRoot configuration;
    private PersistedAssetDeleter deleter;
    private Guid jobId;
    private string assetDirectory;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        jobId = Guid.NewGuid();
        loggerMock = new Mock<ILogger<PersistedAssetDeleter>>();
        assetDirectory = Path.Combine(TestContext.DeploymentDirectory, "Asset");
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        { "Storage:AssetsDirectory", assetDirectory },
    }).Build();

        deleter = new PersistedAssetDeleter(loggerMock.Object, configuration);

        Console.WriteLine($"AssetDirectory: {assetDirectory}");
    }

    [TestMethod]
    public void DeleteJobAssets()
    {
        Directory.CreateDirectory(Path.Combine(assetDirectory, jobId.ToString()));
        File.WriteAllText(Path.Combine(assetDirectory, jobId.ToString(), "TempFileName"), "Some Content");

        deleter.DeleteJobAssets(jobId);

        Assert.IsFalse(Directory.Exists(Path.Combine(assetDirectory, jobId.ToString())));
    }
}
