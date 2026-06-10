namespace Geopilot.Pipeline;

/// <summary>
/// Kind of built-in visualization that consumes a JSON config produced by a pipeline step.
/// Visualizations are a static, in-tree set (not plugin-extensible). Each value corresponds to
/// a React component on the frontend that renders the persisted config.
/// </summary>
public enum VisualizationKind
{
    /// <summary>
    /// Hierarchical tree view (e.g. INTERLIS validation error tree).
    /// </summary>
    Tree,

    /// <summary>
    /// Map view rendering WMTS base layers and WKT feature layers (e.g. INTERLIS error locations).
    /// </summary>
    Map,
}

/// <summary>
/// Tracking record for a visualization config file produced by a pipeline step. The file is
/// persisted to the download file store so the frontend can fetch it through the regular
/// download endpoint and feed it to the matching visualization component.
/// </summary>
/// <param name="Kind">The kind of visualization this config drives.</param>
/// <param name="OriginalFileName">The human-readable file name reported by the producing process.</param>
/// <param name="PersistedFileName">The file name on disk under the job's download directory.</param>
public record StepVisualization(VisualizationKind Kind, string OriginalFileName, string PersistedFileName);
