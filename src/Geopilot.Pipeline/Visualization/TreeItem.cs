using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// A flat item of a tree visualization. The frontend builds the displayed hierarchy by grouping items on the
/// metadata keys named in <see cref="TreeVisualizationConfig.GroupBy"/>. The format is generic: it carries no
/// domain-specific fields, only a display label and arbitrary metadata, so other visualizations can reuse it.
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
    /// Gets the Material Icons ligature shown next to the leaf (e.g. <c>error_outline</c>), or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the MUI palette color tinting the icon (e.g. <c>error</c>, <c>warning</c>), or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; init; }

    /// <summary>
    /// Gets the item's metadata. A value is either a plain <see cref="string"/> (data, e.g. a class name) or a
    /// <c>LocalizedText</c> (a label we generate, e.g. the error category) which serializes to a per-language object.
    /// The keys named in <see cref="TreeVisualizationConfig.GroupBy"/> reference entries of this dictionary.
    /// </summary>
    [JsonPropertyName("metadata")]
    public required IReadOnlyDictionary<string, object> Metadata { get; init; }
}
