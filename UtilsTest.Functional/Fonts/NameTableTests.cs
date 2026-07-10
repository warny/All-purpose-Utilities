using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class NameTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static NameTable NewTable() => (NameTable)new TrueTypeFont(0).CreateTable(TableTypes.NAME);

    [TestMethod]
    public void RoundTrip_PreservesRecords()
    {
        var source = NewTable();
        source.AddRecord(TtfPlatFormId.Microsoft, TtfPlatformSpecificID.UNICODE_DEFAULT, TtfLanguageID.MS_English_United_States, TtfNameID.FAMILY, "My Font");
        source.AddRecord(TtfPlatFormId.Microsoft, TtfPlatformSpecificID.UNICODE_DEFAULT, TtfLanguageID.MS_English_United_States, TtfNameID.SUBFAMILY, "Regular");

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        var table = NewTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual((short)2, table.Count);
        Assert.AreEqual("My Font", table.GetRecord(TtfPlatFormId.Microsoft, TtfPlatformSpecificID.UNICODE_DEFAULT, TtfLanguageID.MS_English_United_States, TtfNameID.FAMILY));
        Assert.AreEqual("Regular", table.GetRecord(TtfPlatFormId.Microsoft, TtfPlatformSpecificID.UNICODE_DEFAULT, TtfLanguageID.MS_English_United_States, TtfNameID.SUBFAMILY));
    }

    // Regression test: WriteData positions the stream, via Push()/Seek()/Pop(), to write each
    // record's string bytes into the storage area -- but on return leaves the stream positioned
    // right after the last record's *directory entry*, not after the storage area's actual string
    // bytes (which extend further). Anything written to the same stream immediately afterwards
    // (e.g. the next table in a real font) must land after the table's real length, not there.
    [TestMethod]
    public void WriteData_LeavesStreamPositionedAfterItsOwnData()
    {
        var source = NewTable();
        source.AddRecord(TtfPlatFormId.Microsoft, TtfPlatformSpecificID.UNICODE_DEFAULT, TtfLanguageID.MS_English_United_States, TtfNameID.FAMILY, "My Font");

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);

        Assert.AreEqual(source.Length, ms.Position);
    }
}
