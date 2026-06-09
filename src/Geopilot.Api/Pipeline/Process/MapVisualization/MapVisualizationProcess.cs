using Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Globalization;
using System.Text.Json;

namespace Geopilot.Api.Pipeline.Process.MapVisualization;

/// <summary>
/// Turns an error-log XTF into a map-visualization config (<see cref="MapVisualizationConfig"/>).
/// The config always contains the swisstopo base map (a WMTS layer) and a feature layer holding a
/// point feature for every validation error that has an associated coordinate, with the error
/// message as the feature's info text.
/// </summary>
internal class MapVisualizationProcess
{
    private const string OutputMappingConfigFile = "map_visualization_config_file";
    private const string OutputMappingStatusMessage = "status_message";

    /// <summary>
    /// WMTS capabilities URL of the swisstopo base map of Switzerland (LV95 / EPSG:2056), matching the
    /// coordinate reference system of INTERLIS error coordinates. Used when no override is configured.
    /// The host of this URL must be allowed by the client's Content-Security-Policy (see
    /// <c>WebApplicationExtensions.MapSpaFallback</c>), otherwise the browser blocks the base map.
    /// </summary>
    public const string DefaultBaseMapWmtsCapabilitiesUrl = "https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml";

    /// <summary>
    /// Identifier of the swisstopo base map layer to display. The default WMTS service advertises hundreds
    /// of layers; this restricts the base map to the colored national map. Without it the client would
    /// add every advertised layer to the map.
    /// </summary>
    private const string DefaultBaseMapLayerId = "ch.swisstopo.pixelkarte-farbe";

    private static readonly Dictionary<string, string> SuccessfulStatusMessage = new()
    {
        { "de", "Kartenvisualisierung erstellt" },
        { "fr", "Visualisation cartographique créée" },
        { "it", "Visualizzazione cartografica creata" },
        { "en", "Map visualization created" },
    };

    private readonly IPipelineFileManager pipelineFileManager;
    private readonly string baseMapWmtsCapabilitiesUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapVisualizationProcess"/> class.
    /// </summary>
    /// <param name="pipelineFileManager">Manages the step's temporary output files.</param>
    /// <param name="baseMapWmtsCapabilitiesUrl">Optional override for the base map WMTS capabilities URL. Defaults to the swisstopo map of Switzerland.</param>
    public MapVisualizationProcess(IPipelineFileManager pipelineFileManager, string? baseMapWmtsCapabilitiesUrl = null)
    {
        this.pipelineFileManager = pipelineFileManager;
        this.baseMapWmtsCapabilitiesUrl = string.IsNullOrWhiteSpace(baseMapWmtsCapabilitiesUrl)
            ? DefaultBaseMapWmtsCapabilitiesUrl
            : baseMapWmtsCapabilitiesUrl;
    }

    /// <summary>
    /// Builds the map-visualization config from the given error-log XTF.
    /// </summary>
    /// <param name="errorLogXtf">The error-log XTF produced by the validation.</param>
    /// <returns>The output map with the generated config file and a status message.</returns>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile errorLogXtf)
    {
        List<LogError> errors;
        using (var errorLogStream = errorLogXtf.OpenReadFileStream())
        using (var errorLogReader = new StreamReader(errorLogStream))
        {
            errors = XtfLogParser.Parse(errorLogReader);
        }

        var errorFeatures = errors
            .Where(error => error.Geometry?.Coord != null)
            .Select(error => new MapFeature
            {
                Geom = ToPointWkt(error.Geometry!.Coord!),
                Info = error.Message ?? string.Empty,
            })
            .ToList();

        var config = new MapVisualizationConfig
        {
            Layers =
            [
                new MapLayer { Wmts = baseMapWmtsCapabilitiesUrl, LayerIds = [DefaultBaseMapLayerId] },
                new MapLayer { Features = errorFeatures },
            ],
        };

        var configFile = pipelineFileManager.GeneratePipelineFile("mapVisualizationConfig", "json");
        using (var fileStream = configFile.OpenWriteFileStream())
        {
            await JsonSerializer.SerializeAsync(fileStream, config);
        }

        return new Dictionary<string, object?>
        {
            { OutputMappingConfigFile, configFile },
            { OutputMappingStatusMessage, SuccessfulStatusMessage },
        };
    }

    /// <summary>
    /// Formats an INTERLIS coordinate as a WKT point. C1 is the easting, C2 the northing.
    /// </summary>
    private static string ToPointWkt(Coord coord)
    {
        var easting = coord.C1.ToString(CultureInfo.InvariantCulture);
        var northing = coord.C2.ToString(CultureInfo.InvariantCulture);
        return $"POINT({easting} {northing})";
    }
}
