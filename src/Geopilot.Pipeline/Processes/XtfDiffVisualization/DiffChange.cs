namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

/// <summary>
/// One entry of the diff as the list of the visualization shows it: the change of one property of one
/// object, or an object added or deleted as a whole.
/// </summary>
internal sealed record DiffChange
{
    /// <summary>
    /// Gets the id of the change, unique within the visualization.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the transfer id of the changed object, which is also the id of its features on the map.
    /// </summary>
    public required string ObjectId { get; init; }

    /// <summary>
    /// Gets whether the value was added, deleted or changed.
    /// </summary>
    public required DiffChangeType ChangeType { get; init; }

    /// <summary>
    /// Gets what kind of property changed.
    /// </summary>
    public required DiffValueType ValueType { get; init; }

    /// <summary>
    /// Gets the qualified INTERLIS name of the class of the object, for example <c>Model.Topic.Class</c>.
    /// </summary>
    public required string InterlisName { get; init; }

    /// <summary>
    /// Gets the path of the changed property, or <see langword="null"/> when the object was added or deleted
    /// as a whole.
    /// </summary>
    public string? AttributePath { get; init; }

    /// <summary>
    /// Gets the value in the old state as text, lists and structures as compact JSON, or
    /// <see langword="null"/> when the old state has none.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// Gets the value in the new state as text, lists and structures as compact JSON, or
    /// <see langword="null"/> when the new state has none.
    /// </summary>
    public string? NewValue { get; init; }

    /// <summary>
    /// Gets the location in the old state as WKT, or <see langword="null"/> without one. The table carries it
    /// only for a geometry change when the map is included.
    /// </summary>
    public string? OldLocation { get; init; }

    /// <summary>
    /// Gets the location in the new state as WKT, or <see langword="null"/> without one. The table carries it
    /// only for a geometry change when the map is included.
    /// </summary>
    public string? NewLocation { get; init; }
}

/// <summary>
/// Whether a value was added, deleted or changed, as the XTF-Diff-Tool reports it in <c>changeType</c>.
/// </summary>
internal enum DiffChangeType
{
    /// <summary>
    /// The value exists in the new state only.
    /// </summary>
    Added,

    /// <summary>
    /// The value exists in the old state only.
    /// </summary>
    Deleted,

    /// <summary>
    /// The value exists in both states and differs.
    /// </summary>
    Changed,
}

/// <summary>
/// The kind of property that changed, as the XTF-Diff-Tool reports it in <c>valueType</c>.
/// </summary>
internal enum DiffValueType
{
    /// <summary>
    /// The whole object.
    /// </summary>
    Object,

    /// <summary>
    /// A reference to another object.
    /// </summary>
    Reference,

    /// <summary>
    /// An attribute that is not a geometry, including structures.
    /// </summary>
    Attribute,

    /// <summary>
    /// A geometry attribute.
    /// </summary>
    Geometry,
}
