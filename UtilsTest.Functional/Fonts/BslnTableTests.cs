using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class BslnTableTests
{
    // Big-endian reader/writer delegates matching the TrueType font encoding.
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (BslnTable table, byte[] bytes) RoundTrip(BslnTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new BslnTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — Format 0: distance-based, no mapping ──────────────────────

    [TestMethod]
    public void Format0_DistanceNoMap_RoundTrip()
    {
        var source = new BslnTable
        {
            Format          = BslnTable.BslnFormat.DistanceNoMap,
            DefaultBaseline = BslnTable.RomanBaseline,
            Deltas          = new short[BslnTable.BaselineCount]
        };
        source.Deltas[BslnTable.RomanBaseline]              =    0;
        source.Deltas[BslnTable.IdeographicCenteredBaseline] =  100;
        source.Deltas[BslnTable.HangingBaseline]             = -200;

        // Expected length: 8-byte header + 64-byte delta array = 72 bytes
        Assert.AreEqual(72, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(72, bytes.Length);
        Assert.AreEqual(source.Version, table.Version);
        Assert.AreEqual(BslnTable.BslnFormat.DistanceNoMap, table.Format);
        Assert.AreEqual(BslnTable.RomanBaseline, table.DefaultBaseline);
        Assert.AreEqual(0,    table.Deltas[BslnTable.RomanBaseline]);
        Assert.AreEqual(100,  table.Deltas[BslnTable.IdeographicCenteredBaseline]);
        Assert.AreEqual(-200, table.Deltas[BslnTable.HangingBaseline]);
        Assert.AreEqual(0, table.Deltas[31]); // untouched slot remains 0
    }

    // ── Test 2 — Format 2: control-point-based, no mapping ─────────────────

    [TestMethod]
    public void Format2_ControlPointNoMap_RoundTrip()
    {
        var source = new BslnTable
        {
            Format          = BslnTable.BslnFormat.ControlPointNoMap,
            DefaultBaseline = BslnTable.RomanBaseline,
            StandardGlyph   = 42,
            ControlPoints   = new ushort[BslnTable.BaselineCount]
        };
        // Fill all with "no point" then override a few
        for (int i = 0; i < BslnTable.BaselineCount; i++)
            source.ControlPoints[i] = BslnTable.NoControlPoint;
        source.ControlPoints[BslnTable.RomanBaseline]              = 5;
        source.ControlPoints[BslnTable.IdeographicCenteredBaseline] = 12;

        // Expected length: 8-byte header + 2-byte stdGlyph + 64-byte ctlPoints = 74 bytes
        Assert.AreEqual(74, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(74, bytes.Length);
        Assert.AreEqual(BslnTable.BslnFormat.ControlPointNoMap, table.Format);
        Assert.AreEqual((ushort)42, table.StandardGlyph);
        Assert.AreEqual((ushort)5,  table.ControlPoints[BslnTable.RomanBaseline]);
        Assert.AreEqual((ushort)12, table.ControlPoints[BslnTable.IdeographicCenteredBaseline]);
        Assert.AreEqual(BslnTable.NoControlPoint, table.ControlPoints[BslnTable.MathBaseline]);
    }

    // ── Test 3 — Format 1: distance-based, with per-glyph mapping ──────────

    [TestMethod]
    public void Format1_DistanceWithMap_RoundTrip()
    {
        var source = new BslnTable
        {
            Format          = BslnTable.BslnFormat.DistanceWithMap,
            DefaultBaseline = BslnTable.RomanBaseline,
            Deltas          = new short[BslnTable.BaselineCount],
            GlyphBaselineMap = new Dictionary<ushort, ushort>
            {
                [10] = BslnTable.IdeographicCenteredBaseline,
                [20] = BslnTable.HangingBaseline,
                [30] = BslnTable.MathBaseline
            }
        };
        source.Deltas[BslnTable.IdeographicCenteredBaseline] = 500;

        // Expected length: 8 + 64 + (12 + 4*4) = 8 + 64 + 28 = 100 bytes
        // LookupTableSize = 12 + (3 entries + 1 sentinel) * 4 = 12 + 16 = 28
        Assert.AreEqual(100, source.Length);

        var (table, _) = RoundTrip(source);

        Assert.AreEqual(BslnTable.BslnFormat.DistanceWithMap, table.Format);
        Assert.AreEqual(500, table.Deltas[BslnTable.IdeographicCenteredBaseline]);
        Assert.AreEqual(3, table.GlyphBaselineMap.Count);
        Assert.AreEqual(BslnTable.IdeographicCenteredBaseline, table.GlyphBaselineMap[10]);
        Assert.AreEqual(BslnTable.HangingBaseline,             table.GlyphBaselineMap[20]);
        Assert.AreEqual(BslnTable.MathBaseline,                table.GlyphBaselineMap[30]);
    }

    // ── Test 4 — Format 3: control-point-based, with per-glyph mapping ─────

    [TestMethod]
    public void Format3_ControlPointWithMap_RoundTrip()
    {
        var source = new BslnTable
        {
            Format          = BslnTable.BslnFormat.ControlPointWithMap,
            DefaultBaseline = BslnTable.RomanBaseline,
            StandardGlyph   = 100,
            ControlPoints   = new ushort[BslnTable.BaselineCount],
            GlyphBaselineMap = new Dictionary<ushort, ushort>
            {
                [50] = BslnTable.IdeographicCenteredBaseline,
                [60] = BslnTable.IdeographicLowBaseline
            }
        };
        for (int i = 0; i < BslnTable.BaselineCount; i++)
            source.ControlPoints[i] = BslnTable.NoControlPoint;
        source.ControlPoints[BslnTable.RomanBaseline] = 3;

        // Expected length: 8 + 66 + (12 + 3*4) = 8 + 66 + 24 = 98 bytes
        // LookupTableSize = 12 + (2 entries + 1 sentinel) * 4 = 12 + 12 = 24
        Assert.AreEqual(98, source.Length);

        var (table, _) = RoundTrip(source);

        Assert.AreEqual(BslnTable.BslnFormat.ControlPointWithMap, table.Format);
        Assert.AreEqual((ushort)100, table.StandardGlyph);
        Assert.AreEqual((ushort)3,   table.ControlPoints[BslnTable.RomanBaseline]);
        Assert.AreEqual(BslnTable.NoControlPoint, table.ControlPoints[BslnTable.MathBaseline]);
        Assert.AreEqual(2, table.GlyphBaselineMap.Count);
        Assert.AreEqual(BslnTable.IdeographicCenteredBaseline, table.GlyphBaselineMap[50]);
        Assert.AreEqual(BslnTable.IdeographicLowBaseline,      table.GlyphBaselineMap[60]);
    }

    // ── Test 5 — ReadData with AAT lookup Format 8 (trimmed array) ─────────

    [TestMethod]
    public void ReadData_Format8Lookup_ParsesGlyphRange()
    {
        // bsln Format 1 (distance + map) whose mapping uses AAT lookup Format 8 (trimmed array).
        // Header:  version=0x00010000, format=1, defaultBaseline=0   (8 bytes)
        // Deltas:  all zero                                           (64 bytes)
        // Lookup8: format=8, firstGlyph=10, glyphCount=3             (6 bytes)
        //          value[10]=0, value[11]=1, value[12]=2             (6 bytes)
        // Total:   84 bytes

        byte[] data = new byte[84];
        // Header
        data[0] = 0x00; data[1] = 0x01; data[2] = 0x00; data[3] = 0x00; // version
        data[4] = 0x00; data[5] = 0x01;                                   // format = 1
        data[6] = 0x00; data[7] = 0x00;                                   // defaultBaseline = 0
        // bytes 8-71: deltas, all zero (already initialised to 0)
        // Lookup Format 8 at offset 72
        data[72] = 0x00; data[73] = 0x08;   // lookupFormat = 8
        data[74] = 0x00; data[75] = 0x0A;   // firstGlyph = 10
        data[76] = 0x00; data[77] = 0x03;   // glyphCount = 3
        data[78] = 0x00; data[79] = 0x00;   // glyph 10 → baseline 0
        data[80] = 0x00; data[81] = 0x01;   // glyph 11 → baseline 1
        data[82] = 0x00; data[83] = 0x02;   // glyph 12 → baseline 2

        var table = new BslnTable();
        table.ReadData(MakeReader(data));

        Assert.AreEqual(BslnTable.BslnFormat.DistanceWithMap, table.Format);
        Assert.AreEqual(3, table.GlyphBaselineMap.Count);
        Assert.AreEqual((ushort)BslnTable.RomanBaseline,              table.GlyphBaselineMap[10]);
        Assert.AreEqual((ushort)BslnTable.IdeographicCenteredBaseline, table.GlyphBaselineMap[11]);
        Assert.AreEqual((ushort)BslnTable.IdeographicLowBaseline,      table.GlyphBaselineMap[12]);
    }
}
