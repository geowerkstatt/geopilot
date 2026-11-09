using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Channels;

/// <summary>
/// A metadata field the declarer filled in, as a channel sees it. Exactly one of <see cref="Texts"/>,
/// <see cref="Option"/>, <see cref="Number"/> and <see cref="Date"/> carries the value.
/// </summary>
internal sealed record MetadataInfo
{
    /// <summary>
    /// The title of the field.
    /// </summary>
    public required LocalizedText Title { get; init; }

    /// <summary>
    /// The value of a text field (one entry) or a text list field (one entry per item).
    /// </summary>
    public IReadOnlyList<string> Texts { get; init; } = [];

    /// <summary>
    /// The title of the chosen option of a selection field.
    /// </summary>
    public LocalizedText? Option { get; init; }

    /// <summary>
    /// The value of a whole number or decimal number field.
    /// </summary>
    public decimal? Number { get; init; }

    /// <summary>
    /// The value of a date field.
    /// </summary>
    public DateOnly? Date { get; init; }
}
