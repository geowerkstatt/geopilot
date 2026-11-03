using Geopilot.Api.Models;

namespace Geopilot.Api.Reactions;

/// <summary>
/// Reads the state of deliveries from their reactions. A delivery counts as forwarded once all its transfers are
/// <see cref="ReactionState.Done"/>, and right away when it has none (ADR 0015). Notifications do not count. Only a
/// forwarded delivery gets its notifications and appears in the STAC catalog.
/// </summary>
internal static class ReactionExtensions
{
    // What holds up the reactions of a delivery the most comes first.
    private static readonly ReactionState[] Precedence = [ReactionState.Error, ReactionState.Running, ReactionState.Pending];

    /// <summary>
    /// The ids of the deliveries that are not forwarded yet, as a subquery to compose into another query.
    /// </summary>
    internal static IQueryable<int> UnforwardedDeliveryIds(this IQueryable<DeliveryReaction> reactions)
        => reactions.Where(r => r.Kind == ReactionKind.Transfer && r.State != ReactionState.Done).Select(r => r.DeliveryId);

    /// <summary>
    /// The ids of the deliveries one of whose reactions has begun: it had an attempt or no longer waits. Their deliverer
    /// can no longer withdraw them, as a subquery to compose into another query.
    /// </summary>
    internal static IQueryable<int> DeliveryIdsWithStartedReactions(this IQueryable<DeliveryReaction> reactions)
        => reactions.Where(r => r.Attempts > 0 || r.State != ReactionState.Pending).Select(r => r.DeliveryId);

    /// <summary>
    /// Sums up the states of the reactions of one kind: <see cref="ReactionState.Error"/> before
    /// <see cref="ReactionState.Running"/> before <see cref="ReactionState.Pending"/> before
    /// <see cref="ReactionState.Done"/>, which they only are once every one is done. For the transfers of a delivery this
    /// is the state of its forwarding.
    /// </summary>
    /// <returns>The summed up state, or <see langword="null"/> without reactions. A delivery without transfers counts as
    /// forwarded right away, but there is no forwarding to follow.</returns>
    internal static ReactionState? SummedUp(this IEnumerable<ReactionState> reactionStates)
    {
        var states = reactionStates.ToHashSet();
        if (states.Count == 0)
            return null;

        return Precedence.FirstOrDefault(states.Contains, ReactionState.Done);
    }
}
