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
        using var ms = new MemoryStream(new byte[20]);
        Assert.ThrowsException<OverflowException>(
            () => new PartialStream(ms, long.MaxValue, 1));
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
}
