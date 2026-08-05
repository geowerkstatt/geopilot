namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// The config for the built-in tree visualization: a flat list of <see cref="Items"/> plus the fields
/// (<see cref="GroupBy"/>) the frontend groups them by to build the displayed hierarchy. The composite root
/// carries the filter fields, since the filter also applies to the map. Serialized to JSON and rendered by the
/// frontend tree component.
/// </summary>
internal sealed class TreeVisualizationConfig
{
    /// <summary>
    /// The flat items. The frontend derives the tree by grouping them on <see cref="GroupBy"/>.
    /// </summary>
    public required IReadOnlyList<TreeItem> Items { get; init; }

    /// <summary>
    /// The fields to group the items by, outermost first (e.g. model, topic, class).
    /// </summary>
    public required IReadOnlyList<TreeField> GroupBy { get; init; }
}
