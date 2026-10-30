using Geopilot.Api.Models;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// A delivery as the administration lists it, with how far its reactions got. Of the mandate it carries no more than
/// its id and name.
/// </summary>
/// <param name="Id">The unique identifier of the delivery.</param>
/// <param name="Date">The date the delivery was declared.</param>
/// <param name="Mandate">The mandate the delivery fulfils.</param>
/// <param name="DeclarerName">Who declared the delivery, a person or a machine client.</param>
/// <param name="Comment">The comment that came with the delivery.</param>
/// <param name="ForwardingState">The states of its transfers summed up: <see cref="ReactionState.Error"/> before
/// <see cref="ReactionState.Running"/> before <see cref="ReactionState.Pending"/> before <see cref="ReactionState.Done"/>,
/// which means the delivery is forwarded. <see langword="null"/> for a delivery without transfers, which counts as
/// forwarded right away.</param>
/// <param name="Reactions">The reactions of the delivery, in the order they were created.</param>
public record DeliveryResponse(int Id, DateTime Date, MandateReference Mandate, string DeclarerName, string Comment, ReactionState? ForwardingState, IReadOnlyList<DeliveryReactionResponse> Reactions);

/// <summary>
/// How far a reaction to a delivery got.
/// </summary>
/// <param name="ReactionId">The id of the reaction in the reaction set of the mandate.</param>
/// <param name="DisplayName">The name of the reaction in the pipeline definition as it is now, or
/// <see langword="null"/> when the definition no longer has the reaction.</param>
/// <param name="Kind">Whether the reaction forwards the delivery or reports on it.</param>
/// <param name="State">The state of the reaction.</param>
/// <param name="Attempts">How many attempts the reaction has started. A reaction still on hold has none.</param>
/// <param name="NextAttemptAt">When its next attempt is due, set only while it is <see cref="ReactionState.Pending"/>.</param>
/// <param name="LastAttemptAt">When its last attempt started.</param>
/// <param name="FinishedAt">When it ended as <see cref="ReactionState.Done"/>, <see cref="ReactionState.Error"/> or
/// <see cref="ReactionState.Deleted"/>.</param>
/// <param name="Message">What its channel last reported, or why its last attempt failed.</param>
public record DeliveryReactionResponse(string ReactionId, LocalizedText? DisplayName, ReactionKind Kind, ReactionState State, int Attempts, DateTime? NextAttemptAt, DateTime? LastAttemptAt, DateTime? FinishedAt, string? Message);
