using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geopilot.Api;

[TestClass]
public class StorageHealthCheckTest
{
    private string root;

    [TestInitialize]
    public void Initialize()
    {
        root = Path.Combine(Path.GetTempPath(), $"geopilot-storage-health-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [TestMethod]
    public void HealthyWhenEveryDirectoryExists()
    {
        var result = StorageHealthCheck.Check([new DataDirectory("Storage:PipelineDirectory", root, DirectoryAccess.ReadWrite)], NullLogger.Instance);

        Assert.AreEqual(HealthStatus.Healthy, result.Status);
    }

    [TestMethod]
    public void UnhealthyNamesTheMissingKeyButNotItsPath()
    {
        var absent = Path.Combine(root, "absent-visualizations");
        var directories = new[]
        {
            new DataDirectory("Storage:PipelineDirectory", root, DirectoryAccess.ReadWrite),
            new DataDirectory("Storage:VisualizationDirectory", absent, DirectoryAccess.ReadWrite),
        };

        var result = StorageHealthCheck.Check(directories, NullLogger.Instance);

        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        Assert.AreEqual("Missing: Storage:VisualizationDirectory", result.Description);
    }
}
