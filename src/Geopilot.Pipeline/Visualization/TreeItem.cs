using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// A flat item of the error-tree visualization. The frontend builds the displayed hierarchy by grouping items
/// on the metadata keys named in <see cref="TreeVisualizationConfig.GroupBy"/> and derives the leaf's icon and
/// colour from its <see cref="Severity"/>.
/// </summary>
internal sealed class TreeItem
{
    /// <summary>
    /// Gets the stable id correlating this item with its map feature so the frontend can cross-select map and tree.
    /// <see langword="null"/> when the item has no correlated feature.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>
    /// Gets the text shown for this item's leaf node.
    /// </summary>
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    /// <summary>
    /// Gets the item's severity (<c>error</c> or <c>warning</c>); the frontend derives the leaf's icon and colour from it.
    /// </summary>
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    /// <summary>
    /// Gets the item's metadata. A value is either a plain <see cref="string"/> (data, e.g. a class name) or a
    /// <c>LocalizedText</c> (a label we generate, e.g. the error category) which serializes to a per-language object.
    /// The keys named in <see cref="TreeVisualizationConfig.GroupBy"/> reference entries of this dictionary.
    /// </summary>
    [JsonPropertyName("metadata")]
    public required IReadOnlyDictionary<string, object> Metadata { get; init; }
}
