using Geopilot.Pipeline.Visualization;

namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

/// <summary>
/// Payload of the XTF diff visualization: the changed objects on a map and every change in a table. As with the map
/// and the tree of the XTF error visualization, the frontend shows the views that are present.
/// </summary>
internal sealed record XtfDiffVisualizationConfig
{
    /// <summary>
    /// Gets the map with the old and the new state of the changed objects, or <see langword="null"/> when the map
    /// is not included or no change has a location.
    /// </summary>
    public MapVisualizationConfig? Map { get; init; }

    /// <summary>
    /// Gets the table of the changes, or <see langword="null"/> when the table is not included.
    /// </summary>
    public TableVisualizationConfig? Table { get; init; }

    /// <summary>
    /// Wraps this config in its visualization envelope, binding it to the <c>xtfDiff</c> discriminator in one
    /// place so the type and the data cannot drift apart.
    /// </summary>
    /// <returns>The XTF diff visualization envelope.</returns>
    public Visualization<XtfDiffVisualizationConfig> ToVisualization() => new("xtfDiff", this);
}
