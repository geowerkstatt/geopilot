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
        { "en", "Diff visualization created" },
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
        var entries = await DiffFileReader.ReadAsync(diff, cancellationToken);
        var config = new XtfDiffVisualizationConfig
        {
            Map = includeMap ? BuildMap(entries) : null,
            Table = includeTable ? new TableVisualizationConfig { Changes = entries.Select(entry => entry.Change).ToList() } : null,
        };

        return new XtfDiffVisualizationResult
        {
            Visualization = config.ToVisualization(),
            StatusMessage = CreatedStatusMessage,
        };
    }

    private MapVisualizationConfig? BuildMap(IReadOnlyList<DiffEntry> entries)
    {
        var oldFeatures = BuildFeatures(entries, entry => entry.OldLocation);
        var newFeatures = BuildFeatures(entries, entry => entry.NewLocation);
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

    private static List<MapFeature> BuildFeatures(IReadOnlyList<DiffEntry> entries, Func<DiffEntry, string?> locationOf)
    {
        // The tool gives every entry of an object the same location in a state, so one feature per object suffices.
        var features = new List<MapFeature>();
        var drawnObjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var location = locationOf(entry);
            if (location is not null && drawnObjectIds.Add(entry.Change.ObjectId))
                features.Add(new MapFeature { Id = entry.Change.ObjectId, Geom = location, Info = entry.Change.InterlisName });
        }

        return features;
    }
}
