using System.Text;

namespace Geopilot.Api.Controllers;

/// <summary>
/// The wrapper exists for one reason: telling a fault in the request body apart from a fault in the storage it is
/// copied into. Both are an <see cref="IOException"/> out of the same copy, and they deserve different answers.
/// </summary>
[TestClass]
public class RequestBodyStreamTest
{
    [TestMethod]
    public async Task PassesContentThroughUnchanged()
    {
        using var inner = new MemoryStream(Encoding.UTF8.GetBytes("the body"));
        using var stream = new RequestBodyStream(inner);
        using var target = new MemoryStream();

        await stream.CopyToAsync(target);

        Assert.AreEqual("the body", Encoding.UTF8.GetString(target.ToArray()));
    }

    [TestMethod]
    public async Task MarksAFaultOfTheBodyAsSuch()
    {
        using var stream = new RequestBodyStream(new FaultingStream());
        using var target = new MemoryStream();

        await Assert.ThrowsAsync<RequestBodyException>(
            () => stream.CopyToAsync(target),
            "A body that ends before it said it would is the caller's to fix, so it must not stay an IOException like a failing disk.");
    }

    [TestMethod]
    public void KeepsTheOriginalFaultAsTheCause()
    {
        using var stream = new RequestBodyStream(new FaultingStream());

        var exception = Assert.ThrowsExactly<RequestBodyException>(() => stream.Read(new byte[8], 0, 8));

        Assert.IsInstanceOfType<IOException>(exception.InnerException, "What actually went wrong belongs in the log.");
    }

    /// <summary>
    /// Stands in for a request body that ends before its declared length, which is what the multipart reader
    /// reports as "Unexpected end of Stream".
    /// </summary>
    private sealed class FaultingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new IOException("Unexpected end of Stream, the content may have already been read by another component.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new IOException("Unexpected end of Stream, the content may have already been read by another component.");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
