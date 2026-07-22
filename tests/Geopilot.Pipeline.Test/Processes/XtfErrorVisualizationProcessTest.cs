using Geopilot.Pipeline;
using Geopilot.Pipeline.Processes.XtfErrorVisualization;
using Geopilot.Pipeline.Visualization;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class XtfErrorVisualizationProcessTest
{
    private const string XtfLogPath = "TestData/DownloadFiles/ilicop/errorLogWithErrors.xtf";
    private const string SwisstopoBaseMapWmtsCapabilitiesUrl = "https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml";

    [TestMethod]
    public async Task DefaultProducesMapAndTree()
    {
        var process = new XtfErrorVisualizationProcess();
        var xtfLog = new PipelineFile(XtfLogPath, "errorLogWithErrors.xtf");

        var processResult = await process.RunAsync(xtfLog).ConfigureAwait(false);
        Assert.IsNotNull(processResult);
        Assert.IsNotNull(processResult.Visualization);
        Assert.IsNotNull(processResult.StatusMessage);
        var visualization = processResult.Visualization;
        Assert.IsNotNull(visualization);

        // The envelope carries the discriminator; the frontend selects the composite component from it.
        Assert.AreEqual("xtfError", visualization.Type);

        var config = visualization.Data;
        Assert.IsNotNull(config.Map, "default config includes the map");
        Assert.IsNotNull(config.Tree, "default config includes the tree");

        // Map: a base map (WMTS) layer followed by a feature layer with one point per geo-located error.
        Assert.HasCount(2, config.Map.Layers);
        var baseMapLayer = config.Map.Layers[0];
        Assert.AreEqual(SwisstopoBaseMapWmtsCapabilitiesUrl, baseMapLayer.Wmts);
        Assert.AreEqual("swisstopo", baseMapLayer.Attribution);
        Assert.AreEqual(
            "https://www.swisstopo.admin.ch/de/nutzungsbedingungen-kostenlose-geodaten-und-geodienste",
            baseMapLayer.AttributionUrl);
        Assert.IsNull(baseMapLayer.Features);
        Assert.IsNotNull(baseMapLayer.LayerIds);
        Assert.HasCount(2, baseMapLayer.LayerIds);
        var featureLayer = config.Map.Layers[1];
        Assert.IsNull(featureLayer.Wmts);
        var features = featureLayer.Features;
        Assert.IsNotNull(features);

        // The fixture has 19 errors carrying a coordinate; each becomes a point feature.
        Assert.HasCount(19, features);
        foreach (var feature in features)
        {
            StringAssert.StartsWith(feature.Geom, "POINT(");
            Assert.IsNotEmpty(feature.Info);
        }

        // Every feature carries an errorId.
        var featureIds = features.Select(f => f.ErrorId).ToList();
        Assert.IsTrue(featureIds.All(id => !string.IsNullOrEmpty(id)), "every feature has an errorId");

        // Tree carries the grouping keys; the composite root carries the filter keys (the filter spans map + tree).
        Assert.IsEmpty(config.Tree.GroupBy, "no grouping (flat list) unless groupBy is configured");
        Assert.IsNotNull(config.FilterBy);
        Assert.IsEmpty(config.FilterBy, "no filters are offered unless filterBy is configured");
        Assert.IsNotEmpty(config.Tree.Items);

        // Each feature's errorId also appears on a flat tree item (cross-select correlation).
        var itemIds = config.Tree.Items.Where(i => i.Id is not null).Select(i => i.Id!).ToHashSet();
        Assert.IsTrue(featureIds.All(itemIds.Contains), "every feature's errorId is present on a tree item");

        // Each item carries a display label, severity icon/color and metadata including the raw validator message.
        foreach (var item in config.Tree.Items)
        {
            Assert.IsNotEmpty(item.Label);
            Assert.IsTrue(item.Severity is "error" or "warning", "item carries an error or warning severity");
            Assert.IsTrue(item.Metadata.ContainsKey("Message"), "item metadata carries the validator message");
        }

        var statusMessage = processResult.StatusMessage;
        Assert.IsNotNull(statusMessage);

        // Assert.HasCount(4, statusMessage);
        Assert.AreEqual("Error visualization created", statusMessage["en"]);
    }

    [TestMethod]
    public async Task IncludeMapOnlyOmitsTree()
    {
        var process = new XtfErrorVisualizationProcess(include: ["map"]);
        var xtfLog = new PipelineFile(XtfLogPath, "errorLogWithErrors.xtf");

        var processResult = await process.RunAsync(xtfLog).ConfigureAwait(false);

        var visualization = processResult.Visualization;
        Assert.IsNotNull(visualization);
        Assert.IsNotNull(visualization.Data.Map);
        Assert.IsNull(visualization.Data.Tree);
    }

    [TestMethod]
    public async Task IncludeTreeOnlyOmitsMap()
    {
        var process = new XtfErrorVisualizationProcess(include: ["tree"]);
        var xtfLog = new PipelineFile(XtfLogPath, "errorLogWithErrors.xtf");

        var processResult = await process.RunAsync(xtfLog).ConfigureAwait(false);

        var visualization = processResult.Visualization;
        Assert.IsNotNull(visualization);
        Assert.IsNull(visualization.Data.Map);
        Assert.IsNotNull(visualization.Data.Tree);
    }

    [TestMethod]
    public async Task TreeUsesConfiguredGroupBy()
    {
        var process = new XtfErrorVisualizationProcess(include: ["tree"], groupBy: ["Class"]);
        var xtfLog = new PipelineFile(XtfLogPath, "errorLogWithErrors.xtf");

        var processResult = await process.RunAsync(xtfLog).ConfigureAwait(false);

        var visualization = processResult.Visualization;
        Assert.IsNotNull(visualization);
        Assert.IsNotNull(visualization.Data.Tree);
        CollectionAssert.AreEqual(new[] { "Class" }, visualization.Data.Tree.GroupBy.ToList());
    }

    [TestMethod]
    public async Task TreeUsesConfiguredFilterBy()
    {
        var process = new XtfErrorVisualizationProcess(include: ["tree"], filterBy: ["Class", "Error type"]);
        var xtfLog = new PipelineFile(XtfLogPath, "errorLogWithErrors.xtf");

        var processResult = await process.RunAsync(xtfLog).ConfigureAwait(false);

        var visualization = processResult.Visualization;
        Assert.IsNotNull(visualization);
        Assert.IsNotNull(visualization.Data.Tree);
        Assert.IsNotNull(visualization.Data.FilterBy);
        CollectionAssert.AreEqual(new[] { "Class", "Error type" }, visualization.Data.FilterBy.ToList());
    }
}
