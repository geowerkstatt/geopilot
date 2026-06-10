using Geopilot.Api.Pipeline.Process.XtfDiff;
using System.Text.Json;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class XtfDiffMapConfigBuilderTest
{
    private const string SwisstopoBaseMapWmtsCapabilitiesUrl = "https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml";

    [TestMethod]
    public void Build()
    {
        var diffEntries = ReadDiffEntries("TestData/XtfDiff/diff.json");
        var config = XtfDiffMapConfigBuilder.Build(diffEntries, SwisstopoBaseMapWmtsCapabilitiesUrl);

        // Base map followed by the deleted, changed (before), changed (after) and added layers.
        Assert.HasCount(5, config.Layers);

        var baseMapLayer = config.Layers[0];
        Assert.AreEqual(SwisstopoBaseMapWmtsCapabilitiesUrl, baseMapLayer.Wmts);
        Assert.IsNull(baseMapLayer.Features);
        Assert.IsNotNull(baseMapLayer.LayerIds);
        Assert.HasCount(2, baseMapLayer.LayerIds);
        Assert.AreEqual("ch.swisstopo.pixelkarte-grau", baseMapLayer.LayerIds[0]);
        Assert.AreEqual("ch.swisstopo.pixelkarte-farbe", baseMapLayer.LayerIds[1]);

        var deletedLayer = config.Layers[1];
        Assert.AreEqual(XtfDiffMapConfigBuilder.DeletedLayerColor, deletedLayer.Color);
        Assert.AreEqual("Gelöschte Geometrien", deletedLayer.Title?["de"]);
        Assert.IsNotNull(deletedLayer.Features);
        // The deleted object change (valueType "object") carries no geometry and is not mapped.
        Assert.HasCount(1, deletedLayer.Features);
        Assert.AreEqual("POINT (2710010.5 1265020.75)", deletedLayer.Features[0].Geom);
        Assert.AreEqual("DMAV_Grundstuecke_V1_0.Grundstuecke.Grenzpunkt.Geometrie (TID 22222222-2222-4222-8222-222222222222)", deletedLayer.Features[0].Info);

        // The fixture's surface values are curve WKT (CURVEPOLYGON / COMPOUNDCURVE) as emitted by
        // the XTF-Diff-Tool; the builder linearizes them for the map client's WKT parser.
        var changedOldLayer = config.Layers[2];
        Assert.AreEqual(XtfDiffMapConfigBuilder.ChangedOldLayerColor, changedOldLayer.Color);
        Assert.IsNotNull(changedOldLayer.Features);
        Assert.HasCount(1, changedOldLayer.Features);
        Assert.AreEqual("POLYGON ((2710000 1265000, 2710100 1265000, 2710100 1265100, 2710000 1265100, 2710000 1265000))", changedOldLayer.Features[0].Geom);

        var changedNewLayer = config.Layers[3];
        Assert.AreEqual(XtfDiffMapConfigBuilder.ChangedNewLayerColor, changedNewLayer.Color);
        Assert.IsNotNull(changedNewLayer.Features);
        Assert.HasCount(1, changedNewLayer.Features);
        Assert.AreEqual("POLYGON ((2710000 1265000, 2710120 1265000, 2710120 1265100, 2710000 1265100, 2710000 1265000))", changedNewLayer.Features[0].Geom);

        var addedLayer = config.Layers[4];
        Assert.AreEqual(XtfDiffMapConfigBuilder.AddedLayerColor, addedLayer.Color);
        Assert.IsNotNull(addedLayer.Features);
        // One single-WKT geometry change plus one carrying an array of two WKT values;
        // the added attribute change (valueType "attribute") is not mapped.
        Assert.HasCount(3, addedLayer.Features);
        Assert.AreEqual("POINT (2710045.205 1265015.255)", addedLayer.Features[0].Geom);
        Assert.AreEqual("POINT (2710000 1265000)", addedLayer.Features[1].Geom);
        Assert.AreEqual("POINT (2710001 1265001)", addedLayer.Features[2].Geom);
    }

    [TestMethod]
    public void BuildWithoutDiffEntriesKeepsAllLayers()
    {
        var config = XtfDiffMapConfigBuilder.Build(Array.Empty<XtfDiffEntry>(), SwisstopoBaseMapWmtsCapabilitiesUrl);

        Assert.HasCount(5, config.Layers);
        foreach (var featureLayer in config.Layers.Skip(1))
        {
            Assert.IsNotNull(featureLayer.Features);
            Assert.IsEmpty(featureLayer.Features);
        }
    }

    private static List<XtfDiffEntry> ReadDiffEntries(string path)
    {
        using var stream = File.OpenRead(path);
        var diffEntries = JsonSerializer.Deserialize<List<XtfDiffEntry>>(stream);
        Assert.IsNotNull(diffEntries);
        return diffEntries;
    }
}
