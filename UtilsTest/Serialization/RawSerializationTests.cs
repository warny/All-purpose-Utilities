using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

[TestClass]
public class RawSerializationTests
{
    private static (RawWriter writer, RawReader reader) CreatePair(bool bigEndian = false)
        => (new RawWriter { BigEndian = bigEndian }, new RawReader { BigEndian = bigEndian });

    private static byte[] Serialize(Action<IWriter, MemoryStream> action, out MemoryStream ms)
    {
        ms = new MemoryStream();
        var writer = new Writer(ms);
        action(writer, ms);
        return ms.ToArray();
    }

    // ---- char round-trip (item 1) ----

    [TestMethod]
    public void CharRoundTrip_LittleEndian()
    {
        var (rw, rr) = CreatePair(bigEndian: false);
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteChar(iw, 'A');
        ms.Position = 0;
        var ir = new Reader(ms);
        char result = rr.ReadChar(ir);
        Assert.AreEqual('A', result);
        Assert.AreEqual(2, ms.Length, "char must occupy exactly 2 bytes");
    }

    [TestMethod]
    public void CharRoundTrip_BigEndian()
    {
        var (rw, rr) = CreatePair(bigEndian: true);
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteChar(iw, 'Z');
        ms.Position = 0;
        var ir = new Reader(ms);
        char result = rr.ReadChar(ir);
        Assert.AreEqual('Z', result);
    }

    [TestMethod]
    public void CharRoundTrip_HighCodePoint()
    {
        // BMP character with non-zero high byte: U+0100 (LATIN CAPITAL LETTER A WITH MACRON)
        char c = 'Ā';
        var (rw, rr) = CreatePair(bigEndian: false);
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteChar(iw, c);
        ms.Position = 0;
        var ir = new Reader(ms);
        char result = rr.ReadChar(ir);
        Assert.AreEqual(c, result);
    }

    [TestMethod]
    public void CharRoundTrip_ByteFormatLE()
    {
        // 'A' is 0x0041 — LE should store [0x41, 0x00]
        var (rw, _) = CreatePair(bigEndian: false);
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteChar(iw, 'A');
        byte[] bytes = ms.ToArray();
        Assert.AreEqual(0x41, bytes[0]);
        Assert.AreEqual(0x00, bytes[1]);
    }

    [TestMethod]
    public void CharRoundTrip_ByteFormatBE()
    {
        // 'A' is 0x0041 — BE should store [0x00, 0x41]
        var (rw, _) = CreatePair(bigEndian: true);
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteChar(iw, 'A');
        byte[] bytes = ms.ToArray();
        Assert.AreEqual(0x00, bytes[0]);
        Assert.AreEqual(0x41, bytes[1]);
    }

    [TestMethod]
    public void CharIsRegisteredInWriterDelegates()
    {
        // Write<char> must resolve without falling back to reflection
        using var ms = new MemoryStream();
        var writer = new Writer(ms);
        writer.Write<char>('X');
        Assert.AreEqual(2, ms.Length, "WriteChar delegate must be registered: char must produce 2 bytes");
    }

    // ---- TimeSpan round-trip (item 2) ----

    [TestMethod]
    public void TimeSpanRoundTrip()
    {
        var span = TimeSpan.FromMilliseconds(123456.789);
        var (rw, rr) = CreatePair();
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteTimeSpan(iw, span);
        ms.Position = 0;
        var ir = new Reader(ms);
        TimeSpan result = rr.ReadTimeSpan(ir);
        Assert.AreEqual(span, result);
    }

    [TestMethod]
    public void TimeSpanRoundTrip_Zero()
    {
        var (rw, rr) = CreatePair();
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteTimeSpan(iw, TimeSpan.Zero);
        ms.Position = 0;
        var ir = new Reader(ms);
        Assert.AreEqual(TimeSpan.Zero, rr.ReadTimeSpan(ir));
    }

    [TestMethod]
    public void TimeSpanRoundTrip_Negative()
    {
        var span = TimeSpan.FromSeconds(-99);
        var (rw, rr) = CreatePair();
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteTimeSpan(iw, span);
        ms.Position = 0;
        var ir = new Reader(ms);
        Assert.AreEqual(span, rr.ReadTimeSpan(ir));
    }

    [TestMethod]
    public void TimeSpanOccupies8Bytes()
    {
        var (rw, _) = CreatePair();
        using var ms = new MemoryStream();
        var iw = new Writer(ms);
        rw.WriteTimeSpan(iw, TimeSpan.FromSeconds(1));
        Assert.AreEqual(8, ms.Length, "TimeSpan must be stored as 8-byte ticks");
    }
}
