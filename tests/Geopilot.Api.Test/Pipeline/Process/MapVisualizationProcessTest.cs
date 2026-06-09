using Geopilot.Api.Pipeline.Process.MapVisualization;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Moq;
using System.Text.Json;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class MapVisualizationProcessTest
{
    private const string SwisstopoBaseMapWmtsCapabilitiesUrl = "https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml";

    [TestMethod]
    public async Task SunnyDay()
    {
        var pipelineFileManagerMock = new Mock<IPipelineFileManager>();
        pipelineFileManagerMock.Setup(m => m.GeneratePipelineFile(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string originalFileName, string fileExtension) =>
            {
                var filePath = Path.Combine(Path.GetTempPath(), $"{originalFileName}_{Guid.NewGuid()}.{fileExtension}");
                return new PipelineFile(filePath, originalFileName + "." + fileExtension);
            });
        var process = new MapVisualizationProcess(pipelineFileManagerMock.Object);

        var errorLogXtf = new PipelineFile("TestData/DownloadFiles/ilicop/errorLogWithErrors.xtf", "errorLogWithErrors.xtf");
        var processResult = await process.RunAsync(errorLogXtf).ConfigureAwait(false);

        Assert.IsNotNull(processResult);
        Assert.HasCount(2, processResult);

        var configFile = processResult["map_visualization_config_file"] as PipelineFile;
        var statusMessage = processResult["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(configFile);
        Assert.IsNotNull(statusMessage);
        Assert.HasCount(4, statusMessage);

        var config = ReadConfig(configFile);
        Assert.IsNotNull(config);

        // Always a base map (WMTS) layer followed by a feature layer.
        Assert.HasCount(2, config.Layers);

        var baseMapLayer = config.Layers[0];
        Assert.AreEqual(SwisstopoBaseMapWmtsCapabilitiesUrl, baseMapLayer.Wmts);
        Assert.IsNull(baseMapLayer.Features);

        var featureLayer = config.Layers[1];
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
    }

    private static MapVisualizationConfig? ReadConfig(PipelineFile configFile)
    {
        using var stream = configFile.OpenReadFileStream();
        return JsonSerializer.Deserialize<MapVisualizationConfig>(stream);
    }
}
