using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class FmtxTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    [TestMethod]
    public void RoundTrip_PreservesAllFields()
    {
        var source = new FmtxTable
        {
            GlyphIndex = 42,
            HorizontalBefore = 1,
            HorizontalAfter = 2,
            HorizontalCaretHead = 3,
            HorizontalCaretBase = 4,
            VerticalBefore = 5,
            VerticalAfter = 6,
            VerticalCaretHead = 7,
            VerticalCaretBase = 8,
        };

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        Assert.AreEqual(16, bytes.Length);

        var table = new FmtxTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(source.Version, table.Version);
        Assert.AreEqual(source.GlyphIndex, table.GlyphIndex);
        Assert.AreEqual(source.HorizontalBefore, table.HorizontalBefore);
        Assert.AreEqual(source.HorizontalAfter, table.HorizontalAfter);
        Assert.AreEqual(source.HorizontalCaretHead, table.HorizontalCaretHead);
        Assert.AreEqual(source.HorizontalCaretBase, table.HorizontalCaretBase);
        Assert.AreEqual(source.VerticalBefore, table.VerticalBefore);
        Assert.AreEqual(source.VerticalAfter, table.VerticalAfter);
        Assert.AreEqual(source.VerticalCaretHead, table.VerticalCaretHead);
        Assert.AreEqual(source.VerticalCaretBase, table.VerticalCaretBase);
    }

    [TestMethod]
    public void ReadData_WrongSize_Throws()
    {
        var table = new FmtxTable();
        Assert.ThrowsException<System.ArgumentException>(() => table.ReadData(MakeReader(new byte[10])));
    }
}
