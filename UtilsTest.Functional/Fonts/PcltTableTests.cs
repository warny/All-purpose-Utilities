using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class PcltTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (PcltTable table, byte[] bytes) RoundTrip(PcltTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new PcltTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — Fixed size is always 54 bytes ────────────────────────────

    [TestMethod]
    public void Length_IsAlways54()
    {
        Assert.AreEqual(54, new PcltTable().Length);
    }

    // ── Test 2 — Round-trip all fields ────────────────────────────────────

    [TestMethod]
    public void AllFields_RoundTrip()
    {
        var source = new PcltTable
        {
            Version              = 0x00010000,
            FontNumber           = 0x00001234,
            Pitch                = 600,
            XHeight              = 400,
            Style                = 0,
            TypeFamily           = 0x0020,
            CapHeight            = 700,
            SymbolSet            = 341,
            Typeface             = "MyTestFont",
            CharacterComplement  = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F],
            FileName             = "MYFONT",
            StrokeWeight         = 0,
            WidthType            = 0,
            SerifStyle           = 64,
        };

        Assert.AreEqual(54, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(54, bytes.Length);
        Assert.AreEqual(0x00010000,   table.Version);
        Assert.AreEqual(0x00001234u,  table.FontNumber);
        Assert.AreEqual((ushort)600,  table.Pitch);
        Assert.AreEqual((ushort)400,  table.XHeight);
        Assert.AreEqual("MyTestFont", table.Typeface);
        Assert.AreEqual("MYFONT",     table.FileName);
        Assert.AreEqual((sbyte)0,     table.StrokeWeight);
        Assert.AreEqual((byte)64,     table.SerifStyle);
        Assert.AreEqual(0xFF, table.CharacterComplement[0]);
        Assert.AreEqual(0x7F, table.CharacterComplement[7]);
    }

    // ── Test 3 — Short typeface name is null-padded ───────────────────────

    [TestMethod]
    public void ShortTypefaceName_NullPadded()
    {
        var source = new PcltTable { Typeface = "AB" };
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        // Typeface starts at: 4+4+2+2+2+2+2+2 = 20
        Assert.AreEqual((byte)'A',  bytes[20]);
        Assert.AreEqual((byte)'B',  bytes[21]);
        Assert.AreEqual((byte)0,    bytes[22]);  // null-padding
        Assert.AreEqual((byte)0,    bytes[35]);  // last byte of 16-byte field

        var table = new PcltTable();
        table.ReadData(MakeReader(bytes));
        Assert.AreEqual("AB", table.Typeface);  // TrimEnd removes nulls
    }

    // ── Test 4 — Wrong size throws ───────────────────────────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReadData_WrongSize_Throws()
    {
        var table = new PcltTable();
        table.ReadData(MakeReader(new byte[20]));  // not 54 bytes
    }
}
