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
    public async Task GetLocalPathCopiesIntoConsumingStepAndLeavesOriginalUntouched()
    {
        var foreign = CreateStepFile("stepA", "sub/dir", "data", "txt", "original");
        var originalPath = await foreign.GetLocalPathAsync();

        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");
        var copyPath = await wrapped.GetLocalPathAsync();

        Assert.AreNotEqual(originalPath, copyPath, "the copy must be a distinct file");
        Assert.IsTrue(copyPath.Contains(Path.Combine(testDirectory, "stepB"), StringComparison.Ordinal), "the copy must live in the consuming step's directory");
        Assert.AreEqual("original", File.ReadAllText(copyPath));

        // Mutating the copy in place (as an external tool would via the path) must not touch the original.
        File.WriteAllText(copyPath, "mutated");

        Assert.AreEqual("mutated", File.ReadAllText(copyPath));
        Assert.AreEqual("original", File.ReadAllText(originalPath), "the originating step's file must stay unchanged");
    }

    [TestMethod]
    public async Task OpenReadReadsOriginalWithoutCopying()
    {
        var foreign = CreateStepFile("stepA", string.Empty, "data", "txt", "content");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        using (var reader = new StreamReader(await wrapped.OpenReadAsync()))
        {
            Assert.AreEqual("content", reader.ReadToEnd());
        }

        Assert.IsFalse(Directory.Exists(Path.Combine(testDirectory, "stepB")), "reading must not create the consuming step's directory or a copy");
    }

    [TestMethod]
    public async Task GetLocalPathCopiesOnlyOnceOnRepeatedCalls()
    {
        var foreign = CreateStepFile("stepA", string.Empty, "data", "txt", "content");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        var first = await wrapped.GetLocalPathAsync();
        var second = await wrapped.GetLocalPathAsync();

        Assert.AreEqual(first, second);
        Assert.HasCount(1, Directory.GetFiles(Path.Combine(testDirectory, "stepB")));
    }

    [TestMethod]
    public async Task GetLocalPathCopiesOnlyOnceOnConcurrentCalls()
    {
        var foreign = CreateStepFile("stepA", string.Empty, "data", "txt", "content");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        var paths = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => wrapped.GetLocalPathAsync())));

        Assert.HasCount(1, paths.Distinct().ToList(), "concurrent callers must share the one copy");
        Assert.HasCount(1, Directory.GetFiles(Path.Combine(testDirectory, "stepB")));
    }

    [TestMethod]
    public async Task PreservesOriginalNameAndRelativePath()
    {
        var foreign = CreateStepFile("stepA", "sub/dir", "data", "xtf", "x");
        var wrapped = new CopyOnWriteFile(foreign, testDirectory, "stepB");

        _ = await wrapped.GetLocalPathAsync();

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
    public async Task PipelineFileGetLocalPathReturnsOwnPath()
    {
        var file = CreateStepFile("stepA", string.Empty, "data", "txt", "x");

        var path = await file.GetLocalPathAsync();

        Assert.IsTrue(path.Contains(Path.Combine(testDirectory, "stepA"), StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task CreateWritableCopyProducesContentIdenticalOwnedCopy()
    {
        var source = CreateStepFile("stepA", string.Empty, "data", "txt", "payload");

        var managerB = new PipelineFileManager(testDirectory, "stepB");
        var copy = await managerB.CreateWritableCopyAsync(source, source.OriginalFileNameWithoutExtension);

        var copyPath = await copy.GetLocalPathAsync();
        Assert.AreNotEqual(await source.GetLocalPathAsync(), copyPath);
        Assert.IsTrue(copyPath.Contains(Path.Combine(testDirectory, "stepB"), StringComparison.Ordinal));
        Assert.AreEqual("payload", File.ReadAllText(copyPath));
        Assert.AreEqual("data.txt", copy.OriginalFileName);
    }

    [TestMethod]
    public async Task CreateWritableCopyLeavesNoFileWhenCancelled()
    {
        var source = CreateStepFile("stepA", string.Empty, "data", "txt", "payload");
        var managerB = new PipelineFileManager(testDirectory, "stepB");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => managerB.CreateWritableCopyAsync(source, "data", cts.Token));

        Assert.IsEmpty(Directory.GetFiles(Path.Combine(testDirectory, "stepB")), "a copy that did not finish must not leave a file behind");
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
