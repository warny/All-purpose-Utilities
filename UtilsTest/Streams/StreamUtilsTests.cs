using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.Collections;
using Utils.IO;

namespace UtilsTest.Streams;

[TestClass]
public class StreamUtilsTests
{
    // ---- item 8: ReadBytes ne renvoie que les octets lus ----

    [TestMethod]
    public void ReadBytes_ExactCount_ReturnsFullArray()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        byte[] result = ms.ReadBytes(4);
        Assert.AreEqual(4, result.Length);
        var cmp = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(cmp.Equals(new byte[] { 1, 2, 3, 4 }, result));
    }

    [TestMethod]
    public void ReadBytes_EofNoException_ReturnsOnlyBytesRead()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2 });
        byte[] result = ms.ReadBytes(5, raiseException: false);
        Assert.AreEqual(2, result.Length, "must not zero-pad: only 2 bytes available");
        Assert.AreEqual(1, result[0]);
        Assert.AreEqual(2, result[1]);
    }

    [TestMethod]
    public void ReadBytes_EofWithException_Throws()
    {
        using var ms = new MemoryStream(new byte[] { 1 });
        Assert.ThrowsException<EndOfStreamException>(() => ms.ReadBytes(5, raiseException: true));
    }

    // ---- item 10: CopyToStream validation de bufferSize ----

    [TestMethod]
    public void CopyToStream_ZeroBufferSize_Throws()
    {
        using var src = new MemoryStream(new byte[] { 1, 2 });
        using var dst = new MemoryStream();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => src.CopyToStream(dst, 0));
    }

    [TestMethod]
    public void CopyToStream_NegativeBufferSize_Throws()
    {
        using var src = new MemoryStream(new byte[] { 1, 2 });
        using var dst = new MemoryStream();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => src.CopyToStream(dst, -1));
    }

    [TestMethod]
    public void CopyToStream_Works()
    {
        byte[] data = new byte[] { 10, 20, 30 };
        using var src = new MemoryStream(data);
        using var dst = new MemoryStream();
        src.CopyToStream(dst, 1024);
        var cmp = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(cmp.Equals(data, dst.ToArray()));
    }

    // ---- item 9: ReadToEnd avec limite ----

    [TestMethod]
    public void ReadToEnd_WithinLimit_ReturnsData()
    {
        byte[] data = new byte[] { 1, 2, 3 };
        using var ms = new MemoryStream(data);
        byte[] result = ms.ReadToEnd(maxBytes: 100);
        var cmp = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(cmp.Equals(data, result));
    }

    [TestMethod]
    public void ReadToEnd_ExceedsLimit_Throws()
    {
        byte[] data = new byte[100];
        using var ms = new MemoryStream(data);
        Assert.ThrowsException<InvalidOperationException>(() => ms.ReadToEnd(maxBytes: 10));
    }

    [TestMethod]
    public void ReadToEnd_NoLimit_ReturnsAll()
    {
        byte[] data = new byte[] { 5, 6, 7, 8 };
        using var ms = new MemoryStream(data);
        byte[] result = ms.ReadToEnd();
        Assert.AreEqual(4, result.Length);
    }
}
