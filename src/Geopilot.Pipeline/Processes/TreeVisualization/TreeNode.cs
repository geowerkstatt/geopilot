using System.Diagnostics;

namespace Geopilot.Pipeline.Processes.TreeVisualization;

/// <summary>
/// A node in a hierarchical message tree rendered by the frontend tree visualization.
/// </summary>
[DebuggerDisplay("Message = {Message}, Icon = {Icon}, Children = {Values.Count}")]
public class TreeNode
{
    /// <summary>
    /// Gets or sets the message text shown for this node.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the Material Icons ligature shown next to the message (e.g. <c>error_outline</c>).
    /// When <see langword="null"/>, no icon is rendered.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the MUI palette color used to tint the icon
    /// (e.g. <c>error</c>, <c>warning</c>, <c>success</c>, <c>info</c>, <c>primary</c>, <c>inherit</c>).
    /// When <see langword="null"/>, the frontend default color is used.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the child nodes nested under this node.
    /// </summary>
    public required IList<TreeNode> Values { get; set; }

    /// <summary>
    /// Gets or sets the key/value details shown by the frontend metadata table when this node is selected.
    /// When <see langword="null"/>, the node has no metadata.
    /// </summary>
    public IDictionary<string, string>? Metadata { get; set; }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is not TreeNode other)
            return false;

        return Message == other.Message
            && Icon == other.Icon
            && Color == other.Color
            && MetadataEquals(Metadata, other.Metadata)
            && Values.SequenceEqual(other.Values);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Message, Icon, Color, Values);

    /// <summary>
    /// Compares two metadata dictionaries for equality, independent of key order.
    /// </summary>
    private static bool MetadataEquals(IDictionary<string, string>? first, IDictionary<string, string>? second)
    {
        if (first is null || second is null)
            return first is null && second is null;

        if (first.Count != second.Count)
            return false;

        return first.All(pair => second.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }
}
