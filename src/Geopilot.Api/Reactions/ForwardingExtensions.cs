using Geopilot.Api.Models;

namespace Geopilot.Api.Reactions;

/// <summary>
/// When a delivery counts as forwarded: once all its transfers are <see cref="ReactionState.Done"/>, and right away
/// when it has none (ADR 0015). Notifications do not count. Only a forwarded delivery gets its notifications and
/// appears in the STAC catalog.
/// </summary>
internal static class ForwardingExtensions
{
    /// <summary>
    /// The ids of the deliveries that are not forwarded yet, as a subquery to compose into another query.
    /// </summary>
    internal static IQueryable<int> UnforwardedDeliveryIds(this IQueryable<DeliveryReaction> reactions)
        => reactions.Where(r => r.Kind == ReactionKind.Transfer && r.State != ReactionState.Done).Select(r => r.DeliveryId);
}
