using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class GaspTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (GaspTable table, byte[] bytes) RoundTrip(GaspTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new GaspTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — Empty ranges ────────────────────────────────────────────────

    [TestMethod]
    public void EmptyRanges_RoundTrip()
    {
        var source = new GaspTable { Version = 0, Ranges = [] };

        Assert.AreEqual(4, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(4, bytes.Length);
        Assert.AreEqual((ushort)0, table.Version);
        Assert.AreEqual(0, table.Ranges.Length);
    }

    // ── Test 2 — Version 0 with two ranges ──────────────────────────────────

    [TestMethod]
    public void Version0_TwoRanges_RoundTrip()
    {
        var source = new GaspTable
        {
            Version = 0,
            Ranges =
            [
                new GaspTable.GaspRange { RangeMaxPPEM = 8,  RangeGaspBehavior = GaspTable.GaspBehavior.DoGray },
                new GaspTable.GaspRange { RangeMaxPPEM = 0xFFFF, RangeGaspBehavior = GaspTable.GaspBehavior.Gridfit | GaspTable.GaspBehavior.DoGray },
            ]
        };

        // Header(4) + 2 × 4 = 12 bytes
        Assert.AreEqual(12, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(12, bytes.Length);
        Assert.AreEqual((ushort)0, table.Version);
        Assert.AreEqual(2, table.Ranges.Length);
        Assert.AreEqual((ushort)8,      table.Ranges[0].RangeMaxPPEM);
        Assert.AreEqual(GaspTable.GaspBehavior.DoGray, table.Ranges[0].RangeGaspBehavior);
        Assert.AreEqual((ushort)0xFFFF, table.Ranges[1].RangeMaxPPEM);
        Assert.AreEqual(GaspTable.GaspBehavior.Gridfit | GaspTable.GaspBehavior.DoGray, table.Ranges[1].RangeGaspBehavior);
    }

    // ── Test 3 — Version 1 with ClearType flags ──────────────────────────────

    [TestMethod]
    public void Version1_ClearTypeFlags_RoundTrip()
    {
        var source = new GaspTable
        {
            Version = 1,
            Ranges =
            [
                new GaspTable.GaspRange
                {
                    RangeMaxPPEM = 20,
                    RangeGaspBehavior = GaspTable.GaspBehavior.Gridfit
                                      | GaspTable.GaspBehavior.SymmetricGridfit
                                      | GaspTable.GaspBehavior.SymmetricSmoothing,
                },
                new GaspTable.GaspRange
                {
                    RangeMaxPPEM = 0xFFFF,
                    RangeGaspBehavior = GaspTable.GaspBehavior.DoGray
                                      | GaspTable.GaspBehavior.SymmetricSmoothing,
                },
            ]
        };

        Assert.AreEqual(12, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual((ushort)1, table.Version);
        Assert.AreEqual(2, table.Ranges.Length);
        Assert.AreEqual((ushort)20, table.Ranges[0].RangeMaxPPEM);
        Assert.IsTrue((table.Ranges[0].RangeGaspBehavior & GaspTable.GaspBehavior.SymmetricGridfit) != 0);
        Assert.IsTrue((table.Ranges[1].RangeGaspBehavior & GaspTable.GaspBehavior.SymmetricSmoothing) != 0);
    }
}
