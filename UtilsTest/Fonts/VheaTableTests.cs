using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class VheaTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (VheaTable table, byte[] bytes) RoundTrip(VheaTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new VheaTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — Default values round-trip ───────────────────────────────────

    [TestMethod]
    public void DefaultValues_RoundTrip()
    {
        var source = new VheaTable
        {
            Version              = 0x00011000,
            VertTypoAscender     = 500,
            VertTypoDescender    = -500,
            VertTypoLineGap      = 0,
            AdvanceHeightMax     = 1000,
            MinTopSideBearing    = 0,
            MinBottomSideBearing = -200,
            YMaxExtent           = 800,
            CaretSlopeRise       = 0,
            CaretSlopeRun        = 1,
            CaretOffset          = 0,
            MetricDataFormat     = 0,
            NumOfLongVerMetrics  = 100,
        };

        Assert.AreEqual(36, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(36, bytes.Length);
        Assert.AreEqual(0x00011000,   table.Version);
        Assert.AreEqual((short)500,   table.VertTypoAscender);
        Assert.AreEqual((short)-500,  table.VertTypoDescender);
        Assert.AreEqual((short)0,     table.VertTypoLineGap);
        Assert.AreEqual((short)1000,  table.AdvanceHeightMax);
        Assert.AreEqual((short)0,     table.MinTopSideBearing);
        Assert.AreEqual((short)-200,  table.MinBottomSideBearing);
        Assert.AreEqual((short)800,   table.YMaxExtent);
        Assert.AreEqual((short)0,     table.CaretSlopeRise);
        Assert.AreEqual((short)1,     table.CaretSlopeRun);
        Assert.AreEqual((short)0,     table.CaretOffset);
        Assert.AreEqual((short)0,     table.MetricDataFormat);
        Assert.AreEqual((ushort)100,  table.NumOfLongVerMetrics);
    }

    // ── Test 2 — Reserved bytes are zeroed on write ──────────────────────────

    [TestMethod]
    public void ReservedBytes_AreZeroOnWrite()
    {
        var source = new VheaTable { VertTypoAscender = 1 };
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        // Reserved region: bytes 22–29 (4 × Int16 at offsets 22, 24, 26, 28)
        for (int i = 22; i < 30; i++)
            Assert.AreEqual(0, bytes[i], $"Reserved byte at offset {i} should be 0");
    }

    // ── Test 3 — Bad size throws ─────────────────────────────────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ReadData_WrongSize_Throws()
    {
        var table = new VheaTable();
        table.ReadData(MakeReader(new byte[20])); // not 36 bytes → throws
    }
}
