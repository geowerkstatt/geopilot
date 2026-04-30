using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.Unzip;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Compression;
using System.Text;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class UnzipProcessTest
{
    private string testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "UnzipProcess_" + Guid.NewGuid());
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }

    [TestMethod]
    public async Task SunnyDay()
    {
        var zipFile = CreateZipFile("input.zip", new (string Path, string Content)[]
        {
            ("readme.txt", "hello"),
            ("data.xtf", "<xtf/>"),
        });

        var process = new UnzipProcess(new PipelineFileManager(testDirectory, "UnzipProcess"), Mock.Of<ILogger<UnzipProcessTest>>());
        var result = await process.RunAsync(zipFile);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result);

        var extracted = result["extracted_files"] as IPipelineFile[];
        Assert.IsNotNull(extracted);
        Assert.HasCount(2, extracted);
        CollectionAssert.AreEquivalent(
            new[] { "readme.txt", "data.xtf" },
            extracted.Select(f => f.OriginalFileName).ToArray());

        var readme = extracted.Single(f => f.OriginalFileName == "readme.txt");
        using (var reader = new StreamReader(readme.OpenReadFileStream()))
        {
            Assert.AreEqual("hello", reader.ReadToEnd());
        }

        var statusMessage = result["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(statusMessage);
        var expected = new Dictionary<string, string>
        {
            { "de", "2 Datei(en) aus dem ZIP Archiv entpackt." },
            { "fr", "2 fichier(s) extrait(s) de l'archive ZIP." },
            { "it", "2 file estratti dall'archivio ZIP." },
            { "en", "2 file(s) extracted from the ZIP archive." },
        };
        CollectionAssert.AreEqual(expected, statusMessage);
    }

    [TestMethod]
    public async Task EmptyArchiveReturnsEmptyArrayAndStatusMessage()
    {
        var zipFile = CreateZipFile("empty.zip", Array.Empty<(string, string)>());

        var process = new UnzipProcess(new PipelineFileManager(testDirectory, "UnzipProcess"), Mock.Of<ILogger<UnzipProcessTest>>());
        var result = await process.RunAsync(zipFile);

        var extracted = result["extracted_files"] as IPipelineFile[];
        Assert.IsNotNull(extracted);
        Assert.HasCount(0, extracted);

        var statusMessage = result["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(statusMessage);
        var expected = new Dictionary<string, string>
        {
            { "de", "Das ZIP Archiv enthält keine Dateien." },
            { "fr", "L'archive ZIP ne contient aucun fichier." },
            { "it", "L'archivio ZIP non contiene file." },
            { "en", "The ZIP archive contains no files." },
        };
        CollectionAssert.AreEqual(expected, statusMessage);
    }

    [TestMethod]
    public async Task DirectoryEntriesAreSkipped()
    {
        var zipPath = Path.Combine(testDirectory, "with-dirs.zip");
        using (var fileStream = File.Create(zipPath))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            // A pure directory entry — Name is empty, FullName ends with '/'.
            archive.CreateEntry("subfolder/");
            var entry = archive.CreateEntry("subfolder/file.txt");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write("nested");
        }

        var zipFile = new PipelineFile(zipPath, "with-dirs.zip");
        var process = new UnzipProcess(new PipelineFileManager(testDirectory, "UnzipProcess"), Mock.Of<ILogger<UnzipProcessTest>>());
        var result = await process.RunAsync(zipFile);

        var extracted = result["extracted_files"] as IPipelineFile[];
        Assert.IsNotNull(extracted);
        Assert.HasCount(1, extracted);
        Assert.AreEqual("file.txt", extracted[0].OriginalFileName);
    }

    [TestMethod]
    public async Task FileWithoutExtensionIsSupported()
    {
        var zipFile = CreateZipFile("no-ext.zip", new (string Path, string Content)[]
        {
            ("README", "no extension here"),
        });

        var process = new UnzipProcess(new PipelineFileManager(testDirectory, "UnzipProcess"), Mock.Of<ILogger<UnzipProcessTest>>());
        var result = await process.RunAsync(zipFile);

        var extracted = result["extracted_files"] as IPipelineFile[];
        Assert.IsNotNull(extracted);
        Assert.HasCount(1, extracted);
        Assert.AreEqual("README.", extracted[0].OriginalFileName);
    }

    [TestMethod]
    public async Task NestedEntriesExposeOriginalRelativePath()
    {
        var zipFile = CreateZipFile("nested.zip", new (string Path, string Content)[]
        {
            ("root.txt", "at root"),
            ("a/b/c/deep.txt", "deep-content"),
            ("a/sibling.txt", "near root"),
        });

        var process = new UnzipProcess(new PipelineFileManager(testDirectory, "UnzipProcess"), Mock.Of<ILogger<UnzipProcessTest>>());
        var result = await process.RunAsync(zipFile);

        var extracted = result["extracted_files"] as IPipelineFile[];
        Assert.IsNotNull(extracted);
        Assert.HasCount(3, extracted);

        var root = extracted.Single(f => f.OriginalFileName == "root.txt");
        Assert.AreEqual(string.Empty, root.OriginalRelativePath);

        var deep = extracted.Single(f => f.OriginalFileName == "deep.txt");
        Assert.AreEqual("a/b/c", deep.OriginalRelativePath);

        var sibling = extracted.Single(f => f.OriginalFileName == "sibling.txt");
        Assert.AreEqual("a", sibling.OriginalRelativePath);

        // OriginalRelativePath is metadata only — the on-disk files all live flat in the step directory.
        var stepDirectory = Path.Combine(testDirectory, "UnzipProcess");
        Assert.HasCount(3, Directory.GetFiles(stepDirectory));
        Assert.HasCount(0, Directory.GetDirectories(stepDirectory));

        using var reader = new StreamReader(deep.OpenReadFileStream());
        Assert.AreEqual("deep-content", reader.ReadToEnd());
    }

    [TestMethod]
    public async Task RejectsZipSlipTraversal()
    {
        // Build a ZIP entry whose FullName escapes the extraction root via '..'.
        // ZipArchive doesn't allow '..' via CreateEntry's normalization on .NET 10, so
        // we construct the archive manually to keep the malicious path intact.
        var zipPath = Path.Combine(testDirectory, "evil.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            // Disable normalization: write a raw entry name with '..'.
            var entry = archive.CreateEntry("../escape.txt");
            using var s = entry.Open();
            var bytes = Encoding.UTF8.GetBytes("malicious");
            s.Write(bytes, 0, bytes.Length);
        }

        var zipFile = new PipelineFile(zipPath, "evil.zip");
        var process = new UnzipProcess(new PipelineFileManager(testDirectory, "UnzipProcess"), Mock.Of<ILogger<UnzipProcessTest>>());

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(zipFile));
    }

    [TestMethod]
    public async Task RejectsRootedEntry()
    {
        var zipPath = Path.Combine(testDirectory, "rooted.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("/absolute/path.txt");
            using var s = entry.Open();
            var bytes = Encoding.UTF8.GetBytes("rooted");
            s.Write(bytes, 0, bytes.Length);
        }

        var zipFile = new PipelineFile(zipPath, "rooted.zip");
        var process = new UnzipProcess(new PipelineFileManager(testDirectory, "UnzipProcess"), Mock.Of<ILogger<UnzipProcessTest>>());

        await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(zipFile));
    }

    private PipelineFile CreateZipFile(string archiveName, IReadOnlyCollection<(string Path, string Content)> entries)
    {
        var zipPath = Path.Combine(testDirectory, archiveName);
        using (var fileStream = File.Create(zipPath))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            foreach (var (entryPath, content) in entries)
            {
                var entry = archive.CreateEntry(entryPath);
                using var entryStream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        return new PipelineFile(zipPath, archiveName);
    }
}
