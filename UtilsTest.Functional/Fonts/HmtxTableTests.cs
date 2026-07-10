using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class HmtxTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    // HmtxTable sizes its arrays from the font's maxp (glyph count) and hhea (long metric count)
    // tables, wired up when the table is added to a TrueTypeFont.
    private static HmtxTable NewTable(short numGlyphs, short numOfLongHorMetrics)
    {
        var font = new TrueTypeFont(0);
        var maxp = (MaxpTable)font.CreateTable(TableTypes.MAXP);
        maxp.NumGlyphs = numGlyphs;
        font.AddTable(TableTypes.MAXP, maxp);

        var hhea = (HheaTable)font.CreateTable(TableTypes.HHEA);
        hhea.NumOfLongHorMetrics = numOfLongHorMetrics;
        font.AddTable(TableTypes.HHEA, hhea);

        var hmtx = (HmtxTable)font.CreateTable(TableTypes.HMTX);
        font.AddTable(TableTypes.HMTX, hmtx);
        return hmtx;
    }

    [TestMethod]
    public void RoundTrip_MoreGlyphsThanLongMetrics_LastAdvanceIsImplicitlyRepeated()
    {
        // 5 glyphs total, only the first 2 have an explicit (advance, lsb) record; the remaining 3
        // only store a left side bearing and reuse the last explicit advance width (monospaced tail).
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<short>(600); w.Write<short>(10); // glyph 0: advance=600, lsb=10
        w.Write<short>(500); w.Write<short>(20); // glyph 1: advance=500, lsb=20
        w.Write<short>(30);                      // glyph 2: lsb only
        w.Write<short>(40);                      // glyph 3: lsb only
        w.Write<short>(50);                      // glyph 4: lsb only
        byte[] original = ms.ToArray();

        var table = NewTable(5, 2);
        table.ReadData(MakeReader(original));

        Assert.AreEqual((short)600, table.GetAdvance(0));
        Assert.AreEqual((short)500, table.GetAdvance(1));
        Assert.AreEqual((short)500, table.GetAdvance(2)); // implicitly repeats the last explicit advance
        Assert.AreEqual((short)500, table.GetAdvance(4));
        Assert.AreEqual((short)10, table.GetLeftSideBearing(0));
        Assert.AreEqual((short)50, table.GetLeftSideBearing(4));

        using var outMs = new MemoryStream();
        var outWriter = new Writer(outMs, BigEndianWriter.WriterDelegates);
        table.WriteData(outWriter);

        CollectionAssert.AreEqual(original, outMs.ToArray());
        Assert.AreEqual(table.Length, outMs.Position);
    }
}
