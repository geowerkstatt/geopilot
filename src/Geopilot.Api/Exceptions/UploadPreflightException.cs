using Geopilot.Api.Enums;

namespace Geopilot.Api.Exceptions;

/// <summary>
/// The exception that is thrown when preflight checks for an upload fail.
/// </summary>
public class UploadPreflightException : Exception
{
    /// <summary>
    /// Gets the reason for the preflight failure.
    /// </summary>
    public PreflightFailureReason FailureReason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadPreflightException"/> class.
    /// </summary>
    public UploadPreflightException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadPreflightException"/> class
    /// with a specified <paramref name="failureReason"/> and error <paramref name="message"/>.
    /// </summary>
    /// <param name="failureReason">The reason for the preflight failure.</param>
    /// <param name="message">The message that describes the error.</param>
    public UploadPreflightException(PreflightFailureReason failureReason, string message)
        : base(message)
    {
        FailureReason = failureReason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadPreflightException"/> class
    /// with a specified error <paramref name="message"/> and a reference to the
    /// <paramref name="innerException"/> that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UploadPreflightException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
