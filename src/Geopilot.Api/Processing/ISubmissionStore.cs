namespace Geopilot.Api.Processing;

/// <summary>
/// Keeps the machine delivery attempts of this process, keyed by their processing job.
/// </summary>
public interface ISubmissionStore
{
    /// <summary>
    /// Remembers an attempt, so the delivery can be declared once its run finishes.
    /// </summary>
    /// <exception cref="InvalidOperationException">If an attempt for the same job already exists.</exception>
    void Add(Submission submission);

    /// <summary>
    /// Gets the attempt for the specified job, or <see langword="null"/> if this process knows none.
    /// </summary>
    Submission? GetSubmission(Guid jobId);

    /// <summary>
    /// Records that the delivery was declared for the attempt.
    /// </summary>
    void MarkDelivered(Guid jobId, int deliveryId);

    /// <summary>
    /// Records why the delivery could not be declared, although the run allowed it.
    /// </summary>
    void MarkDeclarationFailed(Guid jobId, string reason);

    /// <summary>
    /// Forgets the attempt, called when its job is retired.
    /// </summary>
    void Remove(Guid jobId);
}
