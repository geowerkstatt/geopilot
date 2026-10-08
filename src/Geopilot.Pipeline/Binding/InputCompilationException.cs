namespace Geopilot.Pipeline.Config;

/// <summary>
/// The exception that is thrown when a pipeline step's input map cannot be compiled into
/// <see cref="InputValue"/>s, for example a malformed reference or an unsupported value shape.
/// </summary>
[Serializable]
public sealed class InputCompilationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InputCompilationException"/> class.
    /// </summary>
    public InputCompilationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InputCompilationException"/> class
    /// with a specified error <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InputCompilationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InputCompilationException"/> class
    /// with a specified error <paramref name="message"/> and a reference to the
    /// <paramref name="innerException"/> that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InputCompilationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
