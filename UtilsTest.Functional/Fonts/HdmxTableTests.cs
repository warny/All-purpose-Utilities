using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class HdmxTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    // HdmxTable reads its glyph count from the font's maxp table, wired up when the table is
    // added to a TrueTypeFont.
    private static HdmxTable NewTable(short numGlyphs)
    {
        var font = new TrueTypeFont(0);
        var maxp = (MaxpTable)font.CreateTable(TableTypes.MAXP);
        maxp.NumGlyphs = numGlyphs;
        font.AddTable(TableTypes.MAXP, maxp);

        var hdmx = (HdmxTable)font.CreateTable(TableTypes.HDMX);
        font.AddTable(TableTypes.HDMX, hdmx);
        return hdmx;
    }

    [TestMethod]
    public void RoundTrip_PreservesRecordsWithPadding()
    {
        // 3 glyphs -> sizeDeviceRecord = (3 + 2 + 3) & ~3 = 8, so each record needs 3 padding bytes.
        var source = NewTable(3);
        source.Version = 0;
        source.Records =
        [
            new HdmxTable.DeviceRecord { Ppem = 9, MaxWidth = 10, Widths = [8, 9, 10] },
            new HdmxTable.DeviceRecord { Ppem = 12, MaxWidth = 13, Widths = [11, 12, 13] },
        ];

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        Assert.AreEqual(source.Length, bytes.Length);
        Assert.AreEqual(8 + 2 * 8, bytes.Length); // header(8) + 2 records * sizeDeviceRecord(8)

        var table = NewTable(3);
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(2, table.Records.Length);
        Assert.AreEqual((byte)9, table.Records[0].Ppem);
        Assert.AreEqual((byte)10, table.Records[0].MaxWidth);
        CollectionAssert.AreEqual(new byte[] { 8, 9, 10 }, table.Records[0].Widths);
        Assert.AreEqual((byte)12, table.Records[1].Ppem);
        CollectionAssert.AreEqual(new byte[] { 11, 12, 13 }, table.Records[1].Widths);
    }
}
