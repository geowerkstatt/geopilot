using Geopilot.Api.Models;

namespace Geopilot.Api.Contracts;

/// <summary>
/// A delivery as the administration shows it on a page of its own: what was delivered and how far its reactions got.
/// </summary>
/// <param name="Id">The unique identifier of the delivery.</param>
/// <param name="Date">The date the delivery was declared.</param>
/// <param name="Mandate">The mandate the delivery fulfils.</param>
/// <param name="DeclarerName">Who declared the delivery, a person or a machine client.</param>
/// <param name="DeclaringUserId">The user who declared the delivery, <see langword="null"/> when a machine client did.</param>
/// <param name="DeclaringClientId">The machine client that declared the delivery, <see langword="null"/> when a user did.</param>
/// <param name="PrecursorDelivery">The delivery this one follows up, if any.</param>
/// <param name="Partial">Whether the delivery holds partial data, <see langword="null"/> when the mandate does not ask.</param>
/// <param name="JobId">The job the delivery comes from.</param>
/// <param name="Files">Its files. Like the STAC item, they can be downloaded only once the delivery is forwarded.</param>
/// <param name="Comment">The comment that came with the delivery.</param>
/// <param name="Metadata">The copy of the mandate's metadata fields taken at declaration, with the delivered values.</param>
/// <param name="StacItem">Where the delivery is in the STAC catalog, <see langword="null"/> until it is forwarded.</param>
/// <param name="Reactions">The reactions of the delivery, in the order they were created.</param>
public record DeliveryDetailResponse(int Id, DateTime Date, MandateReference Mandate, string DeclarerName, int? DeclaringUserId, int? DeclaringClientId, DeliverySummary? PrecursorDelivery, bool? Partial, Guid JobId, IReadOnlyList<AssetReference> Files, string Comment, IReadOnlyList<DeliveredMetadataField> Metadata, StacItemReference? StacItem, IReadOnlyList<DeliveryReactionResponse> Reactions);

/// <summary>
/// A file of a delivery.
/// </summary>
/// <param name="Id">The id to download the file with.</param>
/// <param name="FileName">The original name of the file.</param>
public record AssetReference(int Id, string FileName);

/// <summary>
/// An item in the STAC catalog.
/// </summary>
/// <param name="CollectionId">The collection of the item, one per mandate.</param>
/// <param name="ItemId">The item itself, one per delivery.</param>
public record StacItemReference(string CollectionId, string ItemId);
