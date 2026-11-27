using Geopilot.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// The exception that is thrown when the execution protocol could not record something the status of a job is read
/// from, because the database stayed unavailable through every attempt. The job cannot go on unrecorded, but nothing
/// is wrong with its data, so it ends <see cref="ProcessingState.Cancelled"/> and keeps its upload for another start.
/// </summary>
public class RecordingUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingUnavailableException"/> class for the write that failed.
    /// </summary>
    /// <param name="write">What the protocol could not record, such as "that step &lt;validation&gt; started".</param>
    /// <param name="innerException">The failure of the last attempt.</param>
    public RecordingUnavailableException(string write, Exception innerException)
        : base($"The execution protocol could not record {write}: the database stayed unavailable.", innerException)
    {
    }
}
