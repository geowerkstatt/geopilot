using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Configuration;

namespace Geopilot.Api;

[TestClass]
public sealed class AssemblyInitialize
{
    public static TestDatabaseFixture DbFixture { get; private set; }

    public static IDirectoryProvider TestDirectoryProvider { get; private set; }

    [AssemblyInitialize]
    public static void TestSetup(TestContext testContext)
    {
        DbFixture = new TestDatabaseFixture();

        var uploadDirectory = Path.Combine(testContext.DeploymentDirectory, "Upload");
        var assetDirectory = Path.Combine(testContext.DeploymentDirectory, "Asset");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Storage:UploadDirectory", uploadDirectory },
                { "Storage:AssetsDirectory", assetDirectory },
            })
            .Build();

        TestDirectoryProvider = new DirectoryProvider(configuration);
        Console.WriteLine($"UploadDirectory: {uploadDirectory}");
        Console.WriteLine($"AssetDirectory: {assetDirectory}");
    }
}
