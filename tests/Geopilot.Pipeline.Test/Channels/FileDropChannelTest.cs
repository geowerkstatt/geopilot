using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Channels.FileDrop;
using System.Text;

namespace Geopilot.Pipeline.Test.Channels;

[TestClass]
public class FileDropChannelTest
{
    private static readonly DeliveryInfo Delivery = new(42, Guid.NewGuid(), 7, new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc));

    private string baseDirectory;
    private string mandateDirectory;
    private string target;

    [TestInitialize]
    public void Initialize()
    {
        baseDirectory = Path.Combine(Path.GetTempPath(), "FileDropChannel_" + Guid.NewGuid());
        Directory.CreateDirectory(baseDirectory);
        mandateDirectory = Path.Combine(baseDirectory, "mandate-a");
        target = Path.Combine(mandateDirectory, "42");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(baseDirectory))
            Directory.Delete(baseDirectory, recursive: true);
    }

    [TestMethod]
    public async Task DeliverPlacesTheFilesInAFolderOfTheirOwn()
    {
        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "<xtf/>"), Content("log.txt", "ok")], CancellationToken.None);

        Assert.IsTrue(result.Delivered, result.Message);
        Assert.AreEqual("<xtf/>", await File.ReadAllTextAsync(Path.Combine(target, "data.xtf")));
        Assert.AreEqual("ok", await File.ReadAllTextAsync(Path.Combine(target, "log.txt")));
        CollectionAssert.AreEqual(new[] { target }, Directory.GetFileSystemEntries(mandateDirectory));
    }

    [TestMethod]
    public async Task DeliverCountsAnExistingTargetAsDelivered()
    {
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "data.xtf"), "earlier");

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "later")], CancellationToken.None);

        Assert.IsTrue(result.Delivered, result.Message);
        Assert.AreEqual("earlier", await File.ReadAllTextAsync(Path.Combine(target, "data.xtf")));
    }

    [TestMethod]
    public async Task DeliverReplacesWhatAStoppedAttemptLeftBehind()
    {
        var partial = Path.Combine(mandateDirectory, ".42.partial");
        Directory.CreateDirectory(partial);
        await File.WriteAllTextAsync(Path.Combine(partial, "data.xtf"), "half");

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "whole")], CancellationToken.None);

        Assert.IsTrue(result.Delivered, result.Message);
        Assert.AreEqual("whole", await File.ReadAllTextAsync(Path.Combine(target, "data.xtf")));
        CollectionAssert.AreEqual(new[] { target }, Directory.GetFileSystemEntries(mandateDirectory));
    }

    [TestMethod]
    public async Task DeliverFailsWithoutCreatingAMissingBaseDirectory()
    {
        var missing = Path.Combine(baseDirectory, "missing");

        var result = await new FileDropChannel(missing, "mandate-a").DeliverAsync(Delivery, [Content("data.xtf", "x")], CancellationToken.None);

        Assert.IsFalse(result.Delivered);
        Assert.IsFalse(Directory.Exists(missing));
    }

    [TestMethod]
    public async Task DeliverRemovesTheTemporaryFolderWhenAFileFails()
    {
        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "x"), new FailingFile("log.txt")], CancellationToken.None);

        Assert.IsFalse(result.Delivered);
        Assert.Contains("log.txt is gone", result.Message);
        Assert.IsEmpty(Directory.GetFileSystemEntries(mandateDirectory));
    }

    [TestMethod]
    public async Task DeliverRemovesTheTemporaryFolderWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Channel().DeliverAsync(Delivery, [Content("data.xtf", "x"), new CancellingFile("log.txt", cancellation)], cancellation.Token));

        Assert.IsEmpty(Directory.GetFileSystemEntries(mandateDirectory));
    }

    [TestMethod]
    [DataRow("../escape.xtf")]
    [DataRow("sub/data.xtf")]
    [DataRow("..")]
    public async Task DeliverRefusesAFileNameThatIsNoPlainName(string name)
    {
        var result = await Channel().DeliverAsync(Delivery, [Content(name, "x")], CancellationToken.None);

        Assert.IsFalse(result.Delivered);
        Assert.IsFalse(File.Exists(Path.Combine(mandateDirectory, "escape.xtf")));
        Assert.IsEmpty(Directory.GetFileSystemEntries(mandateDirectory));
    }

    [TestMethod]
    [DataRow("mandate-a")]
    [DataRow("canton/mandate-a")]
    public void ConstructorAcceptsASubfolderBelowTheBaseDirectory(string subfolder)
    {
        _ = new FileDropChannel(baseDirectory, subfolder);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("../other")]
    [DataRow("a/../b")]
    public void ConstructorRejectsASubfolderThatIsNotBelowTheBaseDirectory(string subfolder)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FileDropChannel(baseDirectory, subfolder));
    }

    [TestMethod]
    public void ConstructorRejectsARootedSubfolder()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FileDropChannel(baseDirectory, Path.GetTempPath()));
    }

    private static InMemoryFile Content(string name, string content) => new(name, content);

    private FileDropChannel Channel() => new(baseDirectory, "mandate-a");

    private sealed class InMemoryFile(string name, string content) : IDeliveryFile
    {
        public string Name => name;

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    private sealed class FailingFile(string name) : IDeliveryFile
    {
        public string Name => name;

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
            => throw new IOException($"{name} is gone.");
    }

    private sealed class CancellingFile(string name, CancellationTokenSource cancellation) : IDeliveryFile
    {
        public string Name => name;

        public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
        {
            await cancellation.CancelAsync();
            return new MemoryStream(Encoding.UTF8.GetBytes("never copied"));
        }
    }
}
