using System.Collections.Concurrent;

namespace Geopilot.Api.Processing;

/// <summary>
/// Stores machine delivery attempts in memory in a thread-safe manner.
/// </summary>
/// <remarks>
/// An attempt is process-local, like the job and the upload it belongs to. A restart ends it: the caller then
/// sees 404 and has to determine the outcome from its deliveries instead. Scaling the API out means persisting
/// this together with the processing job state.
/// </remarks>
public class SubmissionStore : ISubmissionStore
{
    private readonly ConcurrentDictionary<Guid, Submission> submissions = new();

    /// <inheritdoc/>
    public void Add(Submission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (!submissions.TryAdd(submission.JobId, submission))
            throw new InvalidOperationException($"A submission for job <{submission.JobId}> already exists.");
    }

    /// <inheritdoc/>
    public Submission? GetSubmission(Guid jobId) => submissions.TryGetValue(jobId, out var submission) ? submission : null;

    /// <inheritdoc/>
    public void MarkDelivered(Guid jobId, int deliveryId)
        => Update(jobId, submission => submission with { DeliveryId = deliveryId, DeclarationFailure = null });

    /// <inheritdoc/>
    public void MarkDeclarationFailed(Guid jobId, string reason)
        => Update(jobId, submission => submission with { DeclarationFailure = reason });

    /// <inheritdoc/>
    public void Remove(Guid jobId) => submissions.TryRemove(jobId, out _);

    private void Update(Guid jobId, Func<Submission, Submission> update)
    {
        // An attempt that is no longer here was retired with its job, which happens a whole retention period
        // after its run ended. Nothing to record, and nobody left to read it.
        // TryUpdate against the value we read, so a concurrent write is retried instead of overwritten.
        while (submissions.TryGetValue(jobId, out var current))
        {
            if (submissions.TryUpdate(jobId, update(current), current))
                return;
        }
    }
}
