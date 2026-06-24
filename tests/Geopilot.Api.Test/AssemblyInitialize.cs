using Geopilot.Api.FileAccess;
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

        var uploadDirectory = Path.Combine(testContext.DeploymentDirectory, "Uploads");
        var downloadDirectory = Path.Combine(testContext.DeploymentDirectory, "Downloads");
        var visualizationDirectory = Path.Combine(testContext.DeploymentDirectory, "Visualizations");
        var assetDirectory = Path.Combine(testContext.DeploymentDirectory, "Asset");
        var pipelineDirectory = Path.Combine(testContext.DeploymentDirectory, "Pipeline");
        var resourcesDirectory = Path.Combine(testContext.DeploymentDirectory, "Resources");
        var sharedDirectory = Path.Combine(testContext.DeploymentDirectory, "Shared");

        var fileAccessOptions = new FileAccessOptions()
        {
            UploadDirectory = uploadDirectory,
            DownloadDirectory = downloadDirectory,
            VisualizationDirectory = visualizationDirectory,
            AssetsDirectory = assetDirectory,
            PipelineDirectory = pipelineDirectory,
            ResourcesDirectory = resourcesDirectory,
            SharedDirectory = sharedDirectory,
        };

        TestDirectoryProvider = new DirectoryProvider(Options.Create(fileAccessOptions));
        Console.WriteLine($"UploadDirectory: {uploadDirectory}");
        Console.WriteLine($"DownloadDirectory: {downloadDirectory}");
        Console.WriteLine($"AssetDirectory: {assetDirectory}");
        Console.WriteLine($"PipelineDirectory: {pipelineDirectory}");
        Console.WriteLine($"ResourcesDirectory: {resourcesDirectory}");
    }
}
