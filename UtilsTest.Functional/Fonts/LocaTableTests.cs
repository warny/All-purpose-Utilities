using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Parsing;
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

    // TODO-pass2 item 21: short-format loca entries are Offset16 (unsigned), read as raw*2. Verify
    // the specific boundary values called out by the audit, in particular that 0x8000..0xFFFF widen
    // to large positive offsets (65536..131070) instead of being misread as negative via Int16.
    [DataTestMethod]
    [DataRow((ushort)0x0000, 0)]
    [DataRow((ushort)0x7FFF, 65534)]
    [DataRow((ushort)0x8000, 65536)]
    [DataRow((ushort)0xFFFF, 131070)]
    public void ShortFormat_BoundaryValues_WidenToUnsignedByteOffsets(ushort encoded, int expectedByteOffset)
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        // Two entries (GlyphCount=1): the boundary value, then itself again so size=0 and no
        // monotonicity violation is introduced by the test fixture itself.
        w.Write<ushort>(encoded);
        w.Write<ushort>(encoded);
        byte[] original = ms.ToArray();

        var table = NewTable(1, indexToLocFormat: 0);
        table.ReadData(MakeReader(original));

        Assert.AreEqual(expectedByteOffset, table[0].offset);
        Assert.AreEqual(0, table[0].size);
    }

    [TestMethod]
    public void ReadData_DecreasingOffsets_Throws()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        short[] rawOffsets = [0, 20, 10]; // decreasing: entry 2 < entry 1
        foreach (var o in rawOffsets) w.Write<short>(o);

        var table = NewTable(2, indexToLocFormat: 0);
        Assert.ThrowsExactly<FontParseException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    [TestMethod]
    public void ReadData_LongFormat_OffsetAboveIntMaxValue_Throws()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<uint>(0);
        w.Write<uint>(0x80000000u); // > int.MaxValue

        var table = NewTable(1, indexToLocFormat: 1);
        Assert.ThrowsExactly<FontParseException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    // TODO-pass2 item 22 / section 10.6 / section 11: PrepareForSerialization recomputes offsets
    // from the actual glyph lengths and selects short vs. long format automatically, updating
    // head.IndexToLocFormat -- rather than a plain property read silently mutating state.
    [TestMethod]
    public void PrepareForSerialization_AllOffsetsEvenAndSmall_SelectsShortFormat()
    {
        var font = new TrueTypeFont(0);
        var head = (HeadTable)font.CreateTable(TableTypes.HEAD);
        head.IndexToLocFormat = 1; // start long; PrepareForSerialization should switch it back
        font.AddTable(TableTypes.HEAD, head);
        var maxp = (MaxpTable)font.CreateTable(TableTypes.MAXP);
        maxp.NumGlyphs = 0;
        font.AddTable(TableTypes.MAXP, maxp);
        var loca = (LocaTable)font.CreateTable(TableTypes.LOCA);
        font.AddTable(TableTypes.LOCA, loca);
        using (var locaMs = new MemoryStream())
        {
            var locaWriter = new Writer(locaMs, BigEndianWriter.WriterDelegates);
            locaWriter.Write<int>(0); // GlyphCount + 1 = 1 entry
            loca.ReadData(MakeReader(locaMs.ToArray()));
        }
        var glyf = (GlyfTable)font.CreateTable(TableTypes.GLYF);
        font.AddTable(TableTypes.GLYF, glyf);
        glyf.ReadData(MakeReader([])); // zero glyphs: trivially valid, all offsets are 0

        loca.PrepareForSerialization();

        Assert.AreEqual((short)0, head.IndexToLocFormat);
    }
}
