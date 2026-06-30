using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// Composite payload of the built-in XTF error visualization: an optional <see cref="Map"/> and an
/// optional <see cref="Tree"/> view of the same validation errors, wrapped in one envelope so the
/// frontend renders (and, in a later iteration, cross-links) them in a single component.
/// </summary>
internal sealed record XtfErrorVisualizationConfig
{
    /// <summary>Gets the map view, or <see langword="null"/> when the map is not included.</summary>
    [JsonPropertyName("map")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MapVisualizationConfig? Map { get; init; }

    /// <summary>Gets the error-tree view, or <see langword="null"/> when the tree is not included.</summary>
    [JsonPropertyName("tree")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TreeVisualizationConfig? Tree { get; init; }
}
