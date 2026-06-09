using Geopilot.Api.Pipeline.Process.Matcher.FileRenamer;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class FileRenamerProcessTest
{
    private string testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "FileRenamerProcess_" + Guid.NewGuid());
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
            Directory.Delete(testDirectory, recursive: true);
    }

    [TestMethod]
    public async Task SingleMatchingMappingRenamesFileAndCopiesContent()
    {
        var process = CreateProcess(new FileMapping("^input\\.xtf$", "renamed.xtf"));
        var files = FileList(SourceFile("input.xtf", "geodata-content"));

        var (renamed, unmatchedFiles, unmatchedMappings, statusMessage) = await RunAsync(process, files);

        Assert.HasCount(1, renamed);
        Assert.AreEqual("renamed.xtf", renamed[0].OriginalFileName);
        Assert.AreEqual("geodata-content", ReadContent(renamed[0]));
        Assert.IsEmpty(unmatchedFiles);
        Assert.IsEmpty(unmatchedMappings);
        Assert.AreEqual("All files match the criteria.", statusMessage["en"]);
    }

    [TestMethod]
    public async Task RegexCaptureGroupSubstitutionAppliesToTarget()
    {
        var process = CreateProcess(new FileMapping("^(.+)\\.xtf$", "renamed-$1.xtf"));
        var files = FileList(SourceFile("City_Roads.xtf"));

        var (renamed, unmatchedFiles, unmatchedMappings, _) = await RunAsync(process, files);

        Assert.HasCount(1, renamed);
        Assert.AreEqual("renamed-City_Roads.xtf", renamed[0].OriginalFileName);
        Assert.IsEmpty(unmatchedFiles);
        Assert.IsEmpty(unmatchedMappings);
    }

    [TestMethod]
    public async Task TargetWithDirectorySetsOriginalRelativePath()
    {
        var process = CreateProcess(new FileMapping("^input\\.xtf$", "subdir/renamed.xtf"));
        var files = FileList(SourceFile("input.xtf"));

        var (renamed, _, _, _) = await RunAsync(process, files);

        Assert.HasCount(1, renamed);
        Assert.AreEqual("renamed.xtf", renamed[0].OriginalFileName);
        Assert.AreEqual("subdir", renamed[0].OriginalRelativePath);
    }

    [TestMethod]
    public async Task FileMatchingNoMappingIsUnmatched()
    {
        var mapping = new FileMapping("^input\\.xtf$", "renamed.xtf");
        var process = CreateProcess(mapping);
        var files = FileList(SourceFile("report.pdf"));

        var (renamed, unmatchedFiles, unmatchedMappings, statusMessage) = await RunAsync(process, files);

        Assert.IsEmpty(renamed);
        Assert.HasCount(1, unmatchedFiles);
        Assert.AreEqual("report.pdf", unmatchedFiles[0].OriginalFileName);
        Assert.HasCount(1, unmatchedMappings);
        Assert.AreEqual(mapping, unmatchedMappings[0]);
        Assert.AreEqual("The following file(s) do not match any criteria: report.pdf", statusMessage["en"]);
    }

    [TestMethod]
    public async Task FileMatchingMultipleMappingsIsIgnored()
    {
        // A file matching more than one mapping is ambiguous and is therefore left unmatched.
        var process = CreateProcess(
            new FileMapping("data", "byName.xtf"),
            new FileMapping("\\.xtf$", "byExtension.xtf"));
        var files = FileList(SourceFile("data.xtf"));

        var (renamed, unmatchedFiles, unmatchedMappings, _) = await RunAsync(process, files);

        Assert.IsEmpty(renamed);
        Assert.HasCount(1, unmatchedFiles);
        Assert.AreEqual("data.xtf", unmatchedFiles[0].OriginalFileName);
        Assert.IsEmpty(unmatchedMappings);
    }

    [TestMethod]
    public async Task UnusedMappingIsReportedAsUnmatchedMapping()
    {
        var matching = new FileMapping("^input\\.xtf$", "renamed.xtf");
        var unused = new FileMapping("^never\\.xtf$", "other.xtf");
        var process = CreateProcess(matching, unused);
        var files = FileList(SourceFile("input.xtf"));

        var (renamed, unmatchedFiles, unmatchedMappings, _) = await RunAsync(process, files);

        Assert.HasCount(1, renamed);
        Assert.IsEmpty(unmatchedFiles);
        Assert.HasCount(1, unmatchedMappings);
        Assert.AreEqual(unused, unmatchedMappings[0]);
    }

    [TestMethod]
    public async Task NoMappingsConfiguredLeavesAllFilesUnmatched()
    {
        var process = new FileRenamerProcess(null!, new PipelineFileManager(testDirectory, "FileRenamerProcess"));
        var files = FileList(SourceFile("a.xtf"), SourceFile("b.xtf"));

        var (renamed, unmatchedFiles, unmatchedMappings, statusMessage) = await RunAsync(process, files);

        Assert.IsEmpty(renamed);
        Assert.HasCount(2, unmatchedFiles);
        Assert.IsEmpty(unmatchedMappings);
        Assert.AreEqual("The following file(s) do not match any criteria: a.xtf, b.xtf", statusMessage["en"]);
    }

    [TestMethod]
    public async Task EmptyUploadListReturnsEmpty()
    {
        var mapping = new FileMapping("^input\\.xtf$", "renamed.xtf");
        var process = CreateProcess(mapping);
        var files = FileList();

        var (renamed, unmatchedFiles, unmatchedMappings, statusMessage) = await RunAsync(process, files);

        Assert.IsEmpty(renamed);
        Assert.IsEmpty(unmatchedFiles);
        Assert.HasCount(1, unmatchedMappings);
        Assert.AreEqual(mapping, unmatchedMappings[0]);
    }

    [TestMethod]
    public void NullFileManagerThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new FileRenamerProcess(new List<FileMapping>(), null!));
    }

    private FileRenamerProcess CreateProcess(params FileMapping[] mappings) =>
        new FileRenamerProcess(mappings.ToList(), new PipelineFileManager(testDirectory, "FileRenamerProcess"));

    private static PipelineFileList FileList(params IPipelineFile[] files) =>
        new PipelineFileList(files.ToList());

    private PipelineFile SourceFile(string originalFileName, string content = "content")
    {
        var sourceDirectory = Path.Combine(testDirectory, "source");
        Directory.CreateDirectory(sourceDirectory);
        var filePath = Path.Combine(sourceDirectory, $"{Guid.NewGuid():N}_{originalFileName}");
        File.WriteAllText(filePath, content);
        return new PipelineFile(filePath, originalFileName);
    }

    private static string ReadContent(IPipelineFile file)
    {
        using var reader = new StreamReader(file.OpenReadFileStream());
        return reader.ReadToEnd();
    }

    private static async Task<(List<IPipelineFile> RenamedFiles, List<IPipelineFile> UnmatchedFiles, List<FileMapping> UnmatchedMappings, Dictionary<string, string> StatusMessage)> RunAsync(FileRenamerProcess process, IPipelineFileList files)
    {
        var result = await process.RunAsync(files, CancellationToken.None);
        var renamedFiles = (List<IPipelineFile>)result["renamed_files"]!;
        var unmatchedFiles = (List<IPipelineFile>)result["unmatched_files"]!;
        var unmatchedMappings = (List<FileMapping>)result["unmatched_mappings"]!;
        var statusMessage = (Dictionary<string, string>)result["status_message"]!;
        return (renamedFiles, unmatchedFiles, unmatchedMappings, statusMessage);
    }
}
