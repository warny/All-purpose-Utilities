using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class HheaTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static HheaTable NewTable() => (HheaTable)new TrueTypeFont(0).CreateTable(TableTypes.HHEA);

    private static (HheaTable table, byte[] bytes) RoundTrip(HheaTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = NewTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    [TestMethod]
    public void RoundTrip_PreservesAllFields()
    {
        var source = NewTable();
        source.Ascent = 1900;
        source.Descent = -500;
        source.LineGap = 90;
        source.AdvanceWidthMax = 2200;
        source.MinLeftSideBearing = -100;
        source.MinRightSideBearing = -50;
        source.XMaxExtent = 2100;
        source.CaretSlopeRise = 1;
        source.CaretSlopeRun = 0;
        source.CaretOffset = 0;
        source.MetricDataFormat = 0;
        source.NumOfLongHorMetrics = 257;

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(36, bytes.Length);
        Assert.AreEqual(source.Version, table.Version);
        Assert.AreEqual(source.Ascent, table.Ascent);
        Assert.AreEqual(source.Descent, table.Descent);
        Assert.AreEqual(source.LineGap, table.LineGap);
        Assert.AreEqual(source.AdvanceWidthMax, table.AdvanceWidthMax);
        Assert.AreEqual(source.MinLeftSideBearing, table.MinLeftSideBearing);
        Assert.AreEqual(source.MinRightSideBearing, table.MinRightSideBearing);
        Assert.AreEqual(source.XMaxExtent, table.XMaxExtent);
        Assert.AreEqual(source.CaretSlopeRise, table.CaretSlopeRise);
        Assert.AreEqual(source.CaretSlopeRun, table.CaretSlopeRun);
        Assert.AreEqual(source.CaretOffset, table.CaretOffset);
        Assert.AreEqual(source.MetricDataFormat, table.MetricDataFormat);
        Assert.AreEqual(source.NumOfLongHorMetrics, table.NumOfLongHorMetrics);
    }

    [TestMethod]
    public void ReadData_WrongSize_Throws()
    {
        var table = NewTable();
        Assert.ThrowsException<System.ArgumentException>(() => table.ReadData(MakeReader(new byte[10])));
    }
}
