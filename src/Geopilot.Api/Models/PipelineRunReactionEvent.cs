namespace Geopilot.Api.Models;

/// <summary>
/// Something that happened to a reaction of the delivery a run produced: an attempt through its channel. Kept in the
/// protocol of the run, so how a delivery was forwarded stays answerable after its reactions are gone. Holds no personal
/// data of the declarer and no values of the mandate.
/// </summary>
public class PipelineRunReactionEvent
{
    /// <summary>
    /// The unique identifier for the event record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The run whose delivery the reaction belongs to.
    /// </summary>
    public int PipelineRunId { get; set; }

    /// <summary>
    /// The id of the delivery the reaction belongs to. A plain value rather than a reference: deliveries are filtered
    /// once deleted, and the protocol has to stay readable after that.
    /// </summary>
    public int DeliveryId { get; set; }

    /// <summary>
    /// The id of the reaction in its set.
    /// </summary>
    public string ReactionId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the reaction forwards the delivery or reports on it.
    /// </summary>
    public ReactionKind ReactionKind { get; set; }

    /// <summary>
    /// The fully qualified type name of the channel that runs the reaction.
    /// </summary>
    public string Implementation { get; set; } = string.Empty;

    /// <summary>
    /// What happened.
    /// </summary>
    public ReactionEvent Event { get; set; }

    /// <summary>
    /// The number of the attempt, for an <see cref="ReactionEvent.Attempt"/>.
    /// </summary>
    public int? Attempt { get; set; }

    /// <summary>
    /// When it happened (UTC); for an attempt, when it started.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// When the attempt reported back (UTC). <see langword="null"/> while it runs, and for good when its instance
    /// stopped before it could report.
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// The state of the reaction afterwards: <see cref="ReactionState.Running"/> while an attempt runs, then the state
    /// it left. <see langword="null"/> when the attempt no longer held its reaction once it reported; the message
    /// says what the attempt did.
    /// </summary>
    public ReactionState? State { get; set; }

    /// <summary>
    /// What the channel reported, or why the attempt failed.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The administrator who caused the event, or <see langword="null"/> when the application did.
    /// </summary>
    public int? UserId { get; set; }
}
