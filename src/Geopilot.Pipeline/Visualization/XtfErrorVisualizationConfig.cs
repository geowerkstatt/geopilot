namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// Composite payload of the built-in XTF error visualization: an optional <see cref="Map"/> and an
/// optional <see cref="Tree"/> view of the same validation errors, wrapped in one envelope so the
/// frontend renders (and, in a later iteration, cross-links) them in a single component.
/// </summary>
internal sealed record XtfErrorVisualizationConfig
{
    /// <summary>Gets the map view, or <see langword="null"/> when the map is not included.</summary>
    public MapVisualizationConfig? Map { get; init; }

    /// <summary>Gets the error-tree view, or <see langword="null"/> when the tree is not included.</summary>
    public TreeVisualizationConfig? Tree { get; init; }

    /// <summary>
    /// Gets the fields the frontend offers as filters, in display order (e.g. model, topic, class, error type),
    /// or <see langword="null"/> when there is no tree. The filter applies to both the map and the tree, so it
    /// lives on the composite root. Empty means no filters.
    /// </summary>
    public IReadOnlyList<TreeField>? FilterBy { get; init; }
}
