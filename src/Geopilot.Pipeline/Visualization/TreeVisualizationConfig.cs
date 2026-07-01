using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// The config for the built-in tree visualization: a flat list of <see cref="Items"/> plus the metadata keys
/// (<see cref="GroupBy"/>) the frontend groups them by to build the displayed hierarchy. The composite root
/// carries the filter keys, since the filter also applies to the map. Serialized to JSON and rendered by the
/// frontend tree component.
/// </summary>
internal sealed class TreeVisualizationConfig
{
    /// <summary>
    /// The flat items. The frontend derives the tree by grouping them on <see cref="GroupBy"/>.
    /// </summary>
    [JsonPropertyName("items")]
    public required IReadOnlyList<TreeItem> Items { get; init; }

    /// <summary>
    /// The metadata keys to group the items by, outermost first (e.g. <c>["Model", "Topic", "Class"]</c>).
    /// </summary>
    [JsonPropertyName("groupBy")]
    public required IReadOnlyList<string> GroupBy { get; init; }
}
