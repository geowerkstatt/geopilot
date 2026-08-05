using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// A flat item of the error-tree visualization: one validation error or warning with explicit, typed fields.
/// The frontend builds the displayed hierarchy by grouping items on the <see cref="TreeField"/>s named in
/// <see cref="TreeVisualizationConfig.GroupBy"/>, derives the leaf's icon and colour from <see cref="Severity"/>
/// and shows <see cref="Tid"/> (falling back to <see cref="Message"/>) as the leaf text.
/// </summary>
internal sealed class TreeItem
{
    /// <summary>
    /// Gets the stable id correlating this item with its map feature so the frontend can cross-select map and tree.
    /// <see langword="null"/> when the item has no correlated feature.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the item's severity (<c>error</c> or <c>warning</c>); the frontend derives the leaf's icon and colour from it.
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Gets the classified error category as a localized label, or <see langword="null"/> when the message
    /// matches no known category.
    /// </summary>
    public LocalizedText? ErrorType { get; init; }

    /// <summary>
    /// Gets the TID of the failing object, or <see langword="null"/> when the log entry carries none.
    /// </summary>
    public string? Tid { get; init; }

    /// <summary>
    /// Gets the INTERLIS model of the failing object. <see cref="Model"/>, <see cref="Topic"/> and
    /// <see cref="Class"/> are either all set or all <see langword="null"/>.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the INTERLIS topic of the failing object.
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Gets the INTERLIS class of the failing object.
    /// </summary>
    public string? Class { get; init; }

    /// <summary>
    /// Gets the validator message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the line number in the validated file, or <see langword="null"/> when the log entry carries none.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets the error's coordinates as an invariant <c>"C1, C2"</c> display string, or <see langword="null"/>
    /// when the log entry carries no geometry.
    /// </summary>
    public string? Coordinates { get; init; }
}
