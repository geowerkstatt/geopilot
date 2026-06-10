using Geopilot.Api.Pipeline.Process.MapVisualization;
using System.Text.Json;

namespace Geopilot.Api.Pipeline.Process.XtfDiff;

/// <summary>
/// Builds the map-visualization config (<see cref="MapVisualizationConfig"/>) for an XTF diff.
/// Only changes with the geometry value type are mapped; their WKT values become map features,
/// grouped into one layer per change kind (deleted, changed before/after, added) on top of the
/// base map. The layers are always present, even when empty, so the client's layer switcher
/// shows a consistent legend.
/// </summary>
internal static class XtfDiffMapConfigBuilder
{
    /// <summary>Color of deleted geometries. Matches the client theme's error color.</summary>
    public const string DeletedLayerColor = "#e53835";

    /// <summary>Color of the previous state of changed geometries; a lighter variant of <see cref="ChangedNewLayerColor"/>.</summary>
    public const string ChangedOldLayerColor = "#ffb74d";

    /// <summary>Color of the current state of changed geometries.</summary>
    public const string ChangedNewLayerColor = "#fb8c00";

    /// <summary>Color of added geometries.</summary>
    public const string AddedLayerColor = "#43a047";

    /// <summary>
    /// Identifier of the swisstopo grey national map, offered as an additional base layer in the
    /// client's layer switcher.
    /// </summary>
    private const string GrayBaseMapLayerId = "ch.swisstopo.pixelkarte-grau";

    /// <summary>
    /// Identifier of the swisstopo colored national map, the default base map.
    /// </summary>
    private const string ColoredBaseMapLayerId = "ch.swisstopo.pixelkarte-farbe";

    private static readonly Dictionary<string, string> BaseMapLayerTitle = new()
    {
        { "de", "Hintergrundkarte" },
        { "fr", "Carte de base" },
        { "it", "Mappa di base" },
        { "en", "Base map" },
    };

    private static readonly Dictionary<string, string> DeletedLayerTitle = new()
    {
        { "de", "Gelöschte Geometrien" },
        { "fr", "Géométries supprimées" },
        { "it", "Geometrie eliminate" },
        { "en", "Deleted geometries" },
    };

    private static readonly Dictionary<string, string> ChangedOldLayerTitle = new()
    {
        { "de", "Geänderte Geometrien (vorher)" },
        { "fr", "Géométries modifiées (avant)" },
        { "it", "Geometrie modificate (prima)" },
        { "en", "Changed geometries (before)" },
    };

    private static readonly Dictionary<string, string> ChangedNewLayerTitle = new()
    {
        { "de", "Geänderte Geometrien (nachher)" },
        { "fr", "Géométries modifiées (après)" },
        { "it", "Geometrie modificate (dopo)" },
        { "en", "Changed geometries (after)" },
    };

    private static readonly Dictionary<string, string> AddedLayerTitle = new()
    {
        { "de", "Neue Geometrien" },
        { "fr", "Nouvelles géométries" },
        { "it", "Nuove geometrie" },
        { "en", "Added geometries" },
    };

    /// <summary>
    /// Builds the map-visualization config for the given diff entries.
    /// </summary>
    /// <param name="diffEntries">The change records produced by the XTF-Diff-Tool.</param>
    /// <param name="baseMapWmtsCapabilitiesUrl">The WMTS capabilities URL of the base map.</param>
    /// <returns>The config with the base map and one feature layer per change kind.</returns>
    public static MapVisualizationConfig Build(IReadOnlyCollection<XtfDiffEntry> diffEntries, string baseMapWmtsCapabilitiesUrl)
    {
        var geometryChanges = diffEntries
            .Where(entry => string.Equals(entry.ValueType, XtfDiffEntry.ValueTypeGeometry, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new MapVisualizationConfig
        {
            Layers =
            [
                // The client draws layers in array order (later = on top): the previous states (deleted,
                // changed before) sit below the current states (changed after, added).
                new MapLayer
                {
                    Title = BaseMapLayerTitle,
                    Wmts = baseMapWmtsCapabilitiesUrl,
                    LayerIds = [GrayBaseMapLayerId, ColoredBaseMapLayerId],
                },
                new MapLayer
                {
                    Title = DeletedLayerTitle,
                    Color = DeletedLayerColor,
                    Features = CreateFeatures(geometryChanges, XtfDiffEntry.ChangeTypeDeleted, entry => entry.OldValue),
                },
                new MapLayer
                {
                    Title = ChangedOldLayerTitle,
                    Color = ChangedOldLayerColor,
                    Features = CreateFeatures(geometryChanges, XtfDiffEntry.ChangeTypeChanged, entry => entry.OldValue),
                },
                new MapLayer
                {
                    Title = ChangedNewLayerTitle,
                    Color = ChangedNewLayerColor,
                    Features = CreateFeatures(geometryChanges, XtfDiffEntry.ChangeTypeChanged, entry => entry.NewValue),
                },
                new MapLayer
                {
                    Title = AddedLayerTitle,
                    Color = AddedLayerColor,
                    Features = CreateFeatures(geometryChanges, XtfDiffEntry.ChangeTypeAdded, entry => entry.NewValue),
                },
            ],
        };
    }

    private static List<MapFeature> CreateFeatures(IEnumerable<XtfDiffEntry> geometryChanges, string changeType, Func<XtfDiffEntry, JsonElement> valueSelector)
    {
        return geometryChanges
            .Where(entry => string.Equals(entry.ChangeType, changeType, StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => ExtractWktValues(valueSelector(entry))
                .Select(wkt => new MapFeature { Geom = wkt, Info = BuildInfo(entry) }))
            .ToList();
    }

    /// <summary>
    /// Extracts the WKT strings from a diff value, which is either a single WKT string or an array
    /// of WKT strings. Other value kinds (null, missing, objects) yield no geometries. Curve WKT
    /// types (CURVEPOLYGON, COMPOUNDCURVE, ...) emitted by the XTF-Diff-Tool are linearized,
    /// because the map client's WKT parser only supports linear types.
    /// </summary>
    private static IEnumerable<string> ExtractWktValues(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var wkt = value.GetString();
            if (!string.IsNullOrWhiteSpace(wkt))
                yield return WktCurveLinearizer.Linearize(wkt);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
            {
                foreach (var wkt in ExtractWktValues(element))
                    yield return wkt;
            }
        }
    }

    private static string BuildInfo(XtfDiffEntry entry)
    {
        var elementName = string.IsNullOrEmpty(entry.AttributePath)
            ? entry.InterlisName ?? string.Empty
            : $"{entry.InterlisName}.{entry.AttributePath}";
        return string.IsNullOrEmpty(entry.Oid) ? elementName : $"{elementName} (TID {entry.Oid})";
    }
}
