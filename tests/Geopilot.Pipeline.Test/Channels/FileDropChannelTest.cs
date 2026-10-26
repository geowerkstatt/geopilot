using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Channels.FileDrop;
using System.Text;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Test.Channels;

[TestClass]
public class FileDropChannelTest
{
    private static readonly DeliveryInfo Delivery = new()
    {
        Id = 42,
        JobId = Guid.NewGuid(),
        DeclaredAt = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero),
        Mandate = new MandateInfo { Id = 7, Name = new Dictionary<string, string> { ["de"] = "Mandat A" } },
        Declarer = new DeclarerInfo { Kind = DeclarerKind.User, Name = "Lieferant", Email = "lieferant@example.com" },
    };

    private static readonly Regex TemporaryFolderName = new(@"^\.42\.[0-9a-f]{32}\.partial$");

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
    public async Task DeliverCountsATargetWithTheFilesOfTheDeliveryAsDelivered()
    {
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "data.xtf"), "<xtf/>");
        await File.WriteAllTextAsync(Path.Combine(target, "log.txt"), "ok");

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "<xtf/>"), Content("log.txt", "ok")], CancellationToken.None);

        Assert.IsTrue(result.Delivered, result.Message);
        Assert.AreEqual("<xtf/>", await File.ReadAllTextAsync(Path.Combine(target, "data.xtf")));
    }

    [TestMethod]
    public async Task DeliverFailsWhenTheTargetHoldsOtherFiles()
    {
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "other.xtf"), "<xtf/>");

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "<xtf/>")], CancellationToken.None);

        Assert.IsFalse(result.Delivered);
        Assert.Contains("does not hold the files of this delivery", result.Message);
        CollectionAssert.AreEqual(new[] { Path.Combine(target, "other.xtf") }, Directory.GetFileSystemEntries(target));
    }

    [TestMethod]
    public async Task DeliverFailsWhenAFileOfTheTargetDiffersInSize()
    {
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "data.xtf"), "earlier");

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "later")], CancellationToken.None);

        Assert.IsFalse(result.Delivered);
        Assert.AreEqual("earlier", await File.ReadAllTextAsync(Path.Combine(target, "data.xtf")));
    }

    [TestMethod]
    public async Task DeliverFailsWhenTheTargetHoldsAFolder()
    {
        Directory.CreateDirectory(Path.Combine(target, "17"));
        await File.WriteAllTextAsync(Path.Combine(target, "data.xtf"), "<xtf/>");

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "<xtf/>")], CancellationToken.None);

        Assert.IsFalse(result.Delivered);
    }

    [TestMethod]
    public async Task DeliverReplacesWhatAStoppedAttemptLeftBehind()
    {
        var partial = Path.Combine(mandateDirectory, $".42.{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(partial);
        await File.WriteAllTextAsync(Path.Combine(partial, "data.xtf"), "half");

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "whole")], CancellationToken.None);

        Assert.IsTrue(result.Delivered, result.Message);
        Assert.AreEqual("whole", await File.ReadAllTextAsync(Path.Combine(target, "data.xtf")));
        CollectionAssert.AreEqual(new[] { target }, Directory.GetFileSystemEntries(mandateDirectory));
    }

    [TestMethod]
    public async Task DeliverLeavesTheTemporaryFoldersOfOtherDeliveries()
    {
        var other = Path.Combine(mandateDirectory, $".43.{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(other);

        var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "x")], CancellationToken.None);

        Assert.IsTrue(result.Delivered, result.Message);
        Assert.IsTrue(Directory.Exists(other));
    }

    [TestMethod]
    public async Task DeliverWritesEachAttemptToATemporaryFolderOfItsOwn()
    {
        var first = new RecordingFile("data.xtf", mandateDirectory);
        var second = new RecordingFile("data.xtf", mandateDirectory);

        await Channel().DeliverAsync(Delivery, [first, new FailingFile("log.txt")], CancellationToken.None);
        await Channel().DeliverAsync(Delivery, [second], CancellationToken.None);

        Assert.MatchesRegex(TemporaryFolderName, first.TemporaryFolder);
        Assert.MatchesRegex(TemporaryFolderName, second.TemporaryFolder);
        Assert.AreNotEqual(first.TemporaryFolder, second.TemporaryFolder);
    }

    [TestMethod]
    public async Task DeliverRefusesALinkBelowTheBaseDirectory()
    {
        var elsewhere = Path.Combine(baseDirectory, "..", "FileDropChannel_elsewhere_" + Guid.NewGuid());
        Directory.CreateDirectory(elsewhere);
        try
        {
            CreateLinkOrSkip(mandateDirectory, elsewhere);

            var result = await Channel().DeliverAsync(Delivery, [Content("data.xtf", "x")], CancellationToken.None);

            Assert.IsFalse(result.Delivered);
            Assert.Contains("is a link", result.Message);
            Assert.IsEmpty(Directory.GetFileSystemEntries(elsewhere));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeliverAcceptsABaseDirectoryThatIsALink()
    {
        var linkedBase = Path.Combine(Path.GetTempPath(), "FileDropChannel_link_" + Guid.NewGuid());
        CreateLinkOrSkip(linkedBase, baseDirectory);
        try
        {
            var result = await new FileDropChannel(linkedBase, "mandate-a").DeliverAsync(Delivery, [Content("data.xtf", "x")], CancellationToken.None);

            Assert.IsTrue(result.Delivered, result.Message);
            Assert.IsTrue(File.Exists(Path.Combine(target, "data.xtf")));
        }
        finally
        {
            Directory.Delete(linkedBase);
        }
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
    [DataRow("kanton-be/bfs-351")]
    public void ConstructorAcceptsASubfolderBelowTheBaseDirectory(string subfolder)
    {
        _ = new FileDropChannel(baseDirectory, subfolder);
    }

    [TestMethod]
    [DataRow("351")]
    [DataRow("kanton-be/351")]
    [DataRow("007\\mandate-a")]
    public void ConstructorRejectsASubfolderWithASegmentOfDigitsOnly(string subfolder)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new FileDropChannel(baseDirectory, subfolder));

        Assert.Contains("digits only", exception.Message);
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

    private static void CreateLinkOrSkip(string path, string pathToTarget)
    {
        try
        {
            Directory.CreateSymbolicLink(path, pathToTarget);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Windows creates symbolic links only in Developer Mode or with elevation.
            Assert.Inconclusive($"Cannot create a symbolic link here: {ex.Message}");
        }
    }

    private FileDropChannel Channel() => new(baseDirectory, "mandate-a");

    private sealed class InMemoryFile(string name, string content) : IDeliveryFile
    {
        public string Name => name;

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    private sealed class RecordingFile(string name, string mandateDirectory) : IDeliveryFile
    {
        public string Name => name;

        public string TemporaryFolder { get; private set; } = string.Empty;

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
        {
            TemporaryFolder = Path.GetFileName(Directory.GetDirectories(mandateDirectory).Single());
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("x")));
        }
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
