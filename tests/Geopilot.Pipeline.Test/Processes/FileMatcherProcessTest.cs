using Geopilot.Pipeline.Processes.Matcher.FileMatcher;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class FileMatcherProcessTest
{
    private static IPipelineFile[] FileList(params string[] fileNames) =>
        fileNames.Select(n => (IPipelineFile)new PipelineFile("dummy", n)).ToArray();

    private static async Task<(IPipelineFile[] Files, LocalizedText StatusMessage)> RunAsync(FileMatcherProcess process, IPipelineFile[] files)
    {
        var result = await process.RunAsync(files);
        var matchedFiles = result.MatchedFiles;
        var statusMessage = result.StatusMessage;
        return ((IPipelineFile[])matchedFiles!, (LocalizedText)statusMessage!);
    }

    [TestMethod]
    public async Task NoFiltersConfiguredReturnsAllFiles()
    {
        var process = new FileMatcherProcess(null, null);
        var files = FileList("report.pdf", "map.png");

        var (result, statusMessage) = await RunAsync(process, files);

        Assert.HasCount(2, result);
        Assert.AreEqual("2 of 2 file(s) match the filter criteria.", statusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyReturnsMatchingFiles()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList("report.pdf", "map.png");

        var (result, statusMessage) = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("report.pdf", result[0].OriginalFileName);
        Assert.AreEqual("1 von 2 Datei(en) entsprechen den Filterkriterien.", statusMessage["de"]);
        Assert.AreEqual("1 fichier(s) sur 2 correspondent aux critères du filtre.", statusMessage["fr"]);
        Assert.AreEqual("1 file su 2 corrispondono ai criteri del filtro.", statusMessage["it"]);
        Assert.AreEqual("1 of 2 file(s) match the filter criteria.", statusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyNoMatchReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList("map.png", "data.csv");

        var (result, statusMessage) = await RunAsync(process, files);

        Assert.HasCount(0, result);
        Assert.AreEqual("No files match the filter criteria.", statusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterCaseInsensitive()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "PDF" }, null);
        var files = FileList("report.pdf");

        var (result, _) = await RunAsync(process, files);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task MultipleExtensionsOrSemantics()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf", "png" }, null);
        var files = FileList("report.pdf", "map.png", "data.csv");

        var (result, _) = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyReturnsMatchingFiles()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf");

        var (result, _) = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadNetwork.pdf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyNoMatchReturnsEmpty()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.pdf");

        var (result, _) = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task MultipleFileNamePatternsOrSemantics()
    {
        var process = new FileMatcherProcess(null, new HashSet<string>() { "Road.*", "Map.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf", "Other.pdf");

        var (result, _) = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task BothFiltersAndSemantics()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.pdf", "MapData.pdf", "RoadNetwork.png");

        var (result, _) = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadNetwork.pdf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task BothFiltersNoMatchForEitherReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.png");

        var (result, _) = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task EmptyUploadListReturnsEmpty()
    {
        var process = new FileMatcherProcess(new HashSet<string>() { "pdf" }, null);
        var files = FileList();

        var (result, statusMessage) = await RunAsync(process, files);

        Assert.HasCount(0, result);
        Assert.AreEqual("No files match the filter criteria.", statusMessage["en"]);
    }
}
