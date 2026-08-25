using Geopilot.Pipeline.Processes.Matcher.XtfMatcher;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class XtfMatcherProcessTest
{
    private const string RoadsExdm2ienXtf = "TestData/UploadFiles/RoadsExdm2ien.xtf";
    private const string RoadsExdm2ienAltPrefixXtf = "TestData/UploadFiles/RoadsExdm2ien_altPrefix.xtf";
    private const string RoadsExdm2ienDefaultNsXtf = "TestData/UploadFiles/RoadsExdm2ien_defaultNs.xtf";
    private const string IseltwaldGwpBe13Xtf = "TestData/UploadFiles/iseltwald_gwp_be13_1.xtf";
    private const string RoadsExdm2ienModel = "roadsexdm2ien";
    private const string IseltwaldGwpBe13Model = "gwp_bern_13_1";

    private static IPipelineFile[] FileList(params string[] fileNames) =>
        fileNames.Select(n => (IPipelineFile)new PipelineFile("dummy", n)).ToArray();

    private static IPipelineFile[] FileListWithPath(params (string Path, string Name)[] files) =>
        files.Select(f => (IPipelineFile)new PipelineFile(f.Path, f.Name)).ToArray();

    [TestMethod]
    public async Task NoFiltersConfiguredReturnsAllFiles()
    {
        var process = new XtfMatcherProcess(null, null, null);
        var files = FileList("road.xtf", "map.itf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(2, result.XtfFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
        Assert.AreEqual("2 of 2 file(s) match the XTF filter criteria.", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyReturnsMatchingFiles()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "xtf" }, null, null);
        var files = FileList("road.xtf", "map.itf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.AreEqual("road.xtf", result.XtfFiles[0].OriginalFileName);
        Assert.HasCount(1, result.UnmatchedFiles);
        Assert.AreEqual("map.itf", result.UnmatchedFiles[0].OriginalFileName);
        Assert.AreEqual("1 von 2 Datei(en) entsprechen den XTF-Filterkriterien.", result.StatusMessage["de"]);
        Assert.AreEqual("1 fichier(s) sur 2 correspondent aux critères du filtre XTF.", result.StatusMessage["fr"]);
        Assert.AreEqual("1 file su 2 corrispondono ai criteri del filtro XTF.", result.StatusMessage["it"]);
        Assert.AreEqual("1 of 2 file(s) match the XTF filter criteria.", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyNoMatchReturnsEmpty()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "xtf" }, null, null);
        var files = FileList("map.itf", "data.gpkg");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(0, result.XtfFiles);
        Assert.HasCount(2, result.UnmatchedFiles);
        Assert.AreEqual("map.itf", result.UnmatchedFiles[0].OriginalFileName);
        Assert.AreEqual("data.gpkg", result.UnmatchedFiles[1].OriginalFileName);
        Assert.AreEqual("No files match the XTF filter criteria.", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task ExtensionFilterCaseInsensitive()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "XTF" }, null, null);
        var files = FileList("road.xtf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task MultipleExtensionsOrSemantics()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "xtf", "itf" }, null, null);
        var files = FileList("road.xtf", "map.itf", "data.gpkg");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(2, result.XtfFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyReturnsMatchingFiles()
    {
        var process = new XtfMatcherProcess(null, null, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.xtf", "MapData.xtf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.AreEqual("RoadNetwork.xtf", result.XtfFiles[0].OriginalFileName);
        Assert.HasCount(1, result.UnmatchedFiles);
        Assert.AreEqual("MapData.xtf", result.UnmatchedFiles[0].OriginalFileName);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyNoMatchReturnsEmpty()
    {
        var process = new XtfMatcherProcess(null, null, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.xtf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(0, result.XtfFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task MultipleFileNamePatternsOrSemantics()
    {
        var process = new XtfMatcherProcess(null, null, new HashSet<string>() { "Road.*", "Map.*" });
        var files = FileList("RoadNetwork.xtf", "MapData.xtf", "Other.xtf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(2, result.XtfFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task BothFiltersAndSemantics()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "xtf" }, null, new HashSet<string>() { "Road.*" });
        var files = FileList("RoadNetwork.xtf", "MapData.xtf", "RoadNetwork.itf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.AreEqual("RoadNetwork.xtf", result.XtfFiles[0].OriginalFileName);
        Assert.HasCount(2, result.UnmatchedFiles);
        Assert.AreEqual("MapData.xtf", result.UnmatchedFiles[0].OriginalFileName);
        Assert.AreEqual("RoadNetwork.itf", result.UnmatchedFiles[1].OriginalFileName);
    }

    [TestMethod]
    public async Task BothFiltersNoMatchForEitherReturnsEmpty()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "xtf" }, null, new HashSet<string>() { "Road.*" });
        var files = FileList("MapData.itf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(0, result.XtfFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
        Assert.AreEqual("MapData.itf", result.UnmatchedFiles[0].OriginalFileName);
    }

    [TestMethod]
    public async Task EmptyUploadListReturnsEmpty()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "xtf" }, null, null);
        var files = FileList();

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(0, result.XtfFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
        Assert.AreEqual("No files match the XTF filter criteria.", result.StatusMessage["en"]);
    }

    [TestMethod]
    [DataRow(RoadsExdm2ienModel)]
    [DataRow(IseltwaldGwpBe13Model)]
    public async Task IliModelFilterOnlyReturnsMatchingFiles(string models)
    {
        var process = new XtfMatcherProcess(null, models.Split(",").ToHashSet(StringComparer.OrdinalIgnoreCase), null);
        var files = FileListWithPath(
            (RoadsExdm2ienXtf, "RoadsExdm2ien.xtf"),
            (IseltwaldGwpBe13Xtf, "iseltwald_gwp_be13_1.xtf"));
        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task IliModelFilterInterlis24MatchesFileWithAlternativePrefix()
    {
        // INTERLIS 2.4 spec fixes only the namespace URI, the prefix is arbitrary.
        var process = new XtfMatcherProcess(null, new HashSet<string>() { RoadsExdm2ienModel }, null);
        var files = FileListWithPath((RoadsExdm2ienAltPrefixXtf, "RoadsExdm2ien_altPrefix.xtf"));

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task IliModelFilterInterlis24MatchesFileWithDefaultNamespace()
    {
        // INTERLIS 2.4 file using the INTERLIS namespace as default (no prefix).
        var process = new XtfMatcherProcess(null, new HashSet<string>() { RoadsExdm2ienModel }, null);
        var files = FileListWithPath((RoadsExdm2ienDefaultNsXtf, "RoadsExdm2ien_defaultNs.xtf"));

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.HasCount(0, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task IliModelFilterOnlyNoMatchReturnsEmpty()
    {
        var process = new XtfMatcherProcess(null, new HashSet<string>() { "SomeOtherModel" }, null);
        var files = FileListWithPath((RoadsExdm2ienXtf, "RoadsExdm2ien.xtf"));

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(0, result.XtfFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task IliModelFilterNonParsableFileReturnsEmpty()
    {
        var process = new XtfMatcherProcess(null, new HashSet<string>() { RoadsExdm2ienModel }, null);
        var files = FileList("notAnXtfFile.xtf");

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(0, result.XtfFiles);
        Assert.HasCount(1, result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task AllThreeFiltersAndSemantics()
    {
        var process = new XtfMatcherProcess(new HashSet<string>() { "xtf" }, new HashSet<string>() { RoadsExdm2ienModel }, new HashSet<string>() { "Roads.*" });
        var files = FileListWithPath(
            (RoadsExdm2ienXtf, "RoadsExdm2ien.xtf"),
            (RoadsExdm2ienXtf, "RoadsExdm2ien.itf"),
            (RoadsExdm2ienXtf, "OtherName.xtf"));

        var result = await process.RunAsync(files, CancellationToken.None);

        Assert.HasCount(1, result.XtfFiles);
        Assert.AreEqual("RoadsExdm2ien.xtf", result.XtfFiles[0].OriginalFileName);
        Assert.HasCount(2, result.UnmatchedFiles);
    }
}
