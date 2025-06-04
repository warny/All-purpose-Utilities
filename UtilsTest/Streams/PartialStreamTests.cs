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
}
