namespace Geopilot.Api.FileAccess;

[TestClass]
public sealed class DirectoryProviderTest
{
    [TestMethod]
    public void AssetStagingDirectoryIsBelowTheAssetDirectory()
    {
        // Same volume as the archive on purpose: promoting a staged payload is then a rename, not a copy.
        var provider = AssemblyInitialize.TestDirectoryProvider;

        Assert.AreEqual(Path.Combine(provider.AssetDirectory, "staging"), provider.AssetStagingDirectory);
    }

    [TestMethod]
    public void GetAssetStagingDirectoryPathIsTheJobDirectoryBelowTheStagingRoot()
    {
        var provider = AssemblyInitialize.TestDirectoryProvider;
        var jobId = Guid.NewGuid();

        Assert.AreEqual(Path.Combine(provider.AssetStagingDirectory, jobId.ToString()), provider.GetAssetStagingDirectoryPath(jobId));
    }
}
