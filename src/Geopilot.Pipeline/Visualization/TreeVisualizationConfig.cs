using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// The self-describing config for the built-in tree visualization: a <c>type</c> discriminator plus the
/// root <see cref="TreeNode"/>s. Serialized to JSON and rendered by the frontend tree component.
/// </summary>
internal sealed class TreeVisualizationConfig
{
    /// <summary>
    /// The root nodes of the tree, rendered in order.
    /// </summary>
    [JsonPropertyName("nodes")]
    public required IReadOnlyList<TreeNode> Nodes { get; init; }
}
