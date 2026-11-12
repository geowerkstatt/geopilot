using Geopilot.Pipeline.Visualization;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class BaseMapTest
{
    [TestMethod]
    public void CreateLayerFallsBackToTheSwisstopoMapWhenNothingIsConfigured()
    {
        var layer = BaseMap.CreateLayer(null, [], " ", string.Empty, null);

        Assert.AreEqual("https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml", layer.Wmts);
        Assert.AreEqual("swisstopo", layer.Attribution);
        Assert.AreEqual("https://www.swisstopo.admin.ch/de/nutzungsbedingungen-kostenlose-geodaten-und-geodienste", layer.AttributionUrl);
        Assert.IsNotNull(layer.LayerIds);
        CollectionAssert.AreEqual(new[] { "ch.swisstopo.pixelkarte-grau" }, layer.LayerIds.ToArray());
        Assert.AreEqual(0.5, layer.Opacity);
        Assert.IsNull(layer.Features);
    }

    [TestMethod]
    public void CreateLayerUsesTheConfiguredService()
    {
        var layer = BaseMap.CreateLayer("https://wmts.example.ch/WMTSCapabilities.xml", ["grundkarte"], "Example", "https://example.ch/terms", null);

        Assert.AreEqual("https://wmts.example.ch/WMTSCapabilities.xml", layer.Wmts);
        Assert.IsNotNull(layer.LayerIds);
        CollectionAssert.AreEqual(new[] { "grundkarte" }, layer.LayerIds.ToArray());
        Assert.AreEqual("Example", layer.Attribution);
        Assert.AreEqual("https://example.ch/terms", layer.AttributionUrl);
        Assert.AreEqual(1, layer.Opacity);
    }

    [TestMethod]
    [DataRow(null, 0.8)]
    [DataRow("ch.swisstopo.pixelkarte-farbe", 0.0)]
    [DataRow("ch.swisstopo.pixelkarte-farbe", 1.0)]
    public void CreateLayerUsesTheConfiguredOpacity(string? layerId, double opacity)
    {
        var layer = BaseMap.CreateLayer(null, layerId is null ? null : [layerId], null, null, opacity);

        Assert.AreEqual(opacity, layer.Opacity);
    }

    [TestMethod]
    [DataRow(-0.1)]
    [DataRow(1.1)]
    [DataRow(double.NaN)]
    public void CreateLayerRejectsAnOpacityOutsideZeroToOne(double opacity)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BaseMap.CreateLayer(null, null, null, null, opacity));
    }
}
