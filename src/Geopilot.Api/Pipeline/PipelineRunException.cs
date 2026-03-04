namespace Geopilot.Api.Pipeline;

/// <summary>
/// The exception that is thrown when a pipeline run failed due to a misconfigured pipeline or an misbehaving process.
/// </summary>
[Serializable]
public class PipelineRunException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunException"/> class.
    /// </summary>
    public PipelineRunException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunException"/> class
    /// with a specified error <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PipelineRunException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunException"/> class
    /// with a specified error <paramref name="message"/> and a reference to the
    /// <paramref name="innerException"/> that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PipelineRunException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
