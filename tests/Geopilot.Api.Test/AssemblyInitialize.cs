using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

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
        var pipelineDirectory = Path.Combine(testContext.DeploymentDirectory, "Pipeline");

        var fileAccessOptions = new FileAccessOptions()
        {
            UploadDirectory = uploadDirectory,
            AssetsDirectory = assetDirectory,
            PipelineDirectory = assetDirectory,
        };

        TestDirectoryProvider = new DirectoryProvider(Options.Create(fileAccessOptions));
        Console.WriteLine($"UploadDirectory: {uploadDirectory}");
        Console.WriteLine($"AssetDirectory: {assetDirectory}");
        Console.WriteLine($"PipelineDirectory: {pipelineDirectory}");
    }
}
