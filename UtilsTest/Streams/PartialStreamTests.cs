using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO;
using Utils.Collections;

namespace UtilsTest.Streams;

[TestClass]
public class PartialStreamTests
{
    private class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner = new MemoryStream();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }

    // ---- existing tests (unchanged) ----

    [TestMethod]
    public void ConstructorThrowsWhenStreamNotSeekable()
    {
        var stream = new NonSeekableStream();
        Assert.ThrowsException<ArgumentException>(() => new PartialStream(stream, 10));
    }

    [TestMethod]
    public void ReadRespectsBoundsAndBasePositionUnchanged()
    {
        byte[] data = new byte[100];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;
        using MemoryStream baseStream = new MemoryStream(data);
        PartialStream ps = new PartialStream(baseStream, 50, 10);

        byte[] buffer = new byte[10];
        int read = ps.Read(buffer, 0, buffer.Length);

        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(10, read);
        Assert.AreEqual(data.AsSpan(50, 10).ToArray(), buffer, comparer);
        Assert.AreEqual(0, baseStream.Position);
    }

    [TestMethod]
    public void WriteUpdatesUnderlyingStream()
    {
        byte[] baseData = new byte[20];
        using MemoryStream baseStream = new MemoryStream(baseData);
        PartialStream ps = new PartialStream(baseStream, 5, 10);

        byte[] toWrite = new byte[10];
        for (int i = 0; i < toWrite.Length; i++) toWrite[i] = (byte)(i + 1);
        ps.Write(toWrite, 0, toWrite.Length);

        Assert.AreEqual(10, ps.Position);
        Assert.AreEqual(0, baseStream.Position);
        var expected = new byte[20];
        System.Array.Copy(toWrite, 0, expected, 5, 10);
        var comparer = EnumerableEqualityComparer<byte>.Default;
        Assert.AreEqual(expected, baseStream.ToArray(), comparer);
    }

    [TestMethod]
    public void WriteBeyondBoundsThrows()
    {
        using MemoryStream baseStream = new MemoryStream(new byte[10]);
        PartialStream ps = new PartialStream(baseStream, 0, 5);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ps.Write(new byte[6], 0, 6));
    }

    // ---- item 3: base position restored on failure paths ----

    [TestMethod]
    public void Read_BasePositionRestoredAfterArgumentException()
    {
        using var ms = new MemoryStream(new byte[20]);
        ms.Position = 7;
        var ps = new PartialStream(ms, 0, 10);
        // Pass a bad offset so ValidateBufferArguments throws before any seek
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ps.Read(new byte[4], -1, 2));
        Assert.AreEqual(7, ms.Position, "base position must be unchanged after failed Read");
    }

    [TestMethod]
    public void Write_BasePositionRestoredAfterBoundsViolation()
    {
        using var ms = new MemoryStream(new byte[20]);
        ms.Position = 5;
        var ps = new PartialStream(ms, 0, 3);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ps.Write(new byte[5], 0, 5));
        Assert.AreEqual(5, ms.Position, "base position must be unchanged after failed Write");
    }

    // ---- item 4: constructor validation ----

    [TestMethod]
    public void ConstructorThrowsWhenStreamIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new PartialStream(null!, 10));
        Assert.ThrowsException<ArgumentNullException>(() => new PartialStream(null!, 0, 10));
    }

    [TestMethod]
    public void ConstructorThrowsForNegativeLength()
    {
        using var ms = new MemoryStream(new byte[20]);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PartialStream(ms, -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PartialStream(ms, 0, -1));
    }

    [TestMethod]
    public void ConstructorThrowsForNegativePosition()
    {
        using var ms = new MemoryStream(new byte[20]);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PartialStream(ms, -5, 10));
    }

    [TestMethod]
    public void ConstructorThrowsForPositionLengthOverflow()
    {
        // The two-argument constructor validates startOffset + length at construction time.
        using var ms = new MemoryStream(new byte[20]);
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new PartialStream(ms, long.MaxValue, 1));
    }

    [TestMethod]
    public void Constructor1_ThrowsWhenCurrentPositionPlusLengthOverflows()
    {
        // The one-argument constructor reads baseStream.Position as startOffset and
        // must validate that startOffset + length fits in a long.
        var vs = new VirtualSeekableStream(long.MaxValue);
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new PartialStream(vs, 1),
            "startOffset(=long.MaxValue) + length(=1) must be rejected");
    }

    [TestMethod]
    public void SetLength_ThrowsWhenRangeWouldOverflow()
    {
        // startOffset = 1, so setting partialLength = long.MaxValue would make
        // startOffset + partialLength overflow — must be rejected.
        var vs = new VirtualSeekableStream();
        var ps = new PartialStream(vs, 1, 5);
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => ps.SetLength(long.MaxValue),
            "startOffset(=1) + newLength(=long.MaxValue) must be rejected");
    }

    // ---- item 4: Position setter throws instead of clamping ----

    [TestMethod]
    public void PositionSetterThrowsForNegativeValue()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ps.Position = -1);
    }

    [TestMethod]
    public void PositionSetterThrowsForValueBeyondLength()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ps.Position = 11);
    }

    // ---- item 4: Seek throws instead of clamping ----

    [TestMethod]
    public void SeekThrowsBeforeBeginning()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsException<IOException>(() => ps.Seek(-1, SeekOrigin.Begin));
    }

    [TestMethod]
    public void SeekThrowsPastEnd()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ps.Seek(11, SeekOrigin.Begin));
    }

    // ---- item 4: SetLength rejects negative ----

    [TestMethod]
    public void SetLengthThrowsForNegativeValue()
    {
        using var ms = new MemoryStream(new byte[20]);
        var ps = new PartialStream(ms, 0, 10);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ps.SetLength(-1));
    }

    // ---- arithmetic overflow near long.MaxValue ----

    // A seekable stream backed by no real storage; Position and Length are purely virtual.
    // Used to test boundary arithmetic without allocating gigabytes of memory.
    private class VirtualSeekableStream : Stream
    {
        private long _position;
        public VirtualSeekableStream(long position = 0) => _position = position;
        public override bool CanRead  => true;
        public override bool CanSeek  => true;
        public override bool CanWrite => true;
        public override long Length   => long.MaxValue;
        public override long Position { get => _position; set => _position = value; }
        public override void Flush() { }
        public override int  Read(byte[] buffer, int offset, int count) => count;
        public override long Seek(long offset, SeekOrigin origin) { _position = offset; return _position; }
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    [TestMethod]
    public void Write_BoundsCheckDoesNotOverflow_NearMaxValue()
    {
        // partialLength = long.MaxValue - 2; position set to the very end
        // With the old check (partialPosition + count > partialLength), the left-hand side
        // overflows to a large negative value when count is positive, bypassing the guard.
        // The new check (count > partialLength - partialPosition) is immune to this overflow.
        var vs = new VirtualSeekableStream();
        long bigLength = long.MaxValue - 2;
        var ps = new PartialStream(vs, 0, bigLength);
        ps.Position = bigLength; // at the very end of the segment

        // count=5 exceeds the zero remaining bytes; must throw even though old arithmetic overflowed
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => ps.Write(new byte[5], 0, 5),
            "Write past the end must be rejected even when partialPosition + count overflows");
    }

    [TestMethod]
    public void Seek_Current_OverflowThrows()
    {
        var vs = new VirtualSeekableStream();
        var ps = new PartialStream(vs, 0, long.MaxValue - 1);
        ps.Position = long.MaxValue / 2;
        // offset so large that partialPosition + offset overflows long
        Assert.ThrowsException<OverflowException>(
            () => ps.Seek(long.MaxValue, SeekOrigin.Current),
            "Arithmetic overflow in SeekOrigin.Current must propagate as OverflowException");
    }

    [TestMethod]
    public void Seek_End_OverflowThrows()
    {
        var vs = new VirtualSeekableStream();
        var ps = new PartialStream(vs, 0, long.MaxValue - 1);
        // offset so large that partialLength + offset overflows long
        Assert.ThrowsException<OverflowException>(
            () => ps.Seek(long.MaxValue, SeekOrigin.End),
            "Arithmetic overflow in SeekOrigin.End must propagate as OverflowException");
    }
}
