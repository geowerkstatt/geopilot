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
            return new PipelineRunOutcome(kind, delivery.Id, delivery.ForwardingState, []);
        }

        if (run.TerminalState is null or ProcessingState.Pending or ProcessingState.Running)
            return Outcome(isRunning ? PipelineRunOutcomeKind.Running : PipelineRunOutcomeKind.Unknown);

        if (run.TerminalState is ProcessingState.Success or ProcessingState.Warning)
        {
            // shortcut: whoever may not deliver (not a member, or a mandate that allows no delivery) and a machine
            // delivery whose declaration failed show as not delivered until the protocol records IsDeclarable and
            // DeclarationFailure.
            var anonymous = run.UserId is null && run.MachineClientId is null;
            return Outcome(anonymous ? PipelineRunOutcomeKind.NotDeclarable : PipelineRunOutcomeKind.NotDelivered);
        }

        // A restriction does not stop the pipeline, so later steps can restrict the delivery as well, or fail.
        var blockingSteps = run.Steps
            .Where(step => BlockingStepStates.Contains(step.State))
            .OrderBy(step => step.Order)
            .Select(step => new PipelineRunBlockingStep(step.StepId, step.DisplayName))
            .ToList();
        return new PipelineRunOutcome(PipelineRunOutcomeKind.NotDeliverable, null, null, blockingSteps);
    }

    /// <summary>
    /// Loads the deliveries declared for the jobs with the specified <paramref name="jobIds"/>, one per job: a live
    /// delivery before a deleted one.
    /// </summary>
    internal static async Task<Dictionary<Guid, PipelineRunDelivery>> LoadDeliveriesAsync(Context context, IReadOnlyCollection<Guid> jobIds)
    {
        // The unique index on the job ignores deleted deliveries, so a job can have a deleted and a live one.
        var deliveries = await context.Deliveries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => jobIds.Contains(d.JobId))
            .Select(d => new { d.Id, d.JobId, d.Deleted })
            .ToListAsync();

        var liveIds = deliveries.Where(d => !d.Deleted).Select(d => d.Id).ToList();
        var transferStates = await context.DeliveryReactions
            .AsNoTracking()
            .Where(r => liveIds.Contains(r.DeliveryId) && r.Kind == ReactionKind.Transfer)
            .Select(r => new { r.DeliveryId, r.State })
            .ToListAsync();

        return deliveries
            .GroupBy(d => d.JobId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var delivery = group.OrderBy(d => d.Deleted).ThenByDescending(d => d.Id).First();
                    var forwardingState = delivery.Deleted
                        ? null
                        : transferStates.Where(r => r.DeliveryId == delivery.Id).Select(r => r.State).SummedUp();
                    return new PipelineRunDelivery(delivery.Id, delivery.Deleted, forwardingState);
                });
    }

    private static PipelineRunOutcome Outcome(PipelineRunOutcomeKind kind) => new(kind, null, null, []);
}

/// <summary>
/// The delivery declared for a pipeline run, as far as the outcome of the run needs it.
/// </summary>
/// <param name="Id">The id of the delivery.</param>
/// <param name="Deleted">Whether the delivery was deleted.</param>
/// <param name="ForwardingState">The summed up state of its transfers, <see langword="null"/> without transfers or once deleted.</param>
internal sealed record PipelineRunDelivery(int Id, bool Deleted, ReactionState? ForwardingState);
