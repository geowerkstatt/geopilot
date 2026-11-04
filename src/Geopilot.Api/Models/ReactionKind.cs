namespace Geopilot.Api.Models;

/// <summary>
/// Whether a <see cref="DeliveryReaction"/> forwards the delivery or only reports on it.
/// </summary>
public enum ReactionKind
{
    /// <summary>
    /// Forwards the delivery once its hold ended. The delivery counts as forwarded when all its transfers are done.
    /// </summary>
    Transfer,

    /// <summary>
    /// Reports on the delivery once it is forwarded, and never changes its state.
    /// </summary>
    Notification,
}
