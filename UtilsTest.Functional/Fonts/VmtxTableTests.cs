using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class VmtxTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    // VmtxTable sizes its arrays from the font's maxp (glyph count) and vhea (long metric count)
    // tables, wired up when the table is added to a TrueTypeFont.
    private static VmtxTable NewTable(short numGlyphs, ushort numOfLongVerMetrics)
    {
        var font = new TrueTypeFont(0);
        var maxp = (MaxpTable)font.CreateTable(TableTypes.MAXP);
        maxp.NumGlyphs = numGlyphs;
        font.AddTable(TableTypes.MAXP, maxp);

        var vhea = (VheaTable)font.CreateTable(TableTypes.VHEA);
        vhea.NumOfLongVerMetrics = numOfLongVerMetrics;
        font.AddTable(TableTypes.VHEA, vhea);

        var vmtx = (VmtxTable)font.CreateTable(TableTypes.VMTX);
        font.AddTable(TableTypes.VMTX, vmtx);
        return vmtx;
    }

    [TestMethod]
    public void RoundTrip_MoreGlyphsThanLongMetrics_LastAdvanceHeightIsImplicitlyRepeated()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<short>(1000); w.Write<short>(15); // glyph 0: advanceHeight=1000, tsb=15
        w.Write<short>(900); w.Write<short>(25);  // glyph 1: advanceHeight=900, tsb=25
        w.Write<short>(35);                        // glyph 2: tsb only
        w.Write<short>(45);                        // glyph 3: tsb only
        byte[] original = ms.ToArray();

        var table = NewTable(4, 2);
        table.ReadData(MakeReader(original));

        Assert.AreEqual((short)1000, table.GetAdvanceHeight(0));
        Assert.AreEqual((short)900, table.GetAdvanceHeight(1));
        Assert.AreEqual((short)900, table.GetAdvanceHeight(2)); // implicitly repeats the last explicit value
        Assert.AreEqual((short)900, table.GetAdvanceHeight(3));
        Assert.AreEqual((short)15, table.GetTopSideBearing(0));
        Assert.AreEqual((short)45, table.GetTopSideBearing(3));

        using var outMs = new MemoryStream();
        var outWriter = new Writer(outMs, BigEndianWriter.WriterDelegates);
        table.WriteData(outWriter);

        CollectionAssert.AreEqual(original, outMs.ToArray());
        Assert.AreEqual(table.Length, outMs.Position);
    }
}
