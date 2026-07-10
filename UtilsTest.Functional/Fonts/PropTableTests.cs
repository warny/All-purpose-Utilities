using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class PropTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (PropTable table, byte[] bytes) RoundTrip(PropTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new PropTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    [TestMethod]
    public void Format0_RoundTrip_NoPerGlyphLookup()
    {
        var source = new PropTable { Format = 0, DefaultProp = PropTable.HangsOnRight };
        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(8, bytes.Length);
        Assert.AreEqual((ushort)0, table.Format);
        Assert.AreEqual(PropTable.HangsOnRight, table.DefaultProp);
        Assert.AreEqual(0, table.GlyphProperties.Count);
    }

    [TestMethod]
    public void Format1_RoundTrip_PreservesPerGlyphOverrides()
    {
        var source = new PropTable
        {
            Format = 1,
            DefaultProp = 0,
            GlyphProperties = new()
            {
                [10] = PropTable.HangsOnLeft,
                [20] = PropTable.HangsOnRight | PropTable.DirectionMask,
            }
        };

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(source.Length, bytes.Length);
        Assert.AreEqual(2, table.GlyphProperties.Count);
        Assert.AreEqual(PropTable.HangsOnLeft, table.GlyphProperties[10]);
        Assert.AreEqual((ushort)(PropTable.HangsOnRight | PropTable.DirectionMask), table.GlyphProperties[20]);
    }
}
