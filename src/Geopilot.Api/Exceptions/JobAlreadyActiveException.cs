namespace Geopilot.Api.Exceptions;

/// <summary>
/// The exception that is thrown when an upload is started while a job for it is still pending or running.
/// </summary>
public class JobAlreadyActiveException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobAlreadyActiveException"/> class for the upload that
    /// already has an active job.
    /// </summary>
    /// <param name="uploadId">The upload that was started again.</param>
    public JobAlreadyActiveException(Guid uploadId)
        : base($"Upload <{uploadId}> is still being processed. Wait for its job to end before starting it again.")
    {
    }
}
