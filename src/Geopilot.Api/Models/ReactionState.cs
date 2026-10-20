namespace Geopilot.Api.Models;

/// <summary>
/// The state of a <see cref="DeliveryReaction"/>. Deliberately not <c>StepState</c>: it describes forwarding a delivery,
/// not processing it.
/// </summary>
public enum ReactionState
{
    /// <summary>
    /// Waiting for its hold to end or for its next attempt.
    /// </summary>
    Pending,

    /// <summary>
    /// An instance runs an attempt.
    /// </summary>
    Running,

    /// <summary>
    /// Gave up after its last attempt, until an administrator resets it.
    /// </summary>
    Error,

    /// <summary>
    /// Succeeded.
    /// </summary>
    Done,
}
