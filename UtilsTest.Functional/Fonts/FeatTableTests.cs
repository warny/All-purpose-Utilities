using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class FeatTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (FeatTable table, byte[] bytes) RoundTrip(FeatTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new FeatTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    [TestMethod]
    public void RoundTrip_MultipleFeaturesWithSettings()
    {
        var source = new FeatTable
        {
            Features =
            [
                new FeatTable.FeatureNameRecord
                {
                    Feature = 1,
                    FeatureFlags = 0x8000,
                    NameIndex = 256,
                    Settings =
                    [
                        new FeatTable.FeatureSetting { Setting = 0, NameIndex = 257 },
                        new FeatTable.FeatureSetting { Setting = 1, NameIndex = 258 },
                    ]
                },
                new FeatTable.FeatureNameRecord
                {
                    Feature = 3,
                    FeatureFlags = 0,
                    NameIndex = 259,
                    Settings = [new FeatTable.FeatureSetting { Setting = 2, NameIndex = 260 }]
                },
            ]
        };

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(source.Length, bytes.Length);
        Assert.AreEqual(2, table.Features.Length);
        Assert.AreEqual((ushort)1, table.Features[0].Feature);
        Assert.AreEqual(2, table.Features[0].Settings.Length);
        Assert.AreEqual((ushort)0, table.Features[0].Settings[0].Setting);
        Assert.AreEqual((short)258, table.Features[0].Settings[1].NameIndex);
        Assert.AreEqual((ushort)3, table.Features[1].Feature);
        Assert.AreEqual(1, table.Features[1].Settings.Length);
        Assert.AreEqual((short)260, table.Features[1].Settings[0].NameIndex);
    }

    [TestMethod]
    public void RoundTrip_NoFeatures()
    {
        var source = new FeatTable();
        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(12, bytes.Length);
        Assert.AreEqual(0, table.Features.Length);
    }
}
