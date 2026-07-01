using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// Turns the validator's error-log XTF into a composite <c>xtfError</c> visualization combining a map
/// view and an error-tree view of the same errors. <c>include</c> selects which views to produce
/// (default: both). The two views share one envelope so the frontend can render and later cross-link them.
/// </summary>
internal class XtfErrorVisualizationProcess
{
    private const string OutputMappingVisualization = "visualization";
    private const string OutputMappingStatusMessage = "status_message";
    private const string IncludeMap = "map";
    private const string IncludeTree = "tree";

    private static readonly Dictionary<string, string> SuccessfulStatusMessage = new()
    {
        { "de", "Fehlervisualisierung erstellt" },
        { "fr", "Visualisation des erreurs créée" },
        { "it", "Visualizzazione degli errori creata" },
        { "en", "Error visualization created" },
    };

    private static readonly string[] DefaultGroupBy = ["Model", "Topic", "Class"];
    private static readonly string[] DefaultFilterBy = ["Model", "Topic", "Class", "Error type"];

    private readonly bool includeMap;
    private readonly bool includeTree;
    private readonly string baseMapWmtsCapabilitiesUrl;
    private readonly IReadOnlyList<string> groupBy;
    private readonly IReadOnlyList<string> filterBy;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtfErrorVisualizationProcess"/> class.
    /// </summary>
    /// <param name="include">Views to produce ("map", "tree"). Null or empty means both.</param>
    /// <param name="baseMapWmtsCapabilitiesUrl">Optional override for the base map WMTS capabilities URL.</param>
    /// <param name="groupBy">Metadata keys the frontend groups the tree items by. Null or empty means model, topic, class.</param>
    /// <param name="filterBy">Metadata keys the frontend offers as filters, in display order. Null or empty means model, topic, class, error type.</param>
    public XtfErrorVisualizationProcess(HashSet<string>? include = null, string? baseMapWmtsCapabilitiesUrl = null, IReadOnlyList<string>? groupBy = null, IReadOnlyList<string>? filterBy = null)
    {
        var selected = include is { Count: > 0 }
            ? new HashSet<string>(include, StringComparer.OrdinalIgnoreCase)
            : null;
        includeMap = selected?.Contains(IncludeMap) ?? true;
        includeTree = selected?.Contains(IncludeTree) ?? true;
        this.baseMapWmtsCapabilitiesUrl = string.IsNullOrWhiteSpace(baseMapWmtsCapabilitiesUrl)
            ? MapVisualizationBuilder.DefaultBaseMapWmtsCapabilitiesUrl
            : baseMapWmtsCapabilitiesUrl;
        this.groupBy = groupBy is { Count: > 0 } ? groupBy : DefaultGroupBy;
        this.filterBy = filterBy is { Count: > 0 } ? filterBy : DefaultFilterBy;
    }

    /// <summary>
    /// Builds the composite visualization from the given error-log XTF.
    /// </summary>
    /// <param name="xtfLog">The error-log XTF produced by the validation.</param>
    /// <returns>The output map with the composite visualization envelope and a status message.</returns>
    [PipelineProcessRun]
    public Task<Dictionary<string, object?>> RunAsync(IPipelineFile xtfLog)
    {
        var errors = XtfLogParser.Parse(xtfLog)
            .Select((error, index) => new IndexedError($"e{index}", error))
            .ToList();

        var config = new XtfErrorVisualizationConfig
        {
            Map = includeMap ? MapVisualizationBuilder.Build(errors, baseMapWmtsCapabilitiesUrl) : null,
            Tree = includeTree
                ? new TreeVisualizationConfig { Items = LogErrorToTreeItemMapper.Map(errors), GroupBy = groupBy, FilterBy = filterBy }
                : null,
        };

        return Task.FromResult(new Dictionary<string, object?>
        {
            { OutputMappingVisualization, VisualizationFactory.XtfError(config) },
            { OutputMappingStatusMessage, SuccessfulStatusMessage },
        });
    }
}
