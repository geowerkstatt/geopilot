using System.Reflection;

namespace Geopilot.Api.FileAccess;

[TestClass]
public class DataDirectoriesTest
{
    private string root;

    [TestInitialize]
    public void Initialize()
    {
        root = Path.Combine(Path.GetTempPath(), $"geopilot-storage-directories-test-{Guid.NewGuid():N}");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [TestMethod]
    public void LocalCoversEveryStorageDirectoryOption()
    {
        // Read off the type, so a sixth directory cannot be added to the options without being checked.
        var configured = typeof(FileAccessOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => $"Storage:{property.Name}");

        var listed = DataDirectories.Local(CreateOptions()).Select(directory => directory.Key);

        CollectionAssert.AreEquivalent(configured.ToList(), listed.ToList());
    }

    [TestMethod]
    public void LocalOnlyReadsTheResourcesDirectory()
    {
        var readOnly = DataDirectories.Local(CreateOptions())
            .Where(directory => directory.Access == DirectoryAccess.Read)
            .Select(directory => directory.Key);

        CollectionAssert.AreEqual(new[] { "Storage:ResourcesDirectory" }, readOnly.ToList());
    }

    [TestMethod]
    public void EnsureUsableCreatesMissingDirectoriesAndLeavesNoProbe()
    {
        var directories = DataDirectories.Local(CreateOptions());

        DataDirectories.EnsureUsable(directories);

        foreach (var directory in directories)
        {
            Assert.IsTrue(Directory.Exists(directory.Path), directory.Key);
            Assert.IsEmpty(Directory.GetFileSystemEntries(directory.Path), directory.Key);
        }
    }

    [TestMethod]
    public void EnsureUsableNamesTheDirectoryThatCannotBeCreated()
    {
        // A file where the directory should be fails the same way on every platform, without touching permissions.
        Directory.CreateDirectory(root);
        var blocked = Path.Combine(root, "downloads");
        File.WriteAllText(blocked, "not a directory");
        var directories = new[] { new DataDirectory("Storage:DownloadDirectory", blocked, DirectoryAccess.ReadWrite) };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => DataDirectories.EnsureUsable(directories));

        StringAssert.Contains(exception.Message, "Storage:DownloadDirectory");
        StringAssert.Contains(exception.Message, blocked);
    }

    private FileAccessOptions CreateOptions() => new()
    {
        DownloadDirectory = Path.Combine(root, "downloads"),
        VisualizationDirectory = Path.Combine(root, "visualizations"),
        AssetsDirectory = Path.Combine(root, "assets"),
        PipelineDirectory = Path.Combine(root, "pipeline"),
        ResourcesDirectory = Path.Combine(root, "resources"),
    };
}
