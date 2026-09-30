namespace Geopilot.Api.Controllers;

/// <summary>
/// Passes a request body through and marks a fault in it as a <see cref="RequestBodyException"/>. Wrap the body
/// before handing it to anything that copies it into storage: a caller that sent a truncated body and a disk that
/// cannot take the file both raise an <see cref="IOException"/> from the same copy, and the answer differs. The
/// first is a bad request, the second is the installation's problem and must not be blamed on the caller.
/// </summary>
internal sealed class RequestBodyStream : Stream
{
    private const string FaultMessage = "The request body could not be read.";

    private readonly Stream inner;
    private readonly bool leaveOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBodyStream"/> class.
    /// </summary>
    /// <param name="inner">The body to read from.</param>
    /// <param name="leaveOpen">Whether <paramref name="inner"/> survives this wrapper being disposed. True where
    /// it belongs to someone else, which is the case for a part handed out by a multipart reader: the reader
    /// still has to drain it to reach the next part.</param>
    public RequestBodyStream(Stream inner, bool leaveOpen = false)
    {
        this.inner = inner;
        this.leaveOpen = leaveOpen;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        try
        {
            return inner.Read(buffer, offset, count);
        }
        catch (IOException ex)
        {
            throw new RequestBodyException(FaultMessage, ex);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.ReadAsync(buffer, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new RequestBodyException(FaultMessage, ex);
        }
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
            inner.Dispose();

        base.Dispose(disposing);
    }
}

/// <summary>
/// A fault while reading the request body, as opposed to one while writing what was read. Both arrive as an
/// <see cref="IOException"/> and are indistinguishable once the body is copied straight into storage, yet only
/// this one is the caller's to fix.
/// </summary>
internal sealed class RequestBodyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBodyException"/> class.
    /// </summary>
    public RequestBodyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBodyException"/> class.
    /// </summary>
    public RequestBodyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBodyException"/> class.
    /// </summary>
    public RequestBodyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
