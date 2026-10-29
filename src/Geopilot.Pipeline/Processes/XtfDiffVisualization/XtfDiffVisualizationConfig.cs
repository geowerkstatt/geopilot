using Geopilot.Pipeline.Visualization;

namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

/// <summary>
/// Payload of the XTF diff visualization: every change in a table and the changed objects on a map, wherever a
/// change has a location.
/// </summary>
internal sealed record XtfDiffVisualizationConfig
{
    /// <summary>
    /// Gets the map with the old and the new state of the changed objects, or <see langword="null"/> when no change
    /// has a location.
    /// </summary>
    public MapVisualizationConfig? Map { get; init; }

    /// <summary>
    /// Gets the table of the changes. Unlike the map it is always there, because it also lists the changes without a
    /// location.
    /// </summary>
    public required TableVisualizationConfig Table { get; init; }

    /// <summary>
    /// Wraps this config in its visualization envelope, binding it to the <c>xtfDiff</c> discriminator in one
    /// place so the type and the data cannot drift apart.
    /// </summary>
    /// <returns>The XTF diff visualization envelope.</returns>
    public Visualization<XtfDiffVisualizationConfig> ToVisualization() => new("xtfDiff", this);
}
