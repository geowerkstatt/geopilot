using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.Matcher.FileMatcher;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class FileMatcherProcessTest
{
    private static PipelineFileList FileList(params string[] fileNames) =>
        new PipelineFileList(fileNames.Select(n => (IPipelineFile)new PipelineFile("dummy", n)).ToList());

    private static async Task<IPipelineFile[]> RunAsync(FileMatcherProcess process, IPipelineFileList files)
    {
        var result = await process.RunAsync(files);
        result.TryGetValue("matched_files", out var value);
        return (IPipelineFile[])value!;
    }

    [TestMethod]
    public async Task NoFiltersConfiguredReturnsAllFiles()
    {
        var process = new FileMatcherProcess(null, null);
        var files = FileList("report.pdf", "map.png");

        var result = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyReturnsMatchingFiles()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList("report.pdf", "map.png");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("report.pdf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyNoMatchReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList("map.png", "data.csv");

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task ExtensionFilterCaseInsensitive()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "PDF" }, null);
        var files = FileList("report.pdf");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task MultipleExtensionsOrSemantics()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf", "png" }, null);
        var files = FileList("report.pdf", "map.png", "data.csv");

        var result = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyReturnsMatchingFiles()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadNetwork.pdf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyNoMatchReturnsEmpty()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.pdf");

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task MultipleFileNamePatternsOrSemantics()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*", "Map.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf", "Other.pdf");

        var result = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task BothFiltersAndSemantics()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf", "RoadNetwork.png");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadNetwork.pdf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task BothFiltersNoMatchForEitherReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.png");

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task EmptyUploadListReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList();

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }
}
