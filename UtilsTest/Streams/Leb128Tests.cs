using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO.Serialization;

namespace UtilsTest.Streams;

[TestClass]
public class Leb128Tests
{
    private sealed class SimpleReader : IReader
    {
        private readonly Stream _stream;
        public SimpleReader(Stream stream) => _stream = stream;
        public T Read<T>() => throw new NotSupportedException();
        public object Read(Type type) => throw new NotSupportedException();
        public int ReadByte() => _stream.ReadByte();
        public byte[] ReadBytes(int length)
        {
            var buf = new byte[length];
            _ = _stream.Read(buf, 0, length);
            return buf;
        }
    }

    private sealed class SimpleWriter : IWriter
    {
        private readonly Stream _stream;
        public SimpleWriter(Stream stream) => _stream = stream;
        public void Write<T>(T value) => throw new NotSupportedException();
        public void Write(object value) => throw new NotSupportedException();
        public void WriteByte(byte value) => _stream.WriteByte(value);
        public void WriteBytes(ReadOnlySpan<byte> bytes) => _stream.Write(bytes);
    }

    // ── ULEB128 encoding verification ────────────────────────────────────────

    [TestMethod]
    public void WriteULEB128_Zero_ProducesSingleZeroByte()
    {
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteULEB128(0UL);
        CollectionAssert.AreEqual(new byte[] { 0x00 }, ms.ToArray());
    }

    [TestMethod]
    public void WriteULEB128_SingleByte_ProducesOneByte()
    {
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteULEB128(127UL);
        CollectionAssert.AreEqual(new byte[] { 0x7F }, ms.ToArray());
    }

    [TestMethod]
    public void WriteULEB128_128_ProducesTwoBytes()
    {
        // 128 = 0x80 → [0x80, 0x01]
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteULEB128(128UL);
        CollectionAssert.AreEqual(new byte[] { 0x80, 0x01 }, ms.ToArray());
    }

    [TestMethod]
    public void WriteULEB128_300_ProducesTwoBytes()
    {
        // 300 = 0x12C → [0xAC, 0x02]
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteULEB128(300UL);
        CollectionAssert.AreEqual(new byte[] { 0xAC, 0x02 }, ms.ToArray());
    }

    // ── SLEB128 encoding verification ────────────────────────────────────────

    [TestMethod]
    public void WriteSLEB128_Zero_ProducesSingleZeroByte()
    {
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteSLEB128(0L);
        CollectionAssert.AreEqual(new byte[] { 0x00 }, ms.ToArray());
    }

    [TestMethod]
    public void WriteSLEB128_NegativeOne_ProducesSingleByte()
    {
        // -1 → [0x7F]
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteSLEB128(-1L);
        CollectionAssert.AreEqual(new byte[] { 0x7F }, ms.ToArray());
    }

    [TestMethod]
    public void WriteSLEB128_NegativeOneTwentyEight_ProducesTwoBytes()
    {
        // -128 → [0x80, 0x7F]
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteSLEB128(-128L);
        CollectionAssert.AreEqual(new byte[] { 0x80, 0x7F }, ms.ToArray());
    }

    [TestMethod]
    public void WriteSLEB128_Positive63_ProducesSingleByte()
    {
        // 63 = 0x3F — sign bit (0x40) not set, single byte
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteSLEB128(63L);
        CollectionAssert.AreEqual(new byte[] { 0x3F }, ms.ToArray());
    }

    // ── Round-trip tests ──────────────────────────────────────────────────────

    [TestMethod]
    [DataRow(0UL)]
    [DataRow(1UL)]
    [DataRow(127UL)]
    [DataRow(128UL)]
    [DataRow(300UL)]
    [DataRow(624485UL)]
    [DataRow(ulong.MaxValue)]
    public void ULEB128_RoundTrip(ulong value)
    {
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteULEB128(value);
        ms.Position = 0;
        Assert.AreEqual(value, new SimpleReader(ms).ReadULEB128());
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(1L)]
    [DataRow(63L)]
    [DataRow(-1L)]
    [DataRow(-64L)]
    [DataRow(127L)]
    [DataRow(-128L)]
    [DataRow(300L)]
    [DataRow(-300L)]
    [DataRow(long.MinValue)]
    [DataRow(long.MaxValue)]
    public void SLEB128_RoundTrip(long value)
    {
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteSLEB128(value);
        ms.Position = 0;
        Assert.AreEqual(value, new SimpleReader(ms).ReadSLEB128());
    }

    // ── EOF handling ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ReadULEB128_EmptyStream_ThrowsEndOfStreamException()
    {
        using var ms = new MemoryStream();
        Assert.ThrowsException<EndOfStreamException>(() => new SimpleReader(ms).ReadULEB128());
    }

    [TestMethod]
    public void ReadSLEB128_EmptyStream_ThrowsEndOfStreamException()
    {
        using var ms = new MemoryStream();
        Assert.ThrowsException<EndOfStreamException>(() => new SimpleReader(ms).ReadSLEB128());
    }

    [TestMethod]
    public void ReadULEB128_TruncatedMultiByte_ThrowsEndOfStreamException()
    {
        // 0x80 has continuation bit set but no following byte
        using var ms = new MemoryStream([0x80]);
        Assert.ThrowsException<EndOfStreamException>(() => new SimpleReader(ms).ReadULEB128());
    }
}
