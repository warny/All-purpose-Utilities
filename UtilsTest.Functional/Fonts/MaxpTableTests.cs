using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class MaxpTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static MaxpTable NewTable() => (MaxpTable)new TrueTypeFont(0).CreateTable(TableTypes.MAXP);

    private static (MaxpTable table, byte[] bytes) RoundTrip(MaxpTable source)
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
        source.NumGlyphs = 257;
        source.MaxPoints = 120;
        source.MaxContours = 8;
        source.MaxComponentPoints = 40;
        source.MaxComponentContours = 3;
        source.MaxZones = 2;
        source.MaxTwilightPoints = 16;
        source.MaxStorage = 32;
        source.MaxFunctionDefs = 12;
        source.MaxInstructionDefs = 0;
        source.MaxStackElements = 512;
        source.MaxSizeOfInstructions = 1024;
        source.MaxComponentElements = 5;
        source.MaxComponentDepth = 2;

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(32, bytes.Length);
        Assert.AreEqual(source.Version, table.Version);
        Assert.AreEqual(source.NumGlyphs, table.NumGlyphs);
        Assert.AreEqual(source.MaxPoints, table.MaxPoints);
        Assert.AreEqual(source.MaxContours, table.MaxContours);
        Assert.AreEqual(source.MaxComponentPoints, table.MaxComponentPoints);
        Assert.AreEqual(source.MaxComponentContours, table.MaxComponentContours);
        Assert.AreEqual(source.MaxZones, table.MaxZones);
        Assert.AreEqual(source.MaxTwilightPoints, table.MaxTwilightPoints);
        Assert.AreEqual(source.MaxStorage, table.MaxStorage);
        Assert.AreEqual(source.MaxFunctionDefs, table.MaxFunctionDefs);
        Assert.AreEqual(source.MaxInstructionDefs, table.MaxInstructionDefs);
        Assert.AreEqual(source.MaxStackElements, table.MaxStackElements);
        Assert.AreEqual(source.MaxSizeOfInstructions, table.MaxSizeOfInstructions);
        Assert.AreEqual(source.MaxComponentElements, table.MaxComponentElements);
        Assert.AreEqual(source.MaxComponentDepth, table.MaxComponentDepth);
    }

    [TestMethod]
    public void ReadData_WrongSize_Throws()
    {
        var table = NewTable();
        Assert.ThrowsException<System.ArgumentException>(() => table.ReadData(MakeReader(new byte[10])));
    }
}
