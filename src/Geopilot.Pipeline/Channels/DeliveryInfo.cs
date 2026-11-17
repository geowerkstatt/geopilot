namespace Geopilot.Pipeline.Channels;

/// <summary>
/// What a channel learns about the delivery it forwards, read anew for every attempt. It names the supplier and so
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
    /// The id of the reaction the attempt belongs to. It stays the same in every attempt, so a channel can mark what
    /// it sends as one and the same thing.
    /// </summary>
    public required string ReactionId { get; init; }

    /// <summary>
    /// When the delivery was created.
    /// </summary>
    public required DateTimeOffset DeliveredAt { get; init; }

    /// <summary>
    /// Whether the supplier marked the delivery as partial, or <see langword="null"/> when the mandate does not ask.
    /// </summary>
    public bool? Partial { get; init; }

    /// <summary>
    /// The id of the delivery this one follows, or <see langword="null"/> when the supplier named none.
    /// </summary>
    public int? PrecursorDeliveryId { get; init; }

    /// <summary>
    /// The comment of the supplier, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// The mandate the delivery fulfils.
    /// </summary>
    public required MandateInfo Mandate { get; init; }

    /// <summary>
    /// Who delivered.
    /// </summary>
    public required SupplierInfo Supplier { get; init; }

    /// <summary>
    /// The metadata fields the supplier filled in, in the order of the mandate; fields left empty are not listed.
    /// </summary>
    public IReadOnlyList<MetadataInfo> Metadata { get; init; } = [];
}
