using Geopilot.Api.Models;
using Geopilot.Pipeline;
using System.Linq.Expressions;

namespace Geopilot.Api.Processing;

/// <summary>
/// Decides whether a run of the execution protocol is alive, settled, and whether read paths may answer for it.
/// All time comparisons use the database clock, never the clock of the instance.
/// </summary>
public static class Liveness
{
    // shortcut: fixed interval, make it configurable if a deployment needs a different load on the database.
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    // shortcut: fixed, must stay above the heartbeat interval plus the retries of a heartbeat write.
    public static readonly TimeSpan DeadAfter = TimeSpan.FromMinutes(3);

    // shortcut: fixed, must stay below DeadAfter so an instance stops before others declare its run dead.
    public static readonly TimeSpan AbortAfter = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Runs that are not settled and whose last heartbeat is younger than <see cref="DeadAfter"/>.
    /// <paramref name="deliveries"/> must include soft-deleted deliveries (<c>IgnoreQueryFilters()</c>).
    /// </summary>
    public static IQueryable<Recording> WhereAlive(this IQueryable<Recording> runs, IQueryable<Delivery> deliveries) =>
        runs.Where(Alive(deliveries));

    /// <summary>
    /// Runs a read path may answer for: alive, or settled with a <c>TerminalAt</c> within <paramref name="jobRetention"/>.
    /// </summary>
    public static IQueryable<Recording> WhereAnswerable(this IQueryable<Recording> runs, IQueryable<Delivery> deliveries, TimeSpan jobRetention)
    {
        Expression<Func<Recording, bool>> settledWithinRetention = r => r.TerminalAt != null && r.TerminalAt > DateTime.UtcNow - jobRetention;
        var run = Expression.Parameter(typeof(Recording), "r");
        var settled = Expression.Invoke(Settled(deliveries), run);
        var body = Expression.OrElse(
            Expression.AndAlso(settled, Expression.Invoke(settledWithinRetention, run)),
            Expression.Invoke(Alive(deliveries), run));
        return runs.Where(Expression.Lambda<Func<Recording, bool>>(body, run));
    }

    private static Expression<Func<Recording, bool>> Alive(IQueryable<Delivery> deliveries)
    {
        Expression<Func<Recording, bool>> heartbeatIsFresh = r => r.HeartbeatAt != null && r.HeartbeatAt > DateTime.UtcNow - DeadAfter;
        var run = Expression.Parameter(typeof(Recording), "r");
        var body = Expression.AndAlso(
            Expression.Not(Expression.Invoke(Settled(deliveries), run)),
            Expression.Invoke(heartbeatIsFresh, run));
        return Expression.Lambda<Func<Recording, bool>>(body, run);
    }

    // Settled: terminal, unless a submission attempt that may be delivered has neither a delivery nor a failure yet.
    private static Expression<Func<Recording, bool>> Settled(IQueryable<Delivery> deliveries) =>
        r => r.TerminalState != null
            && !(r.SubmissionMandateKey != null
                && (r.TerminalState == ProcessingState.Success || r.TerminalState == ProcessingState.Warning)
                && !deliveries.Any(d => d.JobId == r.JobId)
                && r.DeliveryFailure == null);
}
