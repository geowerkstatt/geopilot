namespace Geopilot.Api.Processing;

/// <summary>
/// Reacts to a processing job that has reached its terminal state. Called once per job by the
/// <see cref="ProcessingRunner"/>, on the runner's thread and outside any request.
/// </summary>
public interface IJobCompletionHandler
{
    /// <summary>
    /// Handles the finished job. Must not throw: the runner only logs what escapes here, so an implementation
    /// that lets an error escape leaves whoever waits on the job without an answer.
    /// </summary>
    /// <param name="jobId">The job that finished.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnJobFinishedAsync(Guid jobId, CancellationToken cancellationToken);
}
