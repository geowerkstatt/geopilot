namespace Geopilot.PipelineCore.Pipeline.Process.Container;

/// <summary>
/// Thrown when a container run cannot be started or an unrecoverable error occurs during orchestration.
/// A non-zero exit code of the container itself is NOT an exception — it is reported in <see cref="ContainerRunResult"/>.
/// </summary>
public class ContainerRunException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerRunException"/> class.
    /// </summary>
    public ContainerRunException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerRunException"/> class with the given message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ContainerRunException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerRunException"/> class with the given message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ContainerRunException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
