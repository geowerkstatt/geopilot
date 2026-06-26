using Geopilot.Pipeline;
using Geopilot.Pipeline.Processes.XtfErrorVisualization;
using Geopilot.Pipeline.Visualization;
using Newtonsoft.Json;

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
        Assert.HasCount(2, processResult);

        var visualization = processResult["visualization"] as Visualization<XtfErrorVisualizationConfig>;
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
        Assert.IsNull(baseMapLayer.Features);
        Assert.IsNotNull(baseMapLayer.LayerIds);
        Assert.HasCount(2, baseMapLayer.LayerIds);
        var featureLayer = config.Map.Layers[1];
        Assert.IsNull(featureLayer.Wmts);
        Assert.AreEqual("#e53835", featureLayer.Color);
        var features = featureLayer.Features;
        Assert.IsNotNull(features);

        // The fixture has 19 errors carrying a coordinate; each becomes a point feature.
        Assert.HasCount(19, features);
        foreach (var feature in features)
        {
            StringAssert.StartsWith(feature.Geom, "POINT(");
            Assert.IsNotEmpty(feature.Info);
        }

        // Tree: matches the recorded expectation.
        var expectedTree = Deserialize(File.ReadAllText("TestData/Expectations/XtfValidatorErrorTree/errorLogWithErrors.json"));
        CollectionAssert.AreEqual(expectedTree, config.Tree.Nodes.ToList(), "error tree is not as expected");

        var statusMessage = processResult["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(statusMessage);
        Assert.HasCount(4, statusMessage);
        Assert.AreEqual("Error visualization created", statusMessage["en"]);
    }

    [TestMethod]
    public async Task IncludeMapOnlyOmitsTree()
    {
        var process = new XtfErrorVisualizationProcess(include: ["map"]);
        var xtfLog = new PipelineFile(XtfLogPath, "errorLogWithErrors.xtf");

        var processResult = await process.RunAsync(xtfLog).ConfigureAwait(false);

        var visualization = processResult["visualization"] as Visualization<XtfErrorVisualizationConfig>;
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

        var visualization = processResult["visualization"] as Visualization<XtfErrorVisualizationConfig>;
        Assert.IsNotNull(visualization);
        Assert.IsNull(visualization.Data.Map);
        Assert.IsNotNull(visualization.Data.Tree);
    }

    private static List<TreeNode>? Deserialize(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<List<TreeNode>>(jsonReader);
    }
}
