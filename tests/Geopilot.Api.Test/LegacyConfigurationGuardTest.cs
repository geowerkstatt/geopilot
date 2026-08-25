using Microsoft.Extensions.Configuration;

namespace Geopilot.Api;

[TestClass]
public class LegacyConfigurationGuardTest
{
    [TestMethod]
    public void ThrowsWhenLegacyCloudStorageSectionIsPresent()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Upload:MaxFileSizeMB"] = "2048",
            ["CloudStorage:MaxFileSizeMB"] = "2048",
        });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(configuration.EnsureNoLegacyCloudStorageSection);

        StringAssert.Contains(exception.Message, "CloudStorage", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "Upload:Cloud", StringComparison.Ordinal);
    }

    [TestMethod]
    public void PassesForMigratedConfiguration()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Upload:MaxFileSizeMB"] = "2048",
            ["Upload:Cloud:BucketName"] = "uploads",
        });

        configuration.EnsureNoLegacyCloudStorageSection();
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
