using Geopilot.Pipeline.Config;

namespace Geopilot.Api.Models;

/// <summary>
/// A reaction to a <see cref="Delivery"/> once it can no longer be withdrawn (ADR 0015): a transfer that forwards it,
/// or a notification that reports on it. The rows are also the queue the instances claim their work from.
/// </summary>
public class DeliveryReaction
{
    /// <summary>
    /// The unique identifier for the reaction.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The delivery the reaction belongs to.
    /// </summary>
    public int DeliveryId { get; set; }

    /// <summary>
    /// Navigation to the delivery.
    /// </summary>
    public Delivery Delivery { get; set; } = null!;

    /// <summary>
    /// The id of the reaction in its set, unique within the delivery.
    /// </summary>
    public string ReactionId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the reaction forwards the delivery or reports on it.
    /// </summary>
    public ReactionKind Kind { get; set; }

    /// <summary>
    /// The fully qualified type name of the channel that runs the reaction.
    /// </summary>
    public string Implementation { get; set; } = string.Empty;

    /// <summary>
    /// The configuration of the channel as the set and the mandate defined it when the delivery was declared, stored
    /// as JSON. Never holds credentials: every attempt reads those from the base configuration.
    /// </summary>
    public ReactionSnapshot Config { get; set; } = new(new Parameterization(), []);

    /// <summary>
    /// The state of the reaction.
    /// </summary>
    public ReactionState State { get; set; }

    /// <summary>
    /// The number of attempts so far. An attempt counts as soon as an instance claims it.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// When the reaction is due (UTC). The first value is the hold, the end of the delete window.
    /// </summary>
    public DateTime NextAttemptAt { get; set; }

    /// <summary>
    /// Until when the running attempt holds the reaction (UTC). Once it passed, another instance takes the reaction over.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Identifies the running attempt. Only the instance holding it can finish the attempt.
    /// </summary>
    public Guid? ClaimToken { get; set; }

    /// <summary>
    /// When the last attempt started (UTC).
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// When the reaction reached <see cref="ReactionState.Done"/> or <see cref="ReactionState.Error"/> (UTC).
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// What the last attempt reported, for the operators: why it failed, or what it did.
    /// </summary>
    public string? Message { get; set; }
}
