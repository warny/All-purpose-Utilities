using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables.CMap;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class CMapFormat4Tests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (CMapFormat4 table, byte[] bytes) RoundTrip(CMapFormat4 source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = (CMapFormat4)CMapFormatBase.CreateCMap(4, source.Language);
        // WriteData does not re-emit format/length/language (those are written by CmapTable's
        // caller via CMapFormatBase); mirror what CMapFormatBase.GetMap does when reading a
        // stand-alone subtable stream.
        var reader = MakeReader(bytes);
        reader.Read<short>(); // format
        var length = reader.Read<short>(); // length
        reader.Read<short>(); // language
        table.ReadData(length, reader);
        return (table, bytes);
    }

    // Regression test: WriteData used to write each explicit glyph-index entry as a single byte
    // (data.Write<Byte>) instead of the 2-byte word the cmap format 4 glyphIdArray requires, which
    // silently corrupted the written stream for any glyph index above 255.
    [TestMethod]
    public void TableMapSegment_WithGlyphIndexAbove255_RoundTrips()
    {
        var source = (CMapFormat4)CMapFormatBase.CreateCMap(4, 0);
        source.AddSegment('A', 'C', [1000, 2000, 3000]);

        var (table, _) = RoundTrip(source);

        Assert.AreEqual((short)1000, table.Map('A'));
        Assert.AreEqual((short)2000, table.Map('B'));
        Assert.AreEqual((short)3000, table.Map('C'));
    }

    [TestMethod]
    public void MultipleSegments_DeltaAndTableMap_RoundTrip()
    {
        var source = (CMapFormat4)CMapFormatBase.CreateCMap(4, 0);
        source.AddSegment('a', 'z', (short)(-32)); // maps a-z to A-Z via delta
        source.AddSegment((char)0x100, (char)0x102, [500, 501, 502]);

        var (table, _) = RoundTrip(source);

        Assert.AreEqual((short)('a' - 32), table.Map('a'));
        Assert.AreEqual((short)('m' - 32), table.Map('m'));
        Assert.AreEqual((short)500, table.Map((char)0x100));
        Assert.AreEqual((short)502, table.Map((char)0x102));
    }
}
