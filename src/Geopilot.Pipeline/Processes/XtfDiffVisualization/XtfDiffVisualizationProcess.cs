using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

/// <summary>
/// Turns a diff of the XTF-Diff-Tool into an <c>xtfDiff</c> visualization: every change in a list, and the
/// changed objects on a map with the old state beneath the new one. The map shows the locations of the diff
/// as they are, the process computes no geometry.
/// </summary>
internal class XtfDiffVisualizationProcess
{
    private static readonly LocalizedText CreatedStatusMessage = new Dictionary<string, string>
    {
        { "de", "Differenzvisualisierung erstellt" },
        { "fr", "Visualisation des différences créée" },
        { "it", "Visualizzazione delle differenze creata" },
        { "en", "Change visualization created" },
    };

    private static readonly Dictionary<string, string> OldLayerTitle = new()
    {
        { "de", "Vorher" },
        { "fr", "Avant" },
        { "it", "Prima" },
        { "en", "Before" },
    };

    private static readonly Dictionary<string, string> NewLayerTitle = new()
    {
        { "de", "Nachher" },
        { "fr", "Après" },
        { "it", "Dopo" },
        { "en", "After" },
    };

    private readonly bool includeMap;
    private readonly bool includeTable;
    private readonly MapLayer baseMapLayer;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtfDiffVisualizationProcess"/> class.
    /// </summary>
    /// <param name="include">The views to show (<see cref="DiffView"/> names, case-insensitive). Null or empty means all of them.</param>
    /// <param name="baseMapWmtsCapabilitiesUrl">Optional override for the base map WMTS capabilities URL.</param>
    /// <param name="baseMapAttribution">Optional override for the base map copyright/attribution text.</param>
    /// <param name="baseMapAttributionUrl">Optional override for the URL the base map attribution links to.</param>
    public XtfDiffVisualizationProcess(IReadOnlyList<DiffView>? include = null, string? baseMapWmtsCapabilitiesUrl = null, string? baseMapAttribution = null, string? baseMapAttributionUrl = null)
    {
        var selected = include is { Count: > 0 } ? include : null;
        includeMap = selected?.Contains(DiffView.Map) ?? true;
        includeTable = selected?.Contains(DiffView.Table) ?? true;
        baseMapLayer = BaseMap.CreateLayer(baseMapWmtsCapabilitiesUrl, baseMapAttribution, baseMapAttributionUrl);
    }

    /// <summary>
    /// Builds the visualization of the given diff.
    /// </summary>
    /// <param name="diff">The diff in the JSON format of the XTF-Diff-Tool.</param>
    /// <param name="cancellationToken">Cancels reading the diff.</param>
    /// <returns>The visualization envelope and a status message.</returns>
    [PipelineProcessRun]
    public async Task<XtfDiffVisualizationResult> RunAsync(IPipelineFile diff, CancellationToken cancellationToken)
    {
        var changes = await DiffFileReader.ReadAsync(diff, cancellationToken);
        var config = new XtfDiffVisualizationConfig
        {
            Map = includeMap ? BuildMap(changes) : null,
            Table = includeTable ? new TableVisualizationConfig { Changes = changes.Select(ForTable).ToList() } : null,
        };

        return new XtfDiffVisualizationResult
        {
            Visualization = config.ToVisualization(),
            StatusMessage = CreatedStatusMessage,
        };
    }

    // Only a geometry change has a map button of its own, so only its locations are sent twice: here and in the map.
    private DiffChange ForTable(DiffChange change) =>
        includeMap && change.ValueType == DiffValueType.Geometry ? change : change with { OldLocation = null, NewLocation = null };

    private MapVisualizationConfig? BuildMap(IReadOnlyList<DiffChange> changes)
    {
        var oldFeatures = BuildFeatures(changes, change => change.OldLocation);
        var newFeatures = BuildFeatures(changes, change => change.NewLocation);
        if (oldFeatures.Count == 0 && newFeatures.Count == 0)
            return null;

        return new MapVisualizationConfig
        {
            Layers =
            [
                baseMapLayer,
                new MapLayer { Title = OldLayerTitle, Style = MapLayerStyle.Old, Features = oldFeatures },
                new MapLayer { Title = NewLayerTitle, Style = MapLayerStyle.New, Features = newFeatures },
            ],
        };
    }

    private static List<MapFeature> BuildFeatures(IReadOnlyList<DiffChange> changes, Func<DiffChange, string?> locationOf)
    {
        // The map knows a feature id once per layer, so the locations of an object form one feature per state.
        var locationsByObjectId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var firstChanges = new List<DiffChange>();
        foreach (var change in changes)
        {
            var location = locationOf(change);
            if (location is null)
                continue;

            if (!locationsByObjectId.TryGetValue(change.ObjectId, out var locations))
            {
                locations = [];
                locationsByObjectId.Add(change.ObjectId, locations);
                firstChanges.Add(change);
            }

            if (!locations.Contains(location, StringComparer.Ordinal))
                locations.Add(location);
        }

        return firstChanges
            .Select(change => new MapFeature
            {
                Id = change.ObjectId,
                Geom = CombineLocations(locationsByObjectId[change.ObjectId]),
                Info = change.InterlisName,
            })
            .ToList();
    }

    // One location as it is, several as a GEOMETRYCOLLECTION, the way the tool writes an object with several geometry
    // attributes. A changed geometry in a structure, for example, then shows beside the geometry of the object.
    private static string CombineLocations(List<string> locations) =>
        locations.Count == 1 ? locations[0] : $"GEOMETRYCOLLECTION ({string.Join(", ", locations)})";
}
