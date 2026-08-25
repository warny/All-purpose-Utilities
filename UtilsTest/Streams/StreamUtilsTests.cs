using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
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
        Assert.ThrowsExactly<EndOfStreamException>(() => ms.ReadBytes(5, raiseException: true));
    }

    // ---- item 10: CopyToStream validation de bufferSize ----

    [TestMethod]
    public void CopyToStream_ZeroBufferSize_Throws()
    {
        using var src = new MemoryStream(new byte[] { 1, 2 });
        using var dst = new MemoryStream();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => src.CopyToStream(dst, 0));
    }

    [TestMethod]
    public void CopyToStream_NegativeBufferSize_Throws()
    {
        using var src = new MemoryStream(new byte[] { 1, 2 });
        using var dst = new MemoryStream();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => src.CopyToStream(dst, -1));
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
        Assert.ThrowsExactly<InvalidOperationException>(() => ms.ReadToEnd(maxBytes: 10));
    }

    [TestMethod]
    public void ReadToEnd_NoLimit_ReturnsAll()
    {
        byte[] data = new byte[] { 5, 6, 7, 8 };
        using var ms = new MemoryStream(data);
        byte[] result = ms.ReadToEnd();
        Assert.AreEqual(4, result.Length);
    }

    // ---- item 26: bounded whole-stream helpers ----

    /// <summary>
    /// A stream that returns at most one byte per Read call, to exercise partial reads.
    /// </summary>
    private sealed class OneByteAtATimeStream : Stream
    {
        private readonly byte[] _data;
        private int _pos;
        public OneByteAtATimeStream(byte[] data) => _data = data;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos >= _data.Length || count == 0) return 0;
            buffer[offset] = _data[_pos++];
            return 1;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [TestMethod]
    public void ReadToMemoryStream_Empty_ReturnsEmpty()
    {
        using var src = new MemoryStream((byte[])[]);
        using var ms = src.ReadToMemoryStream();
        Assert.AreEqual(0, ms.Length);
        Assert.AreEqual(0, ms.Position);
    }

    [TestMethod]
    public void ReadToMemoryStream_ExactlyAtLimit_Succeeds()
    {
        byte[] data = new byte[10];
        using var src = new MemoryStream(data);
        using var ms = src.ReadToMemoryStream(maxBytes: 10);
        Assert.AreEqual(10, ms.Length);
        Assert.AreEqual(0, ms.Position);
    }

    [TestMethod]
    public void ReadToMemoryStream_OneOverLimit_Throws()
    {
        byte[] data = new byte[11];
        using var src = new MemoryStream(data);
        Assert.ThrowsExactly<InvalidOperationException>(() => src.ReadToMemoryStream(maxBytes: 10));
    }

    [TestMethod]
    public void ReadToMemoryStream_NonSeekableSource_Works()
    {
        var src = new OneByteAtATimeStream(new byte[] { 1, 2, 3, 4, 5 });
        using var ms = src.ReadToMemoryStream();
        var cmp = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(cmp.Equals(new byte[] { 1, 2, 3, 4, 5 }, ms.ToArray()));
    }

    [TestMethod]
    public void ReadToMemoryStream_PartialReads_ReadsAll()
    {
        var src = new OneByteAtATimeStream(new byte[] { 9, 8, 7 });
        using var ms = src.ReadToMemoryStream(maxBytes: 3);
        Assert.AreEqual(3, ms.Length);
    }

    [TestMethod]
    public void ReadToMemoryStream_NegativeLimit_Throws()
    {
        using var src = new MemoryStream(new byte[] { 1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => src.ReadToMemoryStream(maxBytes: -1));
    }

    [TestMethod]
    public void ReadAllText_WithinLimit_ReturnsText()
    {
        byte[] data = Encoding.UTF8.GetBytes("hello");
        using var src = new MemoryStream(data);
        Assert.AreEqual("hello", src.ReadAllText(Encoding.UTF8, maxBytes: 100));
    }

    [TestMethod]
    public void ReadAllText_OneOverLimit_Throws()
    {
        byte[] data = Encoding.UTF8.GetBytes("hello world");
        using var src = new MemoryStream(data);
        Assert.ThrowsExactly<InvalidOperationException>(() => src.ReadAllText(Encoding.UTF8, maxBytes: 5));
    }

    [TestMethod]
    public void ReadAllText_BomStream_DetectsAndStripsBom()
    {
        // UTF-8 BOM followed by "abc"
        byte[] data = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', (byte)'b', (byte)'c' };
        using var src = new MemoryStream(data);
        Assert.AreEqual("abc", src.ReadAllText(encoding: null, maxBytes: 100));
    }

    [TestMethod]
    public void ReadBlock_Bounded_SeparatorAtStart_ReturnsEmpty()
    {
        using var src = new MemoryStream(new byte[] { 0xFF, 1, 2, 3 });
        byte[] block = src.ReadBlock(new byte[] { 0xFF }, maxBytes: 100);
        Assert.AreEqual(0, block.Length);
    }

    [TestMethod]
    public void ReadBlock_Bounded_SeparatorInMiddle_ReturnsPrefix()
    {
        using var src = new MemoryStream(new byte[] { 1, 2, 0xFF, 3, 4 });
        byte[] block = src.ReadBlock(new byte[] { 0xFF }, maxBytes: 100);
        var cmp = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(cmp.Equals(new byte[] { 1, 2 }, block));
    }

    [TestMethod]
    public void ReadBlock_Bounded_MultiByteSeparatorCrossingChunkBoundary()
    {
        // Build data where the 2-byte separator straddles the 4096-byte chunk boundary.
        var list = new System.Collections.Generic.List<byte>();
        for (int i = 0; i < 4095; i++) list.Add(0x00);
        list.Add(0xAB); // index 4095 (last of first chunk)
        list.Add(0xCD); // index 4096 (first of second chunk)
        list.Add(0x99);
        using var src = new MemoryStream(list.ToArray());
        byte[] block = src.ReadBlock(new byte[] { 0xAB, 0xCD }, maxBytes: 10000);
        Assert.AreEqual(4095, block.Length);
    }

    [TestMethod]
    public void ReadBlock_Bounded_ABABAC_Pattern_UsesKmpCorrectly()
    {
        // Searching for "ABAC" in "ABABAC..." : the naive window would mismatch,
        // KMP correctly finds the match ending after the 6th byte.
        byte[] data = new byte[] { (byte)'A', (byte)'B', (byte)'A', (byte)'B', (byte)'A', (byte)'C', (byte)'X' };
        using var src = new MemoryStream(data);
        byte[] block = src.ReadBlock(new byte[] { (byte)'A', (byte)'B', (byte)'A', (byte)'C' }, maxBytes: 100);
        var cmp = EnumerableEqualityComparer<byte>.Default;
        Assert.IsTrue(cmp.Equals(new byte[] { (byte)'A', (byte)'B' }, block));
    }

    [TestMethod]
    public void ReadBlock_Bounded_EofWithoutSeparator_Throws()
    {
        using var src = new MemoryStream(new byte[] { 1, 2, 3 });
        Assert.ThrowsExactly<EndOfStreamException>(() => src.ReadBlock(new byte[] { 0xFF }, maxBytes: 100));
    }

    [TestMethod]
    public void ReadBlock_Bounded_LimitExceededBeforeSeparator_Throws()
    {
        using var src = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 0xFF });
        Assert.ThrowsExactly<InvalidOperationException>(() => src.ReadBlock(new byte[] { 0xFF }, maxBytes: 3));
    }

    [TestMethod]
    public void ReadBlock_Bounded_NegativeMaxBytes_Throws()
    {
        using var src = new MemoryStream(new byte[] { 0xFF });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => src.ReadBlock(new byte[] { 0xFF }, maxBytes: -1));
    }

    [TestMethod]
    public void ReadBlock_Bounded_TwoConsecutiveCallsInSingleChunk_NoBytesLost()
    {
        // Two blocks and both separators fit within a single 4096-byte window.
        // If ReadBlock over-reads (reads a full chunk and then scans it) it will swallow bytes from
        // the second block. This test catches that regression.
        var sep = new byte[] { 0xFF, 0xFE };
        var data = new System.Collections.Generic.List<byte>();
        data.AddRange(new byte[] { 10, 20, 30 });
        data.AddRange(sep);
        data.AddRange(new byte[] { 40, 50 });
        data.AddRange(sep);
        data.AddRange(new byte[] { 99 }); // trailing byte after all separators

        using var src = new MemoryStream(data.ToArray());
        var cmp = EnumerableEqualityComparer<byte>.Default;

        byte[] first = src.ReadBlock(sep, maxBytes: 100);
        Assert.IsTrue(cmp.Equals(new byte[] { 10, 20, 30 }, first), "first block must be [10, 20, 30]");

        byte[] second = src.ReadBlock(sep, maxBytes: 100);
        Assert.IsTrue(cmp.Equals(new byte[] { 40, 50 }, second), "second block must be [40, 50]");

        // The trailing byte must still be readable — the two ReadBlock calls must not have over-consumed.
        Assert.AreEqual(99, src.ReadByte(), "trailing byte must not have been consumed by ReadBlock");
    }
}
