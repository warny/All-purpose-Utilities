using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class LocaTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    // LocaTable sizes itself from the font's head (index-to-loc format) and maxp (glyph count)
    // tables, wired up when the table is added to a TrueTypeFont.
    private static LocaTable NewTable(short numGlyphs, short indexToLocFormat)
    {
        var font = new TrueTypeFont(0);
        var head = (HeadTable)font.CreateTable(TableTypes.HEAD);
        head.IndexToLocFormat = indexToLocFormat;
        font.AddTable(TableTypes.HEAD, head);

        var maxp = (MaxpTable)font.CreateTable(TableTypes.MAXP);
        maxp.NumGlyphs = numGlyphs;
        font.AddTable(TableTypes.MAXP, maxp);

        var loca = (LocaTable)font.CreateTable(TableTypes.LOCA);
        font.AddTable(TableTypes.LOCA, loca);
        return loca;
    }

    [TestMethod]
    public void ShortFormat_RoundTrip_OffsetsAreDoubledOnRead()
    {
        // Short format stores offset/2 on the wire; 3 glyphs -> 4 offsets (GlyphCount + 1).
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        short[] rawOffsets = [0, 10, 20, 30]; // actual byte offsets 0, 20, 40, 60
        foreach (var o in rawOffsets) w.Write<short>(o);
        byte[] original = ms.ToArray();

        var table = NewTable(3, indexToLocFormat: 0);
        table.ReadData(MakeReader(original));

        var records = table.ToArray();
        Assert.AreEqual(3, records.Length);
        Assert.AreEqual(0, table[0].offset);
        Assert.AreEqual(20, table[0].size);
        Assert.AreEqual(20, table[1].offset);
        Assert.AreEqual(20, table[1].size);
        Assert.AreEqual(40, table[2].offset);
        Assert.AreEqual(20, table[2].size);

        using var outMs = new MemoryStream();
        var outWriter = new Writer(outMs, BigEndianWriter.WriterDelegates);
        table.WriteData(outWriter);

        CollectionAssert.AreEqual(original, outMs.ToArray());
        Assert.AreEqual(table.Length, outMs.Position);
    }

    [TestMethod]
    public void LongFormat_RoundTrip_OffsetsAreExact()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        int[] rawOffsets = [0, 123, 4567, 4567]; // last two glyphs share an offset -> zero-length glyph
        foreach (var o in rawOffsets) w.Write<int>(o);
        byte[] original = ms.ToArray();

        var table = NewTable(3, indexToLocFormat: 1);
        table.ReadData(MakeReader(original));

        Assert.AreEqual(0, table[0].offset);
        Assert.AreEqual(123, table[0].size);
        Assert.AreEqual(123, table[1].offset);
        Assert.AreEqual(4444, table[1].size);
        Assert.AreEqual(4567, table[2].offset);
        Assert.AreEqual(0, table[2].size);

        using var outMs = new MemoryStream();
        var outWriter = new Writer(outMs, BigEndianWriter.WriterDelegates);
        table.WriteData(outWriter);

        CollectionAssert.AreEqual(original, outMs.ToArray());
    }
}
