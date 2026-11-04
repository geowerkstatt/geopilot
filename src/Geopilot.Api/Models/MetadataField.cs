using Geopilot.PipelineCore.Pipeline;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Geopilot.Api.Models;

/// <summary>
/// The definition of an additional field a deliverer fills in for a mandate. The <paramref name="Id"/> is the key
/// of the value in a delivery. <paramref name="Options"/> is set only for <see cref="MetadataFieldType.Selection"/>.
/// <paramref name="Evaluation"/> is <see cref="FieldEvaluationType.Optional"/> or <see cref="FieldEvaluationType.Required"/>.
/// </summary>
public record MetadataField(
    string Id,
    LocalizedText Title,
    MetadataFieldType Type,
    FieldEvaluationType Evaluation,
    IReadOnlyList<MetadataFieldOption>? Options = null);

/// <summary>
/// One option of a <see cref="MetadataFieldType.Selection"/> field. The <paramref name="Id"/> is what a delivery
/// carries as value, the <paramref name="Title"/> is for display.
/// </summary>
public sealed record MetadataFieldOption(string Id, LocalizedText Title);

/// <summary>
/// The kind of value a <see cref="MetadataField"/> takes. Serialized as camelCase: <c>text</c>, <c>textList</c>,
/// <c>selection</c>, <c>integer</c>, <c>decimal</c>, <c>date</c>.
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The member names give the wire names integer and decimal.")]
public enum MetadataFieldType
{
    /// <summary>A single line of text.</summary>
    Text,

    /// <summary>A list of text entries.</summary>
    TextList,

    /// <summary>One option out of <see cref="MetadataField.Options"/>.</summary>
    Selection,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>A decimal number.</summary>
    Decimal,

    /// <summary>A calendar date.</summary>
    Date,
}

/// <summary>
/// The copy of a <see cref="MetadataField"/> that a delivery keeps, together with the value that was delivered.
/// <paramref name="Value"/> is <see langword="null"/> for a field that was not filled in.
/// </summary>
public sealed record DeliveredMetadataField(
    string Id,
    LocalizedText Title,
    MetadataFieldType Type,
    FieldEvaluationType Evaluation,
    IReadOnlyList<MetadataFieldOption>? Options,
    JsonElement? Value)
    : MetadataField(Id, Title, Type, Evaluation, Options);
