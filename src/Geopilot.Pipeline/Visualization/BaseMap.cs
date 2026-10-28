namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// The base map a map visualization draws its features on: the swisstopo national map, unless the step
/// configures another WMTS service.
/// </summary>
internal static class BaseMap
{
    /// <summary>
    /// WMTS capabilities URL of the swisstopo base map of Switzerland (LV95 / EPSG:2056), matching the
    /// coordinate reference system of INTERLIS data. The host must be allowed by the client
    /// Content-Security-Policy (see <c>WebApplicationExtensions.MapSpaFallback</c>), otherwise the browser
    /// blocks the base map.
    /// </summary>
    public const string DefaultWmtsCapabilitiesUrl = "https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml";

    /// <summary>
    /// Default data-owner credit shown for the swisstopo base map.
    /// </summary>
    public const string DefaultAttribution = "swisstopo";

    /// <summary>
    /// Default target the base map attribution links to: the swisstopo terms of use for free geodata and
    /// geoservices.
    /// </summary>
    public const string DefaultAttributionUrl = "https://www.swisstopo.admin.ch/de/nutzungsbedingungen-kostenlose-geodaten-und-geodienste";

    /// <summary>
    /// Identifier of the swisstopo colored national map: the default base map. Without it the client would
    /// add every layer the WMTS service advertises.
    /// </summary>
    private const string DefaultLayerId = "ch.swisstopo.pixelkarte-farbe";

    /// <summary>
    /// Identifier of the swisstopo gray national map, offered as an additional base layer beneath the
    /// colored one and selectable in the layer switcher.
    /// </summary>
    private const string GrayLayerId = "ch.swisstopo.pixelkarte-grau";

    private static readonly Dictionary<string, string> Title = new()
    {
        { "de", "Hintergrundkarte" },
        { "fr", "Carte de base" },
        { "it", "Mappa di base" },
        { "en", "Base map" },
    };

    /// <summary>
    /// Creates the base map layer. A value that is not configured falls back to the swisstopo default. The
    /// colored map is listed last so it stays the default base map; the gray map sits beneath it and can be
    /// selected in the layer switcher.
    /// </summary>
    /// <param name="wmtsCapabilitiesUrl">The configured WMTS capabilities URL, if any.</param>
    /// <param name="attribution">The configured copyright/attribution text, if any.</param>
    /// <param name="attributionUrl">The configured URL the attribution links to, if any.</param>
    /// <returns>The base map layer.</returns>
    public static MapLayer CreateLayer(string? wmtsCapabilitiesUrl, string? attribution, string? attributionUrl) => new()
    {
        Title = Title,
        Wmts = OrDefault(wmtsCapabilitiesUrl, DefaultWmtsCapabilitiesUrl),
        LayerIds = [GrayLayerId, DefaultLayerId],
        Attribution = OrDefault(attribution, DefaultAttribution),
        AttributionUrl = OrDefault(attributionUrl, DefaultAttributionUrl),
    };

    private static string OrDefault(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;
}
