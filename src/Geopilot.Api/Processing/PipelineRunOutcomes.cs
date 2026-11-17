using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Reactions;
using Geopilot.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Processing;

/// <summary>
/// Derives what became of a pipeline run, for display only: no execution path decides anything from the protocol
/// (ADR 0010).
/// </summary>
internal static class PipelineRunOutcomes
{
    private static readonly StepState[] BlockingStepStates = [StepState.DeliveryRestriction, StepState.Error, StepState.Cancelled];

    /// <summary>
    /// Derives what became of the <paramref name="run"/>.
    /// </summary>
    /// <param name="run">The run with its steps.</param>
    /// <param name="isRunning">Whether the job of the run is still pending or running in this instance.</param>
    /// <param name="delivery">The delivery declared for the run, live before deleted, or <see langword="null"/>.</param>
    internal static PipelineRunOutcome Determine(PipelineRun run, bool isRunning, PipelineRunDelivery? delivery)
    {
        if (delivery is not null)
        {
            var kind = delivery.Deleted ? PipelineRunOutcomeKind.DeliveredAndDeleted : PipelineRunOutcomeKind.Delivered;
            return new PipelineRunOutcome(kind, delivery.Id, [], PreflightFailed: false);
        }

        if (run.TerminalState is null or ProcessingState.Pending or ProcessingState.Running)
            return Outcome(isRunning ? PipelineRunOutcomeKind.Running : PipelineRunOutcomeKind.Unknown);

        if (run.TerminalState is ProcessingState.Success or ProcessingState.Warning)
        {
            if (run.DeclarationFailure is not null)
                return Outcome(PipelineRunOutcomeKind.DeclarationFailed);

            // A run recorded before the protocol kept its declarability has none; only an anonymous one surely was not.
            var declarable = run.Declarability is { } declarability
                ? declarability == Declarability.Declarable
                : run.UserId is not null || run.MachineClientId is not null;
            return Outcome(declarable ? PipelineRunOutcomeKind.NotDelivered : PipelineRunOutcomeKind.NotDeclarable);
        }

        // A restriction does not stop the pipeline, so later steps can restrict the delivery as well, or fail.
        var blockingSteps = run.Steps
            .Where(step => BlockingStepStates.Contains(step.State))
            .OrderBy(step => step.Order)
            .Select(step => new PipelineRunBlockingStep(step.StepId, step.DisplayName))
            .ToList();

        // Only a rejected preflight fails a run before its first step; every step is pending from the job's start.
        var preflightFailed = run.TerminalState == ProcessingState.Failed && run.Steps.All(step => step.State == StepState.Pending);
        return new PipelineRunOutcome(PipelineRunOutcomeKind.NotDeliverable, null, blockingSteps, preflightFailed);
    }

    /// <summary>
    /// Loads the deliveries declared for the jobs with the specified <paramref name="jobIds"/>, one per job: a live
    /// delivery before a deleted one.
    /// </summary>
    internal static async Task<Dictionary<Guid, PipelineRunDelivery>> LoadDeliveriesAsync(Context context, IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken)
    {
        // The unique index on the job ignores deleted deliveries, so a job can have a deleted and a live one.
        var deliveries = await context.Deliveries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => jobIds.Contains(d.JobId))
            .Select(d => new { d.Id, d.JobId, d.Deleted })
            .ToListAsync(cancellationToken);

        return deliveries
            .GroupBy(d => d.JobId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var delivery = group.OrderBy(d => d.Deleted).ThenByDescending(d => d.Id).First();
                    return new PipelineRunDelivery(delivery.Id, delivery.Deleted);
                });
    }

    /// <summary>
    /// Loads the summed up states of the transfers and of the notifications of the <paramref name="delivery"/>, each
    /// <see langword="null"/> without delivery, without reactions of its kind or once the delivery is deleted.
    /// </summary>
    internal static async Task<(ReactionState? Forwarding, ReactionState? Notification)> LoadReactionStatesAsync(Context context, PipelineRunDelivery? delivery, CancellationToken cancellationToken)
    {
        if (delivery is null || delivery.Deleted)
            return (null, null);

        var reactions = await context.DeliveryReactions
            .AsNoTracking()
            .Where(r => r.DeliveryId == delivery.Id)
            .Select(r => new { r.Kind, r.State })
            .ToListAsync(cancellationToken);
        return (
            reactions.Where(r => r.Kind == ReactionKind.Transfer).Select(r => r.State).SummedUp(),
            reactions.Where(r => r.Kind == ReactionKind.Notification).Select(r => r.State).SummedUp());
    }

    private static PipelineRunOutcome Outcome(PipelineRunOutcomeKind kind) => new(kind, null, [], PreflightFailed: false);
}

/// <summary>
/// The delivery declared for a pipeline run, as far as the outcome of the run needs it.
/// </summary>
/// <param name="Id">The id of the delivery.</param>
/// <param name="Deleted">Whether the delivery was deleted.</param>
internal sealed record PipelineRunDelivery(int Id, bool Deleted);
