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
    /// Identifier of the swisstopo gray national map: the default base map. A map visualization is a thematic
    /// map, so the base map only serves orientation and its colors must not compete with the features.
    /// Without an identifier the client would add every layer the WMTS service advertises.
    /// </summary>
    private const string DefaultLayerId = "ch.swisstopo.pixelkarte-grau";

    /// <summary>
    /// Opacity of the default gray map, so it stays pale behind the features. A map chosen with its layer
    /// identifiers is drawn opaque, because its colors are meant to be seen.
    /// </summary>
    private const double DefaultLayerOpacity = 0.5;

    private static readonly Dictionary<string, string> Title = new()
    {
        { "de", "Hintergrundkarte" },
        { "fr", "Carte de base" },
        { "it", "Mappa di base" },
        { "en", "Base map" },
    };

    /// <summary>
    /// Creates the base map layer. A value that is not configured falls back to the swisstopo default.
    /// </summary>
    /// <param name="wmtsCapabilitiesUrl">The configured WMTS capabilities URL, if any.</param>
    /// <param name="layerIds">The configured layers to request from the service, drawn in this order (the last on top), if any.</param>
    /// <param name="attribution">The configured copyright/attribution text, if any.</param>
    /// <param name="attributionUrl">The configured URL the attribution links to, if any.</param>
    /// <param name="opacity">The configured opacity from 0 to 1, if any. Without it the default gray map is half transparent and configured layers are opaque.</param>
    /// <returns>The base map layer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> lies outside 0 to 1.</exception>
    public static MapLayer CreateLayer(string? wmtsCapabilitiesUrl, IReadOnlyList<string>? layerIds, string? attribution, string? attributionUrl, double? opacity)
    {
        if (opacity is not (null or (>= 0 and <= 1)))
            throw new ArgumentOutOfRangeException(nameof(opacity), opacity, "The base map opacity must lie between 0 and 1.");

        var configuredLayers = layerIds is { Count: > 0 } ? layerIds : null;
        return new()
        {
            Title = Title,
            Wmts = OrDefault(wmtsCapabilitiesUrl, DefaultWmtsCapabilitiesUrl),

            // An empty list must not reach the client, which would then show every layer the service advertises.
            LayerIds = configuredLayers is null ? [DefaultLayerId] : [.. configuredLayers],
            Attribution = OrDefault(attribution, DefaultAttribution),
            AttributionUrl = OrDefault(attributionUrl, DefaultAttributionUrl),
            Opacity = opacity ?? (configuredLayers is null ? DefaultLayerOpacity : 1),
        };
    }

    private static string OrDefault(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;
}
