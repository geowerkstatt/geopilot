using Geopilot.Api.Contracts;

namespace Geopilot.Api.Exceptions;

/// <summary>
/// The exception that is thrown when preflight checks for a cloud upload fail.
/// </summary>
[Serializable]
public class CloudUploadPreflightException : Exception
{
    /// <summary>
    /// Gets the reason for the preflight failure.
    /// </summary>
    public PreflightFailureReason FailureReason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudUploadPreflightException"/> class.
    /// </summary>
    public CloudUploadPreflightException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudUploadPreflightException"/> class
    /// with a specified <paramref name="failureReason"/> and error <paramref name="message"/>.
    /// </summary>
    /// <param name="failureReason">The reason for the preflight failure.</param>
    /// <param name="message">The message that describes the error.</param>
    public CloudUploadPreflightException(PreflightFailureReason failureReason, string message)
        : base(message)
    {
        FailureReason = failureReason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudUploadPreflightException"/> class
    /// with a specified error <paramref name="message"/> and a reference to the
    /// <paramref name="innerException"/> that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CloudUploadPreflightException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
