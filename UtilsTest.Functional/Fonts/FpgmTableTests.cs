using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class FpgmTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static FpgmTable NewTable() => (FpgmTable)new TrueTypeFont(0).CreateTable(TableTypes.FPGM);

    [TestMethod]
    public void RoundTrip_PreservesInstructionBytes()
    {
        var source = NewTable();
        source.Instructions = [0x40, 0x01, 0x00, 0xB0, 0x2C, 0x2C];

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        Assert.AreEqual(source.Length, bytes.Length);

        var table = NewTable();
        table.ReadData(MakeReader(bytes));

        CollectionAssert.AreEqual(source.Instructions, table.Instructions);
    }
}
