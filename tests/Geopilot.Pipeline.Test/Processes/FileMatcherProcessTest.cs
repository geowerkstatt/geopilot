using Geopilot.Pipeline.Processes.Matcher.FileMatcher;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class FileMatcherProcessTest
{
    private static IPipelineFile[] FileList(params string[] fileNames) =>
        fileNames.Select(n => (IPipelineFile)new PipelineFile("dummy", n)).ToArray();

    [TestMethod]
    public async Task NoFiltersConfiguredReturnsAllFiles()
    {
        var process = new FileMatcherProcess(null, null);
        var files = FileList("report.pdf", "map.png");

        var result = await process.RunAsync(files);

        Assert.HasCount(2, result.MatchedFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
        Assert.AreEqual("2 of 2 file(s) match the filter criteria.", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyReturnsMatchingFiles()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList("report.pdf", "map.png");

        var result = await process.RunAsync(files);

        Assert.HasCount(1, result.MatchedFiles);
        Assert.AreEqual("report.pdf", result.MatchedFiles[0].OriginalFileName);
        Assert.HasCount(1, result.UnmatchedFiles);
        Assert.AreEqual("map.png", result.UnmatchedFiles[0].OriginalFileName);
        Assert.AreEqual("1 von 2 Datei(en) entsprechen den Filterkriterien.", result.StatusMessage["de"]);
        Assert.AreEqual("1 fichier(s) sur 2 correspondent aux critères du filtre.", result.StatusMessage["fr"]);
        Assert.AreEqual("1 file su 2 corrispondono ai criteri del filtro.", result.StatusMessage["it"]);
        Assert.AreEqual("1 of 2 file(s) match the filter criteria.", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyNoMatchReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList("map.png", "data.csv");

        var result = await process.RunAsync(files);

        Assert.HasCount(0, result.MatchedFiles);
        Assert.HasCount(2, result.UnmatchedFiles);
        Assert.AreEqual("map.png", result.UnmatchedFiles[0].OriginalFileName);
        Assert.AreEqual("data.csv", result.UnmatchedFiles[1].OriginalFileName);
        Assert.AreEqual("No files match the filter criteria.", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterCaseInsensitive()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "PDF" }, null);
        var files = FileList("report.pdf");

        var result = await process.RunAsync(files);

        Assert.HasCount(1, result.MatchedFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task MultipleExtensionsOrSemantics()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf", "png" }, null);
        var files = FileList("report.pdf", "map.png", "data.csv");

        var result = await process.RunAsync(files);

        Assert.HasCount(2, result.MatchedFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyReturnsMatchingFiles()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf");

        var result = await process.RunAsync(files);

        Assert.HasCount(1, result.MatchedFiles);
        Assert.AreEqual("RoadNetwork.pdf", result.MatchedFiles[0].OriginalFileName);
        Assert.HasCount(1, result.UnmatchedFiles);
        Assert.AreEqual("MapData.pdf", result.UnmatchedFiles[0].OriginalFileName);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyNoMatchReturnsEmpty()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.pdf");

        var result = await process.RunAsync(files);

        Assert.HasCount(0, result.MatchedFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
        Assert.AreEqual("MapData.pdf", result.UnmatchedFiles[0].OriginalFileName);
    }

    [TestMethod]
    public async Task MultipleFileNamePatternsOrSemantics()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*", "Map.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf", "Other.pdf");

        var result = await process.RunAsync(files);

        Assert.HasCount(2, result.MatchedFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task BothFiltersAndSemantics()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf", "RoadNetwork.png");

        var result = await process.RunAsync(files);

        Assert.HasCount(1, result.MatchedFiles);
        Assert.AreEqual("RoadNetwork.pdf", result.MatchedFiles[0].OriginalFileName);
        Assert.HasCount(2, result.UnmatchedFiles);
        Assert.AreEqual("MapData.pdf", result.UnmatchedFiles[0].OriginalFileName);
        Assert.AreEqual("RoadNetwork.png", result.UnmatchedFiles[1].OriginalFileName);
    }

    [TestMethod]
    public async Task BothFiltersNoMatchForEitherReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.png");

        var result = await process.RunAsync(files);

        Assert.HasCount(0, result.MatchedFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
        Assert.AreEqual("MapData.png", result.UnmatchedFiles[0].OriginalFileName);
    }

    [TestMethod]
    public async Task EmptyUploadListReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList();

        var result = await process.RunAsync(files);

        Assert.HasCount(0, result.MatchedFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
        Assert.AreEqual("No files match the filter criteria.", result.StatusMessage["en"]);
    }
}
