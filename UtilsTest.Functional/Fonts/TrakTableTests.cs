using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class TrakTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (TrakTable table, byte[] bytes) RoundTrip(TrakTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new TrakTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    [TestMethod]
    public void RoundTrip_HorizontalOnly()
    {
        var source = new TrakTable
        {
            HorizData = new TrakTable.TrackData
            {
                Sizes = [Fixed(9), Fixed(12), Fixed(18)],
                Entries =
                [
                    new TrakTable.TrackEntry { Track = Fixed(-1), NameIndex = 300, PerSizeValues = [-10, -5, 0] },
                    new TrakTable.TrackEntry { Track = Fixed(1),  NameIndex = 301, PerSizeValues = [10, 5, 0] },
                ]
            }
        };

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(source.Length, bytes.Length);
        Assert.IsNotNull(table.HorizData);
        Assert.IsNull(table.VertData);
        CollectionAssert.AreEqual(source.HorizData.Sizes, table.HorizData.Sizes);
        Assert.AreEqual(2, table.HorizData.Entries.Length);
        Assert.AreEqual((ushort)300, table.HorizData.Entries[0].NameIndex);
        CollectionAssert.AreEqual(new short[] { -10, -5, 0 }, table.HorizData.Entries[0].PerSizeValues);
        CollectionAssert.AreEqual(new short[] { 10, 5, 0 }, table.HorizData.Entries[1].PerSizeValues);
    }

    [TestMethod]
    public void RoundTrip_HorizontalAndVertical()
    {
        var source = new TrakTable
        {
            HorizData = new TrakTable.TrackData
            {
                Sizes = [Fixed(10)],
                Entries = [new TrakTable.TrackEntry { Track = 0, NameIndex = 1, PerSizeValues = [0] }]
            },
            VertData = new TrakTable.TrackData
            {
                Sizes = [Fixed(10), Fixed(20)],
                Entries = [new TrakTable.TrackEntry { Track = Fixed(1), NameIndex = 2, PerSizeValues = [5, 15] }]
            }
        };

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(source.Length, bytes.Length);
        Assert.IsNotNull(table.HorizData);
        Assert.IsNotNull(table.VertData);
        Assert.AreEqual(2, table.VertData.Sizes.Length);
        CollectionAssert.AreEqual(new short[] { 5, 15 }, table.VertData.Entries[0].PerSizeValues);
    }

    private static int Fixed(int wholeUnits) => wholeUnits << 16;
}
