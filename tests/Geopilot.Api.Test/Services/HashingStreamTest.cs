using Geopilot.Api.Services;
using System.Security.Cryptography;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class HashingStreamTest
{
    [TestMethod]
    public async Task HashMatchesContentReadInChunks()
    {
        var content = new byte[(64 * 1024) + 17];
        Random.Shared.NextBytes(content);
        var expected = Convert.ToHexStringLower(SHA256.HashData(content));

        using var inner = new MemoryStream(content);
        using var hashingStream = new HashingStream(inner);

        var buffer = new byte[4096];
        var total = 0;
        int read;
        while ((read = await hashingStream.ReadAsync(buffer)) > 0)
            total += read;

        Assert.AreEqual(content.Length, total);
        Assert.AreEqual(expected, hashingStream.HashHex);
    }

    [TestMethod]
    public void HashMatchesContentReadSynchronously()
    {
        var content = "perfectly safe content"u8.ToArray();
        var expected = Convert.ToHexStringLower(SHA256.HashData(content));

        using var inner = new MemoryStream(content);
        using var hashingStream = new HashingStream(inner);

        var buffer = new byte[7];
        while (hashingStream.Read(buffer, 0, buffer.Length) > 0)
        {
        }

        Assert.AreEqual(expected, hashingStream.HashHex);
    }

    [TestMethod]
    public void ForwardsLengthAndPositionToTheInnerStream()
    {
        var content = new byte[100];
        using var inner = new MemoryStream(content);
        using var hashingStream = new HashingStream(inner);

        Assert.AreEqual(100, hashingStream.Length);
        var read = hashingStream.Read(new byte[10], 0, 10);
        Assert.AreEqual(10, read);
        Assert.AreEqual(10, hashingStream.Position);
        Assert.IsFalse(hashingStream.CanWrite);
    }

    [TestMethod]
    public void EmptyStreamYieldsHashOfNothing()
    {
        var expected = Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()));

        using var inner = new MemoryStream();
        using var hashingStream = new HashingStream(inner);
        var read = hashingStream.Read(new byte[8], 0, 8);

        Assert.AreEqual(0, read);
        Assert.AreEqual(expected, hashingStream.HashHex);
    }
}
