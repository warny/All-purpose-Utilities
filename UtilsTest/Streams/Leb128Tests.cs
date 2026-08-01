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

    // ── Overflow / overlong rejection (item 17) ──────────────────────────────

    [TestMethod]
    public void ReadULEB128_ElevenBytes_Throws()
    {
        // 11 continuation bytes: continuation bit stays set on the tenth byte → overflow.
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01]);
        Assert.ThrowsException<OverflowException>(() => new SimpleReader(ms).ReadULEB128());
    }

    [TestMethod]
    public void ReadULEB128_TenthBytePayloadTooLarge_ThrowsOverflow()
    {
        // Nine continuation bytes then a tenth byte with payload > 0x01 → value would exceed 64 bits.
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02]);
        Assert.ThrowsException<OverflowException>(() => new SimpleReader(ms).ReadULEB128());
    }

    [TestMethod]
    public void ReadULEB128_MaxValueEncoding_RoundTrips()
    {
        // ulong.MaxValue = ten bytes, the last being 0x01.
        using var ms = new MemoryStream([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01]);
        Assert.AreEqual(ulong.MaxValue, new SimpleReader(ms).ReadULEB128());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x80, 0x00 })]        // overlong 0
    [DataRow(new byte[] { 0x81, 0x00 })]        // overlong 1
    [DataRow(new byte[] { 0xFF, 0x80, 0x00 })]  // overlong 127
    public void ReadULEB128_Overlong_ThrowsFormatException(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        Assert.ThrowsException<FormatException>(() => new SimpleReader(ms).ReadULEB128());
    }

    [TestMethod]
    public void ReadSLEB128_ElevenBytes_Throws()
    {
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00]);
        Assert.ThrowsException<OverflowException>(() => new SimpleReader(ms).ReadSLEB128());
    }

    [TestMethod]
    public void ReadSLEB128_BadSignExtensionOnTenthByte_ThrowsOverflow()
    {
        // Nine continuation bytes then a tenth byte whose high bits are not a valid sign extension.
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x40]);
        Assert.ThrowsException<OverflowException>(() => new SimpleReader(ms).ReadSLEB128());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x80, 0x00 })]        // overlong 0
    [DataRow(new byte[] { 0x81, 0x00 })]        // overlong 1
    [DataRow(new byte[] { 0xFF, 0x7F })]        // overlong -1
    public void ReadSLEB128_Overlong_ThrowsFormatException(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        Assert.ThrowsException<FormatException>(() => new SimpleReader(ms).ReadSLEB128());
    }

    [TestMethod]
    public void ReadULEB128_AfterError_PositionAdvancedOnlyByBytesConsumed()
    {
        // 0x80 0x00 (overlong) followed by a trailing marker byte.
        using var ms = new MemoryStream([0x80, 0x00, 0x2A]);
        var reader = new SimpleReader(ms);
        try { reader.ReadULEB128(); Assert.Fail("Expected FormatException."); }
        catch (FormatException) { }
        // Exactly two bytes were consumed; the marker remains readable.
        Assert.AreEqual(0x2A, ms.ReadByte());
    }

    [TestMethod]
    public void ReadULEB128_TruncatedAtTenBytes_PositionMatchesConsumed()
    {
        // Ten continuation bytes with no terminator: overflow triggers on the tenth byte.
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80]);
        var reader = new SimpleReader(ms);
        Assert.ThrowsException<OverflowException>(() => reader.ReadULEB128());
        Assert.AreEqual(10, ms.Position, "Reader must stop after consuming the tenth byte.");
    }

    // ── 10-byte overlong SLEB128 (regression for item 17 fix) ────────────────

    [TestMethod]
    public void ReadSLEB128_TenByteOverlongZero_ThrowsFormatException()
    {
        // Zero encoded with nine continuation bytes then a zero terminal byte:
        // 80 80 80 80 80 80 80 80 80 00 — the ninth byte's payload (0x00) has sign bit 0x40 clear,
        // so a terminal 0x00 on the tenth byte merely repeats the already-established sign and is overlong.
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00]);
        Assert.ThrowsException<FormatException>(() => new SimpleReader(ms).ReadSLEB128());
    }

    [TestMethod]
    public void ReadSLEB128_TenByteOverlongNegativeOne_ThrowsFormatException()
    {
        // -1 encoded with nine continuation bytes then a 0x7F terminal byte:
        // FF FF FF FF FF FF FF FF FF 7F — the ninth byte's payload (0x7F) has sign bit 0x40 set,
        // so a terminal 0x7F on the tenth byte merely repeats the negative sign and is overlong.
        using var ms = new MemoryStream([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]);
        Assert.ThrowsException<FormatException>(() => new SimpleReader(ms).ReadSLEB128());
    }

    [TestMethod]
    public void ReadULEB128_TenByteOverlongZero_ThrowsFormatException()
    {
        // Zero encoded with nine continuation bytes then a zero terminal byte:
        // 80 80 80 80 80 80 80 80 80 00 — trailing zero payload is overlong (same check as byte-2 overlongs).
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00]);
        Assert.ThrowsException<FormatException>(() => new SimpleReader(ms).ReadULEB128());
    }

    [TestMethod]
    public void ReadSLEB128_LongMinValue_RoundTrips_NotOverlong()
    {
        // long.MinValue requires all 10 bytes; its canonical encoding must NOT be rejected as overlong.
        // Encoding: 80 80 80 80 80 80 80 80 80 7F
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteSLEB128(long.MinValue);
        Assert.AreEqual(10, ms.Length, "long.MinValue must encode to exactly 10 bytes.");
        ms.Position = 0;
        Assert.AreEqual(long.MinValue, new SimpleReader(ms).ReadSLEB128());
    }

    [TestMethod]
    public void ReadSLEB128_LongMaxValue_RoundTrips_NotOverlong()
    {
        // long.MaxValue also requires 10 bytes; its canonical encoding must NOT be rejected as overlong.
        // Encoding: FF FF FF FF FF FF FF FF FF 00
        using var ms = new MemoryStream();
        new SimpleWriter(ms).WriteSLEB128(long.MaxValue);
        Assert.AreEqual(10, ms.Length, "long.MaxValue must encode to exactly 10 bytes.");
        ms.Position = 0;
        Assert.AreEqual(long.MaxValue, new SimpleReader(ms).ReadSLEB128());
    }
}
