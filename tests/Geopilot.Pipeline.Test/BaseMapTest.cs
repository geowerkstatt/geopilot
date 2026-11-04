using Geopilot.Pipeline.Visualization;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class BaseMapTest
{
    [TestMethod]
    public void CreateLayerFallsBackToTheSwisstopoMapWhenNothingIsConfigured()
    {
        var layer = BaseMap.CreateLayer(null, [], " ", string.Empty);

        Assert.AreEqual("https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml", layer.Wmts);
        Assert.AreEqual("swisstopo", layer.Attribution);
        Assert.AreEqual("https://www.swisstopo.admin.ch/de/nutzungsbedingungen-kostenlose-geodaten-und-geodienste", layer.AttributionUrl);
        Assert.IsNotNull(layer.LayerIds);
        CollectionAssert.AreEqual(new[] { "ch.swisstopo.pixelkarte-grau", "ch.swisstopo.pixelkarte-farbe" }, layer.LayerIds.ToArray());
        Assert.IsNull(layer.Features);
    }

    [TestMethod]
    public void CreateLayerUsesTheConfiguredService()
    {
        var layer = BaseMap.CreateLayer("https://wmts.example.ch/WMTSCapabilities.xml", ["grundkarte"], "Example", "https://example.ch/terms");

        Assert.AreEqual("https://wmts.example.ch/WMTSCapabilities.xml", layer.Wmts);
        Assert.IsNotNull(layer.LayerIds);
        CollectionAssert.AreEqual(new[] { "grundkarte" }, layer.LayerIds.ToArray());
        Assert.AreEqual("Example", layer.Attribution);
        Assert.AreEqual("https://example.ch/terms", layer.AttributionUrl);
    }
}
