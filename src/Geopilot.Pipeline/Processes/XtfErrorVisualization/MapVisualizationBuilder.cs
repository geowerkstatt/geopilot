using Geopilot.Pipeline.Visualization;
using System.Globalization;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// Builds the <see cref="MapVisualizationConfig"/> for the XTF error visualization: the swisstopo base
/// map (WMTS) plus a feature layer with a point for every validation error that carries a coordinate.
/// </summary>
internal static class MapVisualizationBuilder
{
    /// <summary>
    /// WMTS capabilities URL of the swisstopo base map of Switzerland (LV95 / EPSG:2056), matching the
    /// coordinate reference system of INTERLIS error coordinates. The host must be allowed by the client
    /// Content-Security-Policy (see <c>WebApplicationExtensions.MapSpaFallback</c>), otherwise the browser
    /// blocks the base map.
    /// </summary>
    public const string DefaultBaseMapWmtsCapabilitiesUrl = "https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml";

    /// <summary>
    /// Default data-owner credit shown for the swisstopo base map.
    /// </summary>
    public const string DefaultBaseMapAttribution = "swisstopo";

    /// <summary>
    /// Default target the base map attribution links to: the swisstopo terms of use for free geodata and
    /// geoservices.
    /// </summary>
    public const string DefaultBaseMapAttributionUrl = "https://www.swisstopo.admin.ch/de/nutzungsbedingungen-kostenlose-geodaten-und-geodienste";

    /// <summary>
    /// Identifier of the swisstopo colored national map: the default base map. Without it the client would
    /// add every layer the WMTS service advertises.
    /// </summary>
    private const string DefaultBaseMapLayerId = "ch.swisstopo.pixelkarte-farbe";

    /// <summary>
    /// Identifier of the swisstopo grey national map, offered as an additional base layer beneath the
    /// colored one and selectable in the layer switcher.
    /// </summary>
    private const string GrayBaseMapLayerId = "ch.swisstopo.pixelkarte-grau";

    private static readonly Dictionary<string, string> BaseMapLayerTitle = new()
    {
        { "de", "Hintergrundkarte" },
        { "fr", "Carte de base" },
        { "it", "Mappa di base" },
        { "en", "Base map" },
    };

    private static readonly Dictionary<string, string> ErrorLayerTitle = new()
    {
        { "de", "Fehler" },
        { "fr", "Erreurs" },
        { "it", "Errori" },
        { "en", "Errors" },
    };

    /// <summary>
    /// Builds the map config: a base map (WMTS) layer followed by a feature layer with one point per
    /// geo-located error. The colored map is listed last so it stays the default base map; the grey map
    /// sits beneath it and can be selected in the layer switcher.
    /// </summary>
    /// <param name="errors">The parsed validator errors.</param>
    /// <param name="baseMapWmtsCapabilitiesUrl">The base map WMTS capabilities URL to use.</param>
    /// <param name="baseMapAttribution">The base map copyright/attribution text.</param>
    /// <param name="baseMapAttributionUrl">The URL the base map attribution links to.</param>
    /// <returns>The map visualization config.</returns>
    public static MapVisualizationConfig Build(IReadOnlyList<IndexedError> errors, string baseMapWmtsCapabilitiesUrl, string baseMapAttribution, string baseMapAttributionUrl)
    {
        var errorFeatures = errors
            .Where(error => error.Error.Geometry?.Coord != null)
            .Select(error => new MapFeature
            {
                ErrorId = error.Id,
                Geom = ToPointWkt(error.Error.Geometry!.Coord!),
                Info = error.Error.Message ?? string.Empty,
            })
            .ToList();

        return new MapVisualizationConfig
        {
            Layers =
            [
                new MapLayer
                {
                    Title = BaseMapLayerTitle,
                    Wmts = baseMapWmtsCapabilitiesUrl,
                    LayerIds = [GrayBaseMapLayerId, DefaultBaseMapLayerId],
                    Attribution = baseMapAttribution,
                    AttributionUrl = baseMapAttributionUrl,
                },
                new MapLayer { Title = ErrorLayerTitle, Features = errorFeatures },
            ],
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
