using Geopilot.Pipeline.Visualization;
using System.Globalization;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// Builds the <see cref="MapVisualizationConfig"/> for the XTF error visualization: the base map plus a
/// feature layer with a point for every validation error that carries a coordinate.
/// </summary>
internal static class MapVisualizationBuilder
{
    private static readonly Dictionary<string, string> ErrorLayerTitle = new()
    {
        { "de", "Fehler" },
        { "fr", "Erreurs" },
        { "it", "Errori" },
        { "en", "Errors" },
    };

    /// <summary>
    /// Builds the map config: the base map layer followed by a feature layer with one point per
    /// geo-located error.
    /// </summary>
    /// <param name="errors">The parsed validator errors.</param>
    /// <param name="baseMapLayer">The base map layer, see <see cref="BaseMap.CreateLayer"/>.</param>
    /// <returns>The map visualization config.</returns>
    public static MapVisualizationConfig Build(IReadOnlyList<IndexedError> errors, MapLayer baseMapLayer)
    {
        var errorFeatures = errors
            .Where(error => error.Error.Geometry?.Coord != null)
            .Select(error => new MapFeature
            {
                Id = error.Id,
                Geom = ToPointWkt(error.Error.Geometry!.Coord!),
                Info = error.Error.Message ?? string.Empty,
            })
            .ToList();

        return new MapVisualizationConfig
        {
            Layers =
            [
                baseMapLayer,
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
