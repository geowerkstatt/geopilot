using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class CopyOnWriteFileTest
{
    private string testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "CopyOnWriteFile_" + Guid.NewGuid());
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }

    [TestMethod]
    public void GetLocalPathCopiesIntoConsumingStepAndLeavesOriginalUntouched()
    {
        var foreign = CreateStepFile("stepA", "sub/dir", "data", "txt", "original");
        var originalPath = foreign.GetLocalPath();

        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");
        var copyPath = wrapped.GetLocalPath();

        Assert.AreNotEqual(originalPath, copyPath, "the copy must be a distinct file");
        Assert.IsTrue(copyPath.Contains(Path.Combine(testDirectory, "stepB"), StringComparison.Ordinal), "the copy must live in the consuming step's directory");
        Assert.AreEqual("original", File.ReadAllText(copyPath));

        // Mutating the copy in place (as an external tool would via the path) must not touch the original.
        File.WriteAllText(copyPath, "mutated");

        Assert.AreEqual("mutated", File.ReadAllText(copyPath));
        Assert.AreEqual("original", File.ReadAllText(originalPath), "the originating step's file must stay unchanged");
    }

    [TestMethod]
    public void OpenReadFileStreamReadsOriginalWithoutCopying()
    {
        var foreign = CreateStepFile("stepA", string.Empty, "data", "txt", "content");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        using (var reader = new StreamReader(wrapped.OpenReadFileStream()))
        {
            Assert.AreEqual("content", reader.ReadToEnd());
        }

        Assert.IsFalse(Directory.Exists(Path.Combine(testDirectory, "stepB")), "reading must not create the consuming step's directory or a copy");
    }

    [TestMethod]
    public void GetLocalPathCopiesOnlyOnceOnRepeatedCalls()
    {
        var foreign = CreateStepFile("stepA", string.Empty, "data", "txt", "content");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        var first = wrapped.GetLocalPath();
        var second = wrapped.GetLocalPath();

        Assert.AreEqual(first, second);
        Assert.HasCount(1, Directory.GetFiles(Path.Combine(testDirectory, "stepB")));
    }

    [TestMethod]
    public void PreservesOriginalNameAndRelativePath()
    {
        var foreign = CreateStepFile("stepA", "sub/dir", "data", "xtf", "x");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        _ = wrapped.GetLocalPath();

        Assert.AreEqual("data.xtf", wrapped.OriginalFileName);
        Assert.AreEqual("sub/dir", wrapped.OriginalRelativePath);
        Assert.AreEqual("xtf", wrapped.FileExtension);
    }

    [TestMethod]
    public void OpenWriteFileStreamThrows()
    {
        var foreign = CreateStepFile("stepA", string.Empty, "data", "txt", "content");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        Assert.Throws<NotSupportedException>(() => wrapped.OpenWriteFileStream());
    }

    [TestMethod]
    public void PipelineFileGetLocalPathReturnsOwnPath()
    {
        var file = CreateStepFile("stepA", string.Empty, "data", "txt", "x");

        var path = file.GetLocalPath();

        Assert.IsTrue(path.Contains(Path.Combine(testDirectory, "stepA"), StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void CreateWritableCopyProducesContentIdenticalOwnedCopy()
    {
        var source = CreateStepFile("stepA", string.Empty, "data", "txt", "payload");

        var managerB = new PipelineFileManager(testDirectory, "stepB");
        var copy = managerB.CreateWritableCopy(source, source.OriginalFileNameWithoutExtension);

        Assert.AreNotEqual(source.GetLocalPath(), copy.GetLocalPath());
        Assert.IsTrue(copy.GetLocalPath().Contains(Path.Combine(testDirectory, "stepB"), StringComparison.Ordinal));
        Assert.AreEqual("payload", File.ReadAllText(copy.GetLocalPath()));
        Assert.AreEqual("data.txt", copy.OriginalFileName);
    }

    private IPipelineFile CreateStepFile(string stepId, string relativePath, string name, string extension, string content)
    {
        var manager = new PipelineFileManager(testDirectory, stepId);
        var file = manager.GeneratePipelineFile(relativePath, name, extension);
        using (var writer = new StreamWriter(file.OpenWriteFileStream()))
        {
            writer.Write(content);
        }

        return file;
    }
}
