using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.Fonts.TTF.Tables.CMap;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class CmapTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static CmapTable NewTable() => (CmapTable)new TrueTypeFont(0).CreateTable(TableTypes.CMAP);

    private static (CmapTable table, byte[] bytes) RoundTrip(CmapTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = NewTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // Regression test: CmapTable.ReadData used to compute each subtable's length from the
    // *previous* subtable's offset instead of the *next* one, so with more than one subtable the
    // first subtable's length was wrongly set to its own file offset (a small number, far below
    // the 262 bytes a format-0 subtable needs), causing it to be silently skipped as malformed.
    [TestMethod]
    public void MultipleSubtables_AllSurviveRoundTrip()
    {
        var source = NewTable();
        var mac = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        mac.SetMap((byte)'A', 10);
        var windows = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        windows.SetMap((byte)'A', 20);

        source.AddCMap(1, 0, mac);     // Macintosh Roman
        source.AddCMap(3, 1, windows); // Windows Unicode BMP

        var (table, _) = RoundTrip(source);

        Assert.AreEqual((short)2, table.NumberSubtables);

        var readMac = (CMapFormat0)table.GetCMap(1, 0);
        var readWindows = (CMapFormat0)table.GetCMap(3, 1);
        Assert.IsNotNull(readMac);
        Assert.IsNotNull(readWindows);
        Assert.AreEqual((short)10, readMac.Map('A'));
        Assert.AreEqual((short)20, readWindows.Map('A'));
    }

    [TestMethod]
    public void SingleSubtable_RoundTrip()
    {
        var source = NewTable();
        var mac = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        mac.SetMap((byte)'Z', 42);
        source.AddCMap(1, 0, mac);

        var (table, _) = RoundTrip(source);

        Assert.AreEqual((short)1, table.NumberSubtables);
        var read = (CMapFormat0)table.GetCMap(1, 0);
        Assert.IsNotNull(read);
        Assert.AreEqual((short)42, read.Map('Z'));
    }
}
