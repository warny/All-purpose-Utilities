using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class CvtTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static CvtTable NewTable() => (CvtTable)new TrueTypeFont(0).CreateTable(TableTypes.CVT);

    [TestMethod]
    public void RoundTrip_PreservesControlValues()
    {
        var source = NewTable();
        source.ControlValues = [10, -20, 0, short.MaxValue, short.MinValue];

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        Assert.AreEqual(source.Length, bytes.Length);

        var table = NewTable();
        table.ReadData(MakeReader(bytes));

        CollectionAssert.AreEqual(source.ControlValues, table.ControlValues);
    }

    [TestMethod]
    public void RoundTrip_EmptyTable()
    {
        var source = NewTable();
        source.ControlValues = [];

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);

        Assert.AreEqual(0, ms.ToArray().Length);

        var table = NewTable();
        table.ReadData(MakeReader(ms.ToArray()));
        Assert.AreEqual(0, table.ControlValues.Length);
    }
}
