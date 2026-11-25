using Geopilot.Api.Models;
using Geopilot.Pipeline;

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
    /// A run is settled once it has a terminal state, except while a submission attempt that may be delivered
    /// has neither a delivery nor a failure yet. <paramref name="deliveries"/> must include soft-deleted
    /// deliveries (<c>IgnoreQueryFilters()</c>).
    /// </summary>
    public static IQueryable<Recording> WhereAlive(this IQueryable<Recording> runs, IQueryable<Delivery> deliveries) =>
        runs.Where(r => !(r.TerminalState != null
                && !(r.SubmissionMandateKey != null
                    && (r.TerminalState == ProcessingState.Success || r.TerminalState == ProcessingState.Warning)
                    && !deliveries.Any(d => d.JobId == r.JobId)
                    && r.DeliveryFailure == null))
            && r.HeartbeatAt != null
            && r.HeartbeatAt > DateTime.UtcNow - DeadAfter);

    /// <summary>
    /// Runs a read path may answer for: alive, or settled with a <c>TerminalAt</c> within <paramref name="jobRetention"/>.
    /// </summary>
    public static IQueryable<Recording> WhereAnswerable(this IQueryable<Recording> runs, IQueryable<Delivery> deliveries, TimeSpan jobRetention) =>
        runs.Where(r => (r.TerminalState != null
                && !(r.SubmissionMandateKey != null
                    && (r.TerminalState == ProcessingState.Success || r.TerminalState == ProcessingState.Warning)
                    && !deliveries.Any(d => d.JobId == r.JobId)
                    && r.DeliveryFailure == null))
            ? r.TerminalAt != null && r.TerminalAt > DateTime.UtcNow - jobRetention
            : r.HeartbeatAt != null && r.HeartbeatAt > DateTime.UtcNow - DeadAfter);
}
