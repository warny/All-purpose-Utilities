using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace UtilsTest.Streams;

/// <summary>
/// Tests for the variable-length string prefix helpers (item 18).
/// </summary>
[TestClass]
public class VariableLengthStringTests
{
    private static (byte[] bytes, int written) WriteString(string value, Encoding encoding, int sizeLength)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms);
        writer.WriteVariableLengthString(value, encoding, sizeLength);
        return (ms.ToArray(), (int)ms.Length);
    }

    private static string ReadString(byte[] bytes, Encoding encoding, int sizeLength)
    {
        using var ms = new MemoryStream(bytes);
        var reader = new Reader(ms);
        return reader.ReadVariableLengthString(encoding, sizeLength);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    public void RoundTrip_AllPrefixWidths(int sizeLength)
    {
        var (bytes, _) = WriteString("Hello, world!", Encoding.UTF8, sizeLength);
        Assert.AreEqual("Hello, world!", ReadString(bytes, Encoding.UTF8, sizeLength));
    }

    // ── Invalid sizeLength ─────────────────────────────────────────────────────

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(-1)]
    public void Write_InvalidSizeLength_Throws(int sizeLength)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => writer.WriteVariableLengthString("x", Encoding.UTF8, sizeLength));
        Assert.AreEqual(0, ms.Length, "No bytes must be written when sizeLength is invalid.");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(-1)]
    public void Read_InvalidSizeLength_Throws(int sizeLength)
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var reader = new Reader(ms);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => reader.ReadVariableLengthString(Encoding.UTF8, sizeLength));
        Assert.AreEqual(0, ms.Position, "No bytes must be consumed when sizeLength is invalid.");
    }

    // ── Capacity checks ────────────────────────────────────────────────────────

    [TestMethod]
    public void Write_255Bytes_Prefix1_Succeeds()
    {
        string value = new string('a', 255);
        var (bytes, _) = WriteString(value, Encoding.ASCII, 1);
        Assert.AreEqual(256, bytes.Length); // 1 prefix + 255 payload
        Assert.AreEqual(value, ReadString(bytes, Encoding.ASCII, 1));
    }

    [TestMethod]
    public void Write_256Bytes_Prefix1_ThrowsAndWritesNothing()
    {
        string value = new string('a', 256);
        using var ms = new MemoryStream();
        var writer = new Writer(ms);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => writer.WriteVariableLengthString(value, Encoding.ASCII, 1));
        Assert.AreEqual(0, ms.Length, "A failed capacity check must not write any bytes.");
    }

    [TestMethod]
    public void Write_65535Bytes_Prefix2_Succeeds()
    {
        string value = new string('a', 65535);
        var (bytes, _) = WriteString(value, Encoding.ASCII, 2);
        Assert.AreEqual(65537, bytes.Length); // 2 prefix + 65535 payload
        Assert.AreEqual(value, ReadString(bytes, Encoding.ASCII, 2));
    }

    [TestMethod]
    public void Write_65536Bytes_Prefix2_ThrowsAndWritesNothing()
    {
        string value = new string('a', 65536);
        using var ms = new MemoryStream();
        var writer = new Writer(ms);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => writer.WriteVariableLengthString(value, Encoding.ASCII, 2));
        Assert.AreEqual(0, ms.Length);
    }

    // ── Byte length vs char count for multi-byte UTF-8 ──────────────────────────

    [TestMethod]
    public void MultiByteUtf8_LengthIsMeasuredInBytesNotChars()
    {
        // Each 'é' is 2 bytes in UTF-8; "éé" is 2 chars but 4 bytes.
        string value = "éé";
        var (bytes, _) = WriteString(value, Encoding.UTF8, 1);
        Assert.AreEqual(4, bytes[0], "The prefix must record the byte length (4), not the char count (2).");
        Assert.AreEqual(value, ReadString(bytes, Encoding.UTF8, 1));
    }

    // ── Negative length in stream ──────────────────────────────────────────────

    [TestMethod]
    public void Read_NegativeLengthInStream_Prefix4_ThrowsFormatException()
    {
        // 0xFFFFFFFF little-endian = -1 as Int32.
        using var ms = new MemoryStream(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00 });
        var reader = new Reader(ms);
        Assert.ThrowsExactly<FormatException>(
            () => reader.ReadVariableLengthString(Encoding.UTF8, 4));
    }

    // ── maxByteLength enforcement ──────────────────────────────────────────────

    [TestMethod]
    public void Read_ExceedingMaxByteLength_ThrowsBeforeAllocation()
    {
        // Prefix says 100 bytes; only a few follow. The limit is checked before ReadBytes.
        using var ms = new MemoryStream(new byte[] { 100, 1, 2, 3 });
        var reader = new Reader(ms);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => reader.ReadVariableLengthString(Encoding.UTF8, 1, maxByteLength: 10));
        // Only the single prefix byte was consumed before rejection.
        Assert.AreEqual(1, ms.Position);
    }

    [TestMethod]
    public void Read_WithinMaxByteLength_Succeeds()
    {
        var (bytes, _) = WriteString("abc", Encoding.ASCII, 1);
        using var ms = new MemoryStream(bytes);
        var reader = new Reader(ms);
        Assert.AreEqual("abc", reader.ReadVariableLengthString(Encoding.ASCII, 1, maxByteLength: 100));
    }

    [TestMethod]
    public void Read_NegativeMaxByteLength_Throws()
    {
        using var ms = new MemoryStream(new byte[] { 1, (byte)'a' });
        var reader = new Reader(ms);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => reader.ReadVariableLengthString(Encoding.ASCII, 1, maxByteLength: -1));
        Assert.AreEqual(0, ms.Position, "No bytes consumed when maxByteLength is invalid.");
    }
}
