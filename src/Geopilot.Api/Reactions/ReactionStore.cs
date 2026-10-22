using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Reactions;

/// <summary>
/// The <see cref="DeliveryReaction"/> table used as a work queue: an instance claims a due reaction, runs one attempt
/// and reports how it went. Every write is a conditional update of a single row, so concurrent instances never run
/// the same attempt twice, and every time comes from the database clock, never from an instance.
/// </summary>
public class ReactionStore
{
    /// <summary>
    /// How long an attempt holds its reaction beyond the time limit, so the outcome of an attempt that used up its
    /// time limit still reaches the database before another instance takes the reaction over.
    /// </summary>
    internal static readonly TimeSpan LockMargin = TimeSpan.FromMinutes(1);

    // Each lost claim means another instance took that reaction, so a few rounds suffice; the next poll tries again.
    private const int ClaimRounds = 5;

    private const string LastAttemptLostMessage = "The last attempt did not report back before its lock ran out.";

    private const string DeliveryDeletedMessage = "The delivery was deleted.";

    private readonly DeliveryReactionOptions options;
    private readonly ILogger<ReactionStore> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactionStore"/> class.
    /// </summary>
    public ReactionStore(IOptions<DeliveryReactionOptions> options, ILogger<ReactionStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Value;
        this.logger = logger;
    }

    /// <summary>
    /// Claims the reaction that has been due the longest and starts its next attempt. A running reaction whose lock ran
    /// out counts as due, unless its last attempt has run out, in which case it gives up.
    /// </summary>
    /// <returns>The claimed reaction, or <see langword="null"/> when none is due.</returns>
    public async Task<DeliveryReaction?> ClaimNextAsync(Context context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var gaveUp = await GiveUpLostLastAttemptsAsync(context, cancellationToken);
        if (gaveUp > 0)
        {
            logger.LogError("{Count} delivery reactions gave up because their last attempt did not report back before its lock ran out.", gaveUp);
        }

        for (var round = 0; round < ClaimRounds; round++)
        {
            var candidateId = await Claimable(context)
                .OrderBy(r => r.NextAttemptAt)
                .ThenBy(r => r.Id)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (candidateId is not int reactionId)
            {
                return null;
            }

            var claimToken = Guid.NewGuid();
            if (await TryClaimAsync(context, reactionId, claimToken, cancellationToken))
            {
                return await context.DeliveryReactions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .SingleAsync(r => r.Id == reactionId && r.ClaimToken == claimToken, cancellationToken);
            }
        }

        return null;
    }

    /// <summary>
    /// Finishes the attempt of a claimed reaction as successful.
    /// </summary>
    /// <returns><see cref="ReactionState.Done"/>, or <see langword="null"/> when the attempt no longer holds the
    /// reaction.</returns>
    public async Task<ReactionState?> CompleteAsync(Context context, DeliveryReaction reaction, string? message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reaction);

        var updated = await HeldBy(context, reaction).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(r => r.State, ReactionState.Done)
                .SetProperty(r => r.FinishedAt, r => DateTime.UtcNow)
                .SetProperty(r => r.LockedUntil, (DateTime?)null)
                .SetProperty(r => r.ClaimToken, (Guid?)null)
                .SetProperty(r => r.Message, message),
            cancellationToken);
        return updated == 1 ? ReactionState.Done : null;
    }

    /// <summary>
    /// Finishes the attempt of a claimed reaction as failed. The next attempt is due its wait after the start of the
    /// failed one, at once if that has passed; after the last attempt the reaction gives up.
    /// </summary>
    /// <returns><see cref="ReactionState.Pending"/> when another attempt follows, <see cref="ReactionState.Error"/>
    /// when the reaction gave up, or <see langword="null"/> when the attempt no longer holds the reaction.</returns>
    public async Task<ReactionState?> FailAsync(Context context, DeliveryReaction reaction, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reaction);

        var gaveUp = reaction.Attempts >= options.MaxAttempts;
        var wait = gaveUp ? TimeSpan.Zero : options.EffectiveRetryDelays[reaction.Attempts - 1];
        var updated = await HeldBy(context, reaction).ExecuteUpdateAsync(
            setters =>
            {
                setters
                    .SetProperty(r => r.LockedUntil, (DateTime?)null)
                    .SetProperty(r => r.ClaimToken, (Guid?)null)
                    .SetProperty(r => r.Message, message);
                if (gaveUp)
                {
                    setters
                        .SetProperty(r => r.State, ReactionState.Error)
                        .SetProperty(r => r.FinishedAt, r => DateTime.UtcNow);
                }
                else
                {
                    // From the start, not the end, so the run of the attempts does not push the last one out.
                    setters
                        .SetProperty(r => r.State, ReactionState.Pending)
                        .SetProperty(r => r.NextAttemptAt, r => (r.LastAttemptAt ?? DateTime.UtcNow) + wait);
                }
            },
            cancellationToken);
        if (updated != 1)
            return null;

        return gaveUp ? ReactionState.Error : ReactionState.Pending;
    }

    /// <summary>
    /// Ends the reactions of a deleted delivery that are not finished yet. A running attempt is not waited for: its
    /// report is refused, because its reaction no longer belongs to it.
    /// </summary>
    /// <returns>All reactions of the delivery, in the state the deletion left them.</returns>
    public async Task<IReadOnlyList<DeliveryReaction>> MarkDeletedAsync(Context context, int deliveryId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await OfDelivery(context, deliveryId)
            .Where(r => r.State == ReactionState.Pending || r.State == ReactionState.Running)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.State, ReactionState.Deleted)
                    .SetProperty(r => r.FinishedAt, r => DateTime.UtcNow)
                    .SetProperty(r => r.LockedUntil, (DateTime?)null)
                    .SetProperty(r => r.ClaimToken, (Guid?)null)
                    .SetProperty(r => r.Message, DeliveryDeletedMessage),
                cancellationToken);

        return await OfDelivery(context, deliveryId)
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Claims one reaction if it is still claimable. Every condition sits in the update itself, so when two instances
    /// claim the same reaction, the second one finds it claimed once the first commits.
    /// </summary>
    internal async Task<bool> TryClaimAsync(Context context, int reactionId, Guid claimToken, CancellationToken cancellationToken)
    {
        var lockDuration = options.TimeLimit + LockMargin;
        var updated = await Claimable(context)
            .Where(r => r.Id == reactionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.State, ReactionState.Running)
                    .SetProperty(r => r.Attempts, r => r.Attempts + 1)
                    .SetProperty(r => r.ClaimToken, claimToken)
                    .SetProperty(r => r.LastAttemptAt, r => DateTime.UtcNow)
                    .SetProperty(r => r.LockedUntil, r => DateTime.UtcNow + lockDuration),
                cancellationToken);
        return updated == 1;
    }

    // shortcut: one update without the ids of the reactions, so the execution protocol keeps their last attempt as
    // running. Select the ids first once the protocol has to record the giving up itself.
    private Task<int> GiveUpLostLastAttemptsAsync(Context context, CancellationToken cancellationToken)
    {
        var maxAttempts = options.MaxAttempts;
        return context.DeliveryReactions
            .Where(r => r.State == ReactionState.Running && r.LockedUntil < DateTime.UtcNow && r.Attempts >= maxAttempts)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.State, ReactionState.Error)
                    .SetProperty(r => r.FinishedAt, r => DateTime.UtcNow)
                    .SetProperty(r => r.LockedUntil, (DateTime?)null)
                    .SetProperty(r => r.ClaimToken, (Guid?)null)
                    .SetProperty(r => r.Message, LastAttemptLostMessage),
                cancellationToken);
    }

    // Due, and a notification only once its delivery is forwarded; the query filter leaves out deleted deliveries.
    private IQueryable<DeliveryReaction> Claimable(Context context)
    {
        var maxAttempts = options.MaxAttempts;
        return context.DeliveryReactions.Where(r =>
            ((r.State == ReactionState.Pending && r.NextAttemptAt <= DateTime.UtcNow)
                || (r.State == ReactionState.Running && r.LockedUntil < DateTime.UtcNow && r.Attempts < maxAttempts))
            && (r.Kind == ReactionKind.Transfer
                || !context.DeliveryReactions.Any(transfer =>
                    transfer.DeliveryId == r.DeliveryId && transfer.Kind == ReactionKind.Transfer && transfer.State != ReactionState.Done)));
    }

    // The delivery is deleted by then, which the query filter would hide.
    private static IQueryable<DeliveryReaction> OfDelivery(Context context, int deliveryId)
        => context.DeliveryReactions.IgnoreQueryFilters().Where(r => r.DeliveryId == deliveryId);

    private static IQueryable<DeliveryReaction> HeldBy(Context context, DeliveryReaction reaction)
        => context.DeliveryReactions.Where(r => r.Id == reaction.Id && r.State == ReactionState.Running && r.ClaimToken == reaction.ClaimToken);
}
