using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class Os2TableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (Os2Table table, byte[] bytes) RoundTrip(Os2Table source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new Os2Table();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — Version 0 round-trip ────────────────────────────────────

    [TestMethod]
    public void Version0_RoundTrip()
    {
        var source = new Os2Table
        {
            Version          = 0,
            XAvgCharWidth    = 512,
            UsWeightClass    = 700,
            UsWidthClass     = 5,
            FsType           = Os2Table.EmbeddingFlags.PrintAndPreview,
            YSubscriptXSize  = 700,
            YSubscriptYSize  = 650,
            YSubscriptXOffset = 0,
            YSubscriptYOffset = 140,
            YSuperscriptXSize = 700,
            YSuperscriptYSize = 650,
            YSuperscriptXOffset = 0,
            YSuperscriptYOffset = 480,
            YStrikeoutSize   = 50,
            YStrikeoutPosition = 300,
            SFamilyClass     = 0x0201,
            Panose           = [2, 4, 6, 3, 5, 4, 4, 2, 2, 4],
            UlUnicodeRange1  = 0x000000FF,
            UlUnicodeRange2  = 0,
            UlUnicodeRange3  = 0,
            UlUnicodeRange4  = 0,
            AchVendId        = "TEST",
            FsSelection      = Os2Table.SelectionFlags.Bold,
            UsFirstCharIndex = 0x0020,
            UsLastCharIndex  = 0x00FF,
            STypoAscender    = 800,
            STypoDescender   = -200,
            STypoLineGap     = 0,
            UsWinAscent      = 1000,
            UsWinDescent     = 200,
        };

        Assert.AreEqual(78, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(78, bytes.Length);
        Assert.AreEqual((ushort)0,    table.Version);
        Assert.AreEqual((ushort)700,  table.UsWeightClass);
        Assert.AreEqual("TEST",       table.AchVendId);
        Assert.AreEqual(Os2Table.SelectionFlags.Bold, table.FsSelection);
        Assert.AreEqual((short)800,   table.STypoAscender);
        Assert.AreEqual((short)-200,  table.STypoDescender);
        Assert.AreEqual((ushort)1000, table.UsWinAscent);
        Assert.AreEqual((ushort)200,  table.UsWinDescent);
        Assert.AreEqual(2, table.Panose[0]);
        Assert.AreEqual(4, table.Panose[1]);
    }

    // ── Test 2 — Version 1 adds code-page ranges ─────────────────────────

    [TestMethod]
    public void Version1_CodePageRanges_RoundTrip()
    {
        var source = new Os2Table
        {
            Version          = 1,
            UsWeightClass    = 400,
            UsWidthClass     = 5,
            AchVendId        = "MSFT",
            STypoAscender    = 800,
            STypoDescender   = -200,
            UsBreakChar      = 0x0020,
            UlCodePageRange1 = 0xE0000003,
            UlCodePageRange2 = 0x00000000,
        };

        Assert.AreEqual(86, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(86, bytes.Length);
        Assert.AreEqual((ushort)1,       table.Version);
        Assert.AreEqual(0xE0000003u,     table.UlCodePageRange1);
        Assert.AreEqual(0u,              table.UlCodePageRange2);
    }

    // ── Test 3 — Version 2 adds typographic height fields ────────────────

    [TestMethod]
    public void Version2_ExtraFields_RoundTrip()
    {
        var source = new Os2Table
        {
            Version       = 2,
            UsWeightClass = 400,
            UsWidthClass  = 5,
            AchVendId     = "    ",
            STypoAscender = 800,
            STypoDescender = -200,
            SxHeight      = 500,
            SCapHeight    = 700,
            UsDefaultChar = 0,
            UsBreakChar   = 0x0020,
            UsMaxContext  = 3,
        };

        Assert.AreEqual(96, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(96, bytes.Length);
        Assert.AreEqual((ushort)2, table.Version);
        Assert.AreEqual((short)500,  table.SxHeight);
        Assert.AreEqual((short)700,  table.SCapHeight);
        Assert.AreEqual((ushort)0,   table.UsDefaultChar);
        Assert.AreEqual((ushort)0x0020, table.UsBreakChar);
        Assert.AreEqual((ushort)3,   table.UsMaxContext);
    }

    // ── Test 4 — Version 4 still 96 bytes ────────────────────────────────

    [TestMethod]
    public void Version4_Is96Bytes()
    {
        var source = new Os2Table
        {
            Version       = 4,
            UsWeightClass = 400,
            UsWidthClass  = 5,
            AchVendId     = "    ",
            FsSelection   = Os2Table.SelectionFlags.Regular | Os2Table.SelectionFlags.UseTypoMetrics,
        };

        Assert.AreEqual(96, source.Length);
        var (_, bytes) = RoundTrip(source);
        Assert.AreEqual(96, bytes.Length);
    }

    // ── Test 5 — Version 5 adds optical size range ───────────────────────

    [TestMethod]
    public void Version5_OpticalSize_RoundTrip()
    {
        var source = new Os2Table
        {
            Version                  = 5,
            UsWeightClass            = 400,
            UsWidthClass             = 5,
            AchVendId                = "    ",
            STypoAscender            = 800,
            STypoDescender           = -200,
            UsLowerOpticalPointSize  = 120,   // 6pt in twentieths
            UsUpperOpticalPointSize  = 240,   // 12pt in twentieths
        };

        Assert.AreEqual(100, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(100, bytes.Length);
        Assert.AreEqual((ushort)5,   table.Version);
        Assert.AreEqual((ushort)120, table.UsLowerOpticalPointSize);
        Assert.AreEqual((ushort)240, table.UsUpperOpticalPointSize);
    }

    // ── Test 6 — VendorID padding with short string ───────────────────────

    [TestMethod]
    public void VendorId_ShortString_PaddedWithSpaces()
    {
        // "AB" should be stored as "AB  " (two trailing spaces)
        var source = new Os2Table { Version = 0, AchVendId = "AB" };
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        // Vendor ID starts at offset 2+2+2+2+2+2+2+2+2+2+2+2+2+2+2+2+10+16 = 68? Let me count...
        // 16 shorts before panose = 32 bytes
        // panose = 10 bytes
        // 4 × UInt32 = 16 bytes
        // vendor starts at 32+10+16 = 58
        Assert.AreEqual((byte)'A', bytes[58]);
        Assert.AreEqual((byte)'B', bytes[59]);
        Assert.AreEqual((byte)' ', bytes[60]);
        Assert.AreEqual((byte)' ', bytes[61]);

        var table = new Os2Table();
        table.ReadData(MakeReader(bytes));
        Assert.AreEqual("AB  ", table.AchVendId);
    }
}
