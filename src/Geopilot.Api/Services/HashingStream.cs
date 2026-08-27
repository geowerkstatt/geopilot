using System.Security.Cryptography;

namespace Geopilot.Api.Services;

/// <summary>
/// Read-through decorator that computes the SHA-256 of everything read from the inner stream, so the
/// malware scan and the hash for the execution protocol share one pass over the bytes. The hash is only
/// meaningful for a sequential, complete read, which is how the INSTREAM scan consumes the stream, so
/// seeking is deliberately unsupported: repositioning would silently corrupt the hash. Length and the
/// current position stay readable. Takes ownership of the inner stream and disposes it along with itself.
/// </summary>
internal sealed class HashingStream : Stream
{
    private readonly Stream inner;
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private string? hashHex;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashingStream"/> class, taking ownership of
    /// <paramref name="inner"/>.
    /// </summary>
    public HashingStream(Stream inner)
    {
        this.inner = inner;
    }

    /// <summary>
    /// The SHA-256 of all bytes read so far, as lowercase hex. Finalized on first access.
    /// </summary>
    public string HashHex => hashHex ??= Convert.ToHexStringLower(hash.GetHashAndReset());

    /// <inheritdoc/>
    public override bool CanRead => inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => inner.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException("Seeking would corrupt the hash.");
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        if (read > 0)
            hash.AppendData(buffer, offset, read);
        return read;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        if (read > 0)
            hash.AppendData(buffer.Span[..read]);
        return read;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Seeking would corrupt the hash.");

    /// <inheritdoc/>
    public override void Flush() => inner.Flush();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hash.Dispose();
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
