using Geopilot.Api.Models;

namespace Geopilot.Api.Reactions;

/// <summary>
/// When a delivery counts as forwarded: once all its transfers are <see cref="ReactionState.Done"/>, and right away
/// when it has none (ADR 0015). Notifications do not count. Only a forwarded delivery gets its notifications and
/// appears in the STAC catalog.
/// </summary>
internal static class ForwardingExtensions
{
    // What holds up the forwarding of a delivery the most comes first.
    private static readonly ReactionState[] Precedence = [ReactionState.Error, ReactionState.Running, ReactionState.Pending];

    /// <summary>
    /// The ids of the deliveries that are not forwarded yet, as a subquery to compose into another query.
    /// </summary>
    internal static IQueryable<int> UnforwardedDeliveryIds(this IQueryable<DeliveryReaction> reactions)
        => reactions.Where(r => r.Kind == ReactionKind.Transfer && r.State != ReactionState.Done).Select(r => r.DeliveryId);

    /// <summary>
    /// Sums up the states of the transfers of a delivery to the state of its forwarding: <see cref="ReactionState.Error"/>
    /// before <see cref="ReactionState.Running"/> before <see cref="ReactionState.Pending"/> before
    /// <see cref="ReactionState.Done"/>, which it only is once every transfer is done.
    /// </summary>
    /// <returns>The state of the forwarding, or <see langword="null"/> for a delivery without transfers: it counts as
    /// forwarded right away, but there is no forwarding to follow.</returns>
    internal static ReactionState? ForwardingState(this IEnumerable<ReactionState> transferStates)
    {
        var states = transferStates.ToHashSet();
        if (states.Count == 0)
            return null;

        return Precedence.FirstOrDefault(states.Contains, ReactionState.Done);
    }
}
