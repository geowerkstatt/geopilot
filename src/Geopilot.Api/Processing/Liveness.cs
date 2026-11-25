using Geopilot.Api.Models;
using Geopilot.Pipeline;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Geopilot.Api.Processing;

/// <summary>
/// Decides whether a run of the execution protocol is alive, settled, and whether read paths may answer for it.
/// All time comparisons use the database clock, never the clock of the instance.
/// </summary>
public static class Liveness
{
    /// <summary>How often an instance records that it is still processing a run.</summary>
    // shortcut: fixed interval, make it configurable if a deployment needs a different load on the database.
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>A run without a heartbeat for this long is considered dead.</summary>
    // shortcut: fixed, must stay above the heartbeat interval plus the retries of a heartbeat write.
    public static readonly TimeSpan DeadAfter = TimeSpan.FromMinutes(3);

    /// <summary>An instance without a successful write for this long stops its own run.</summary>
    // shortcut: fixed, must stay below DeadAfter so an instance stops before others declare its run dead.
    public static readonly TimeSpan AbortAfter = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Runs that are not settled and whose last heartbeat is younger than <see cref="DeadAfter"/>.
    /// <paramref name="deliveries"/> must include soft-deleted deliveries (<c>IgnoreQueryFilters()</c>).
    /// </summary>
    public static IQueryable<Recording> WhereAlive(this IQueryable<Recording> runs, IQueryable<Delivery> deliveries) =>
        runs.Where(Alive(deliveries));

    /// <summary>
    /// Runs that are settled: terminal, unless a submission attempt that may be delivered has neither a delivery nor a
    /// failure yet. A run that is neither alive nor settled is dead.
    /// <paramref name="deliveries"/> must include soft-deleted deliveries (<c>IgnoreQueryFilters()</c>).
    /// </summary>
    public static IQueryable<Recording> WhereSettled(this IQueryable<Recording> runs, IQueryable<Delivery> deliveries) =>
        runs.Where(Settled(deliveries));

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

    /// <summary>
    /// Finds the run of <paramref name="jobId"/> with its steps and their artifacts, when a read path may answer for it
    /// (see <see cref="WhereAnswerable"/>), otherwise <see langword="null"/>. The result is not tracked.
    /// </summary>
    public static Task<Recording?> FindAnswerableRecordingAsync(this Context context, Guid jobId, TimeSpan jobRetention, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Recordings
            .AsNoTracking()
            .Include(r => r.Steps)
            .ThenInclude(s => s.Artifacts)
            .Where(r => r.JobId == jobId)
            .WhereAnswerable(context.Deliveries.IgnoreQueryFilters(), jobRetention)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The name the step gave the download stored as <paramref name="persistedFileName"/> for <paramref name="jobId"/>,
    /// or <see langword="null"/> if the protocol knows no such download. Independent of the answer window: the file
    /// store decides whether the file still exists.
    /// </summary>
    public static Task<string?> FindDownloadNameAsync(this Context context, Guid jobId, string persistedFileName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.RecordingArtifacts
            .AsNoTracking()
            .Where(a => a.Kind == ArtifactKind.Download && a.PersistedFileName == persistedFileName && a.Step.Recording.JobId == jobId)
            .Select(a => a.OriginalFileName)
            .FirstOrDefaultAsync(cancellationToken);
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
