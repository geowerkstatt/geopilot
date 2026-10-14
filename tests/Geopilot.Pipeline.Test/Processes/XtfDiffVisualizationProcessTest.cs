using Geopilot.Pipeline.Processes.XtfDiffVisualization;
using Geopilot.Pipeline.Visualization;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class XtfDiffVisualizationProcessTest
{
    private const string OldBuilding = "POLYGON((2600000 1200000,2600010 1200000,2600010 1200010,2600000 1200000))";
    private const string NewBuilding = "POLYGON((2600000 1200000,2600012 1200000,2600012 1200010,2600000 1200000))";

    [TestMethod]
    public async Task ListsEveryEntryOfTheDiffAsAChange()
    {
        var changes = Changes(await VisualizeAsync("changes.json"));

        Assert.HasCount(5, changes);

        var egid = changes[0];
        Assert.AreEqual("c0", egid.Id);
        Assert.AreEqual("building", egid.ObjectId);
        Assert.AreEqual(DiffChangeType.Changed, egid.ChangeType);
        Assert.AreEqual(DiffValueType.Attribute, egid.ValueType);
        Assert.AreEqual("DMAV_Bodenbedeckung_V1_0.Bodenbedeckung.BoFlaeche", egid.InterlisName);
        Assert.AreEqual("EGID", egid.AttributePath);
        Assert.AreEqual("190001", egid.OldValue);
        Assert.AreEqual("190002", egid.NewValue);

        Assert.AreEqual(DiffValueType.Geometry, changes[1].ValueType);

        var addedPoint = changes[2];
        Assert.AreEqual("c2", addedPoint.Id);
        Assert.AreEqual("boundary-point", addedPoint.ObjectId);
        Assert.AreEqual(DiffChangeType.Added, addedPoint.ChangeType);
        Assert.AreEqual(DiffValueType.Object, addedPoint.ValueType);
        Assert.IsNull(addedPoint.AttributePath);
        Assert.IsNull(addedPoint.OldValue);
        Assert.IsNull(addedPoint.NewValue);

        var deletedReference = changes[3];
        Assert.AreEqual(DiffChangeType.Deleted, deletedReference.ChangeType);
        Assert.AreEqual(DiffValueType.Reference, deletedReference.ValueType);
    }

    [TestMethod]
    public async Task WritesListsAndStructuresAsCompactJson()
    {
        var changes = Changes(await VisualizeAsync("changes.json"));

        Assert.AreEqual("[\"old-boundary-point\"]", changes[3].OldValue);
        Assert.AreEqual("[{\"Text\":\"CAIMAN\"},{\"Text\":\"SNAKE\"}]", changes[4].OldValue);
        Assert.AreEqual("[{\"Text\":\"SNAKE\"},{\"Text\":\"Bär\"}]", changes[4].NewValue);
    }

    [TestMethod]
    public async Task DrawsEachObjectOnceInTheLayerOfItsState()
    {
        var map = (await VisualizeAsync("changes.json")).Map;

        Assert.IsNotNull(map);
        Assert.HasCount(3, map.Layers);
        Assert.IsNotNull(map.Layers[0].Wmts, "the base map comes first, beneath the changes");

        // The building has two entries but one location per state, the added point exists in the new state only.
        var oldLayer = map.Layers[1];
        Assert.AreEqual(MapLayerStyle.Old, oldLayer.Style);
        CollectionAssert.AreEqual(new[] { ("building", OldBuilding) }, Features(oldLayer));

        var newLayer = map.Layers[2];
        Assert.AreEqual(MapLayerStyle.New, newLayer.Style);
        CollectionAssert.AreEqual(new[] { ("building", NewBuilding), ("boundary-point", "POINT(2600005 1200005)") }, Features(newLayer));
    }

    [TestMethod]
    public async Task UsesTheConfiguredBaseMap()
    {
        var process = new XtfDiffVisualizationProcess(baseMapWmtsCapabilitiesUrl: "https://wmts.example.ch/WMTSCapabilities.xml");

        var map = (await VisualizeAsync("changes.json", process)).Map;

        Assert.IsNotNull(map);
        Assert.AreEqual("https://wmts.example.ch/WMTSCapabilities.xml", map.Layers[0].Wmts);
    }

    [TestMethod]
    public async Task ShowsTheMapAndTheTableWithoutAnInclude()
    {
        foreach (var process in new[] { new XtfDiffVisualizationProcess(), new XtfDiffVisualizationProcess(include: []) })
        {
            var config = await VisualizeAsync("changes.json", process);

            Assert.IsNotNull(config.Map);
            Assert.IsNotNull(config.Table);
        }
    }

    [TestMethod]
    public async Task LeavesTheTableOutWhenOnlyTheMapIsIncluded()
    {
        var config = await VisualizeAsync("changes.json", new XtfDiffVisualizationProcess(include: [DiffView.Map]));

        Assert.IsNotNull(config.Map);
        Assert.IsNull(config.Table);
    }

    [TestMethod]
    public async Task LeavesTheMapOutWhenOnlyTheTableIsIncluded()
    {
        var config = await VisualizeAsync("changes.json", new XtfDiffVisualizationProcess(include: [DiffView.Table]));

        Assert.IsNull(config.Map);
        Assert.IsNotNull(config.Table);
    }

    [TestMethod]
    public async Task LeavesTheMapOutWhenNoChangeHasALocation()
    {
        var config = await VisualizeAsync("withoutLocations.json");

        Assert.HasCount(1, Changes(config));
        Assert.IsNull(config.Map);
    }

    [TestMethod]
    public async Task ShowsNoChangesForAnEmptyDiff()
    {
        var config = await VisualizeAsync("empty.json");

        Assert.IsEmpty(Changes(config));
        Assert.IsNull(config.Map);
    }

    [TestMethod]
    [DataRow("notJson.json", "JSON")]
    [DataRow("notAnArray.json", "array")]
    [DataRow("withoutOid.json", "oid")]
    [DataRow("unknownChangeType.json", "<moved>")]
    public async Task RejectsAFileThatIsNotADiffOfTheTool(string fileName, string cause)
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => VisualizeAsync(fileName));

        StringAssert.Contains(exception.Message, cause);
    }

    [TestMethod]
    public async Task ReadsTheDiffOfTheXtfDiffTool()
    {
        var config = await VisualizeAsync("xtfDiffToolDiff.json");
        var changes = Changes(config);

        Assert.HasCount(37, changes);
        Assert.AreEqual(7, changes.Count(change => change.ChangeType == DiffChangeType.Added));
        Assert.AreEqual(7, changes.Count(change => change.ChangeType == DiffChangeType.Deleted));
        Assert.AreEqual(23, changes.Count(change => change.ChangeType == DiffChangeType.Changed));
        Assert.IsNotNull(config.Map);
        Assert.HasCount(9, Features(config.Map.Layers[1]));
        Assert.HasCount(9, Features(config.Map.Layers[2]));
    }

    private static async Task<XtfDiffVisualizationConfig> VisualizeAsync(string fileName, XtfDiffVisualizationProcess? process = null)
    {
        var diff = new PipelineFile(Path.Combine("TestData", "XtfDiffVisualization", fileName), fileName);
        var result = await (process ?? new XtfDiffVisualizationProcess()).RunAsync(diff, CancellationToken.None);
        return result.Visualization.Data;
    }

    private static IReadOnlyList<DiffChange> Changes(XtfDiffVisualizationConfig config)
    {
        Assert.IsNotNull(config.Table);
        return config.Table.Changes;
    }

    private static (string Id, string Geom)[] Features(MapLayer layer)
    {
        Assert.IsNotNull(layer.Features);
        return layer.Features.Select(feature => (feature.Id, feature.Geom)).ToArray();
    }
}
