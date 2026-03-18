using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class AvarTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (AvarTable table, byte[] bytes) RoundTrip(AvarTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new AvarTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // F2Dot14 raw encoding for a normalized value (×16384, clamped to short)
    private static short F(double v) => AvarTable.DoubleToF2Dot14(v);

    // ── Test 1 — Empty axis list ──────────────────────────────────────────

    [TestMethod]
    public void NoAxes_RoundTrip()
    {
        var source = new AvarTable { SegmentMaps = [] };

        // 8-byte header only
        Assert.AreEqual(8, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(8, bytes.Length);
        Assert.AreEqual(0, table.SegmentMaps.Length);
    }

    // ── Test 2 — Two axes with identity and non-identity maps ────────────

    [TestMethod]
    public void TwoAxes_SegmentMaps_RoundTrip()
    {
        // Axis 0: identity (−1→−1, 0→0, +1→+1)
        // Axis 1: non-linear (−1→−1, 0.2→0, +1→+1) — lifts the zero point
        var source = new AvarTable
        {
            SegmentMaps =
            [
                new AvarTable.SegmentMap
                {
                    Mappings =
                    [
                        new AvarTable.AxisValueMap(F(-1.0), F(-1.0)),
                        new AvarTable.AxisValueMap(F( 0.0), F( 0.0)),
                        new AvarTable.AxisValueMap(F(+1.0), F(+1.0)),
                    ]
                },
                new AvarTable.SegmentMap
                {
                    Mappings =
                    [
                        new AvarTable.AxisValueMap(F(-1.0), F(-1.0)),
                        new AvarTable.AxisValueMap(F( 0.2), F( 0.0)),
                        new AvarTable.AxisValueMap(F(+1.0), F(+1.0)),
                    ]
                },
            ]
        };

        // 8 + (2 + 3×4) + (2 + 3×4) = 8 + 14 + 14 = 36
        Assert.AreEqual(36, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(36, bytes.Length);
        Assert.AreEqual(2, table.SegmentMaps.Length);

        // Axis 0: identity
        Assert.AreEqual(3, table.SegmentMaps[0].Mappings.Length);
        Assert.AreEqual(F(-1.0), table.SegmentMaps[0].Mappings[0].FromCoord);
        Assert.AreEqual(F(-1.0), table.SegmentMaps[0].Mappings[0].ToCoord);
        Assert.AreEqual(F(0.0),  table.SegmentMaps[0].Mappings[1].FromCoord);
        Assert.AreEqual(F(0.0),  table.SegmentMaps[0].Mappings[1].ToCoord);

        // Axis 1: non-linear at 0.2→0
        Assert.AreEqual(F(0.2), table.SegmentMaps[1].Mappings[1].FromCoord);
        Assert.AreEqual(F(0.0), table.SegmentMaps[1].Mappings[1].ToCoord);
    }

    // ── Test 3 — F2Dot14 helpers ─────────────────────────────────────────

    [TestMethod]
    public void F2Dot14Helpers_KnownValues()
    {
        Assert.AreEqual((short)0x4000,  AvarTable.DoubleToF2Dot14(+1.0));
        Assert.AreEqual(unchecked((short)0xC000), AvarTable.DoubleToF2Dot14(-1.0));
        Assert.AreEqual((short)0x0000,  AvarTable.DoubleToF2Dot14(0.0));
        Assert.AreEqual(+1.0, AvarTable.F2Dot14ToDouble(0x4000),  1e-6);
        Assert.AreEqual(-1.0, AvarTable.F2Dot14ToDouble(unchecked((short)0xC000)), 1e-6);
    }

    // ── Test 4 — Binary decode of hand-crafted bytes ──────────────────────

    [TestMethod]
    public void ReadData_FromBinary_ParsesCorrectly()
    {
        // 1 axis, 3 mappings: (−1,−1), (0,0), (+1,+1)
        byte[] data =
        [
            0x00, 0x01,  // majorVersion = 1
            0x00, 0x00,  // minorVersion = 0
            0x00, 0x00,  // reserved
            0x00, 0x01,  // axisCount = 1
            // SegmentMap[0]
            0x00, 0x03,  // positionMapCount = 3
            0xC0, 0x00, 0xC0, 0x00,  // (−1.0, −1.0)
            0x00, 0x00, 0x00, 0x00,  // ( 0.0,  0.0)
            0x40, 0x00, 0x40, 0x00,  // (+1.0, +1.0)
        ];

        var table = new AvarTable();
        table.ReadData(MakeReader(data));

        Assert.AreEqual(1, table.SegmentMaps.Length);
        Assert.AreEqual(3, table.SegmentMaps[0].Mappings.Length);
        Assert.AreEqual(unchecked((short)0xC000), table.SegmentMaps[0].Mappings[0].FromCoord);
        Assert.AreEqual((short)0x0000,            table.SegmentMaps[0].Mappings[1].FromCoord);
        Assert.AreEqual((short)0x4000,            table.SegmentMaps[0].Mappings[2].ToCoord);
    }
}
