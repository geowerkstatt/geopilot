using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.Matcher.XtfMatcher;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class XtfMatcherProcessTest
{
    private const string RoadsExdm2ienXtf = "TestData/UploadFiles/RoadsExdm2ien.xtf";
    private const string RoadsExdm2ienModel = "RoadsExdm2ien";

    private static PipelineFileList FileList(params string[] fileNames) =>
        new PipelineFileList(fileNames.Select(n => (IPipelineFile)new PipelineFile("dummy", n)).ToList());

    private static PipelineFileList FileListWithPath(params (string Path, string Name)[] files) =>
        new PipelineFileList(files.Select(f => (IPipelineFile)new PipelineFile(f.Path, f.Name)).ToList());

    private static async Task<IPipelineFile[]> RunAsync(XtfMatcherProcess process, IPipelineFileList files)
    {
        var result = await process.RunAsync(files);
        result.TryGetValue("xtf_files", out var value);
        return (IPipelineFile[])value!;
    }

    [TestMethod]
    public async Task NoFiltersConfiguredReturnsAllFiles()
    {
        var process = new XtfMatcherProcess(null, null, null);
        var files = FileList("road.xtf", "map.itf");

        var result = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyReturnsMatchingFiles()
    {
        var process = new XtfMatcherProcess("xtf", null, null);
        var files = FileList("road.xtf", "map.itf");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("road.xtf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task ExtensionFilterOnlyNoMatchReturnsEmpty()
    {
        var process = new XtfMatcherProcess("xtf", null, null);
        var files = FileList("map.itf", "data.gpkg");

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task ExtensionFilterCaseInsensitive()
    {
        var process = new XtfMatcherProcess("XTF", null, null);
        var files = FileList("road.xtf");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task MultipleExtensionsOrSemantics()
    {
        var process = new XtfMatcherProcess("xtf,itf", null, null);
        var files = FileList("road.xtf", "map.itf", "data.gpkg");

        var result = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyReturnsMatchingFiles()
    {
        var process = new XtfMatcherProcess(null, null, "Road.*");
        var files = FileList("RoadNetwork.xtf", "MapData.xtf");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadNetwork.xtf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task FileNamePatternOnlyNoMatchReturnsEmpty()
    {
        var process = new XtfMatcherProcess(null, null, "Road.*");
        var files = FileList("MapData.xtf");

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task MultipleFileNamePatternsOrSemantics()
    {
        var process = new XtfMatcherProcess(null, null, "Road.*,Map.*");
        var files = FileList("RoadNetwork.xtf", "MapData.xtf", "Other.xtf");

        var result = await RunAsync(process, files);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task BothFiltersAndSemantics()
    {
        var process = new XtfMatcherProcess("xtf", null, "Road.*");
        var files = FileList("RoadNetwork.xtf", "MapData.xtf", "RoadNetwork.itf");

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadNetwork.xtf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task BothFiltersNoMatchForEitherReturnsEmpty()
    {
        var process = new XtfMatcherProcess("xtf", null, "Road.*");
        var files = FileList("MapData.itf");

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task EmptyUploadListReturnsEmpty()
    {
        var process = new XtfMatcherProcess("xtf", null, null);
        var files = FileList();

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task IliModelFilterOnlyReturnsMatchingFiles()
    {
        var process = new XtfMatcherProcess(null, RoadsExdm2ienModel, null);
        var files = FileListWithPath((RoadsExdm2ienXtf, "RoadsExdm2ien.xtf"));

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadsExdm2ien.xtf", result[0].OriginalFileName);
    }

    [TestMethod]
    public async Task IliModelFilterOnlyNoMatchReturnsEmpty()
    {
        var process = new XtfMatcherProcess(null, "SomeOtherModel", null);
        var files = FileListWithPath((RoadsExdm2ienXtf, "RoadsExdm2ien.xtf"));

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task IliModelFilterNonParsableFileReturnsEmpty()
    {
        var process = new XtfMatcherProcess(null, RoadsExdm2ienModel, null);
        var files = FileList("notAnXtfFile.xtf");

        var result = await RunAsync(process, files);

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task AllThreeFiltersAndSemantics()
    {
        var process = new XtfMatcherProcess("xtf", RoadsExdm2ienModel, "Roads.*");
        var files = FileListWithPath(
            (RoadsExdm2ienXtf, "RoadsExdm2ien.xtf"),
            (RoadsExdm2ienXtf, "RoadsExdm2ien.itf"),
            (RoadsExdm2ienXtf, "OtherName.xtf"));

        var result = await RunAsync(process, files);

        Assert.HasCount(1, result);
        Assert.AreEqual("RoadsExdm2ien.xtf", result[0].OriginalFileName);
    }
}
