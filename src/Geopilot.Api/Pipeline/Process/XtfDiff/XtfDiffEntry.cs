using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Pipeline.Process.XtfDiff;

/// <summary>
/// A single change record from the XTF-Diff-Tool JSON output. The tool writes a JSON array of these
/// records; see https://github.com/geowerkstatt/XTF-Diff-Tool/blob/main/OutputFileDescription.md.
/// </summary>
internal class XtfDiffEntry
{
    /// <summary>Value of <see cref="ChangeType"/> for newly added elements.</summary>
    public const string ChangeTypeAdded = "added";

    /// <summary>Value of <see cref="ChangeType"/> for modified elements.</summary>
    public const string ChangeTypeChanged = "changed";

    /// <summary>Value of <see cref="ChangeType"/> for removed elements.</summary>
    public const string ChangeTypeDeleted = "deleted";

    /// <summary>Value of <see cref="ValueType"/> for geometry changes, whose values are WKT strings.</summary>
    public const string ValueTypeGeometry = "geometry";

    /// <summary>
    /// The unique transfer id (TID) of the affected object.
    /// </summary>
    [JsonPropertyName("oid")]
    public string? Oid { get; set; }

    /// <summary>
    /// The kind of change: <see cref="ChangeTypeAdded"/>, <see cref="ChangeTypeChanged"/> or <see cref="ChangeTypeDeleted"/>.
    /// </summary>
    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    /// <summary>
    /// The kind of the changed property: "object", "reference", "attribute" or <see cref="ValueTypeGeometry"/>.
    /// </summary>
    [JsonPropertyName("valueType")]
    public string? ValueType { get; set; }

    /// <summary>
    /// The fully qualified name of the affected INTERLIS element.
    /// </summary>
    [JsonPropertyName("interlisName")]
    public string? InterlisName { get; set; }

    /// <summary>
    /// The path to the affected property (e.g. the attribute name). Null for object-level changes.
    /// </summary>
    [JsonPropertyName("attributePath")]
    public string? AttributePath { get; set; }

    /// <summary>
    /// The previous value; null for added elements. For geometry changes this is a WKT string
    /// (or an array of WKT strings), hence the loosely typed <see cref="JsonElement"/>.
    /// </summary>
    [JsonPropertyName("oldValue")]
    public JsonElement OldValue { get; set; }

    /// <summary>
    /// The current value; null for deleted elements. For geometry changes this is a WKT string
    /// (or an array of WKT strings), hence the loosely typed <see cref="JsonElement"/>.
    /// </summary>
    [JsonPropertyName("newValue")]
    public JsonElement NewValue { get; set; }
}
