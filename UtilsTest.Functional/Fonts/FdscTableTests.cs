using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class FdscTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (FdscTable table, byte[] bytes) RoundTrip(FdscTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new FdscTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    [TestMethod]
    public void RoundTrip_PreservesDescriptors()
    {
        var source = new FdscTable
        {
            Descriptors =
            [
                new FdscTable.FontDescriptor { Tag = "wght", Value = FdscTable.DoubleToFixed(1.5) },
                new FdscTable.FontDescriptor { Tag = "wdth", Value = FdscTable.DoubleToFixed(-0.25) },
            ]
        };

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(source.Length, bytes.Length);
        Assert.AreEqual(2, table.Descriptors.Length);
        Assert.AreEqual("wght", table.Descriptors[0].Tag);
        Assert.AreEqual(1.5, FdscTable.FixedToDouble(table.Descriptors[0].Value), 1e-4);
        Assert.AreEqual("wdth", table.Descriptors[1].Tag);
        Assert.AreEqual(-0.25, FdscTable.FixedToDouble(table.Descriptors[1].Value), 1e-4);
    }

    [TestMethod]
    public void FixedPointConversion_RoundTrips()
    {
        int fixedValue = FdscTable.DoubleToFixed(3.75);
        Assert.AreEqual(3.75, FdscTable.FixedToDouble(fixedValue), 1e-6);
    }
}
