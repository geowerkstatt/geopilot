namespace Geopilot.Pipeline.Channels;

/// <summary>
/// What a channel learns about the delivery it forwards, read anew for every attempt. It names the declarer and so
/// carries personal data (ADR 0015): a channel passes on only what its target needs, and its documentation says what.
/// </summary>
internal sealed record DeliveryInfo
{
    /// <summary>
    /// The id of the delivery.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The id of the processing job the delivery comes from.
    /// </summary>
    public required Guid JobId { get; init; }

    /// <summary>
    /// When the delivery was declared.
    /// </summary>
    public required DateTimeOffset DeclaredAt { get; init; }

    /// <summary>
    /// Whether the declarer marked the delivery as partial, or <see langword="null"/> when the mandate does not ask.
    /// </summary>
    public bool? Partial { get; init; }

    /// <summary>
    /// The id of the delivery this one follows, or <see langword="null"/> when the declarer named none.
    /// </summary>
    public int? PrecursorDeliveryId { get; init; }

    /// <summary>
    /// The comment of the declarer, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// The mandate the delivery fulfils.
    /// </summary>
    public required MandateInfo Mandate { get; init; }

    /// <summary>
    /// Who declared the delivery.
    /// </summary>
    public required DeclarerInfo Declarer { get; init; }
}
