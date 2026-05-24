using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

/// <summary>
/// Tests for the simpler AAT tables: fmtx, fdsc, feat, prop, opbd, lcar, trak.
/// </summary>
[TestClass]
public class AatTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static byte[] Serialize<T>(T table) where T : TrueTypeTable
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        table.WriteData(writer);
        return ms.ToArray();
    }

    // ── fmtx ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Fmtx_RoundTrip()
    {
        var source = new FmtxTable
        {
            Version           = 0x00020000,
            GlyphIndex        = 42,
            HorizontalBefore  = 2,
            HorizontalAfter   = 3,
            HorizontalCaretHead = 4,
            HorizontalCaretBase = 5,
            VerticalBefore    = 6,
            VerticalAfter     = 7,
            VerticalCaretHead = 8,
            VerticalCaretBase = 9,
        };

        Assert.AreEqual(16, source.Length);
        byte[] bytes = Serialize(source);
        Assert.AreEqual(16, bytes.Length);

        var table = new FmtxTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(42u,  table.GlyphIndex);
        Assert.AreEqual(2,    table.HorizontalBefore);
        Assert.AreEqual(7,    table.VerticalAfter);
        Assert.AreEqual(9,    table.VerticalCaretBase);
    }

    // ── fdsc ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Fdsc_RoundTrip()
    {
        var source = new FdscTable
        {
            Descriptors =
            [
                new FdscTable.FontDescriptor { Tag = "wght", Value = FdscTable.DoubleToFixed(400.0) },
                new FdscTable.FontDescriptor { Tag = "wdth", Value = FdscTable.DoubleToFixed(100.0) },
            ]
        };

        // 8 + 2×8 = 24
        Assert.AreEqual(24, source.Length);
        byte[] bytes = Serialize(source);
        Assert.AreEqual(24, bytes.Length);

        var table = new FdscTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(2, table.Descriptors.Length);
        Assert.AreEqual("wght", table.Descriptors[0].Tag);
        Assert.AreEqual(FdscTable.DoubleToFixed(400.0), table.Descriptors[0].Value);
        Assert.AreEqual("wdth", table.Descriptors[1].Tag);
    }

    // ── feat ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Feat_RoundTrip()
    {
        var source = new FeatTable
        {
            Features =
            [
                new FeatTable.FeatureNameRecord
                {
                    Feature      = 1,
                    FeatureFlags = 0,
                    NameIndex    = 256,
                    Settings     =
                    [
                        new FeatTable.FeatureSetting { Setting = 0, NameIndex = 257 },
                        new FeatTable.FeatureSetting { Setting = 1, NameIndex = 258 },
                    ],
                },
                new FeatTable.FeatureNameRecord
                {
                    Feature      = 3,
                    FeatureFlags = 0x8000,  // exclusive
                    NameIndex    = 259,
                    Settings     =
                    [
                        new FeatTable.FeatureSetting { Setting = 0, NameIndex = 260 },
                    ],
                },
            ]
        };

        // 12 (header) + 2×12 (feature records) + 2×4 + 1×4 (settings) = 12+24+12 = 48
        Assert.AreEqual(48, source.Length);
        byte[] bytes = Serialize(source);
        Assert.AreEqual(48, bytes.Length);

        var table = new FeatTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(2, table.Features.Length);
        Assert.AreEqual((ushort)1,    table.Features[0].Feature);
        Assert.AreEqual((short)256,   table.Features[0].NameIndex);
        Assert.AreEqual(2,            table.Features[0].Settings.Length);
        Assert.AreEqual((ushort)0,    table.Features[0].Settings[0].Setting);
        Assert.AreEqual((ushort)3,    table.Features[1].Feature);
        Assert.AreEqual((ushort)0x8000, table.Features[1].FeatureFlags);
        Assert.AreEqual(1,            table.Features[1].Settings.Length);
    }

    // ── prop ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Prop_Format0_RoundTrip()
    {
        var source = new PropTable
        {
            Format      = 0,
            DefaultProp = 0x0000,   // strong L→R for all glyphs
        };

        Assert.AreEqual(8, source.Length);
        byte[] bytes = Serialize(source);
        Assert.AreEqual(8, bytes.Length);

        var table = new PropTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual((ushort)0, table.Format);
        Assert.AreEqual((ushort)0x0000, table.DefaultProp);
        Assert.AreEqual(0, table.GlyphProperties.Count);
    }

    [TestMethod]
    public void Prop_Format1_WithLookup_RoundTrip()
    {
        var source = new PropTable
        {
            Format      = 1,
            DefaultProp = 0x0000,
            GlyphProperties = new Dictionary<ushort, ushort>
            {
                [10] = 0x4000,   // strong R→L
                [20] = 0x2000,   // hangs on right
            }
        };

        byte[] bytes = Serialize(source);

        var table = new PropTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual((ushort)1, table.Format);
        Assert.AreEqual(2, table.GlyphProperties.Count);
        Assert.AreEqual((ushort)0x4000, table.GlyphProperties[10]);
        Assert.AreEqual((ushort)0x2000, table.GlyphProperties[20]);
    }

    // ── opbd ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Opbd_RoundTrip()
    {
        var source = new OpbdTable
        {
            Format = 0,
            GlyphBounds = new Dictionary<ushort, OpbdTable.OpticalBounds>
            {
                [5]  = new OpbdTable.OpticalBounds(-10, 0, 5, 0),
                [12] = new OpbdTable.OpticalBounds(  0, 0, 8, 0),
            }
        };

        byte[] bytes = Serialize(source);

        var table = new OpbdTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(2, table.GlyphBounds.Count);
        Assert.AreEqual(new OpbdTable.OpticalBounds(-10, 0, 5, 0), table.GlyphBounds[5]);
        Assert.AreEqual(new OpbdTable.OpticalBounds(  0, 0, 8, 0), table.GlyphBounds[12]);
    }

    // ── lcar ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Lcar_RoundTrip()
    {
        var source = new LcarTable
        {
            Format = 0,
            GlyphCarets = new Dictionary<ushort, short[]>
            {
                [100] = [300, 600],        // fi ligature: 2 components → 1 caret... but spec says n-1 carets for n-component, so 2 carets = 3-component
                [200] = [400],             // 2-component ligature: 1 caret
            }
        };

        byte[] bytes = Serialize(source);

        var table = new LcarTable();
        table.ReadData(MakeReader(bytes));

        Assert.AreEqual(2, table.GlyphCarets.Count);
        CollectionAssert.AreEqual(new short[] { 300, 600 }, table.GlyphCarets[100]);
        CollectionAssert.AreEqual(new short[] { 400 },       table.GlyphCarets[200]);
    }

    // ── trak ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Trak_HorizontalOnly_RoundTrip()
    {
        // 1 tracking level (normal = 0), 2 sizes (12pt and 24pt in 16.16 fixed)
        var source = new TrakTable
        {
            HorizData = new TrakTable.TrackData
            {
                Sizes = [0x000C0000, 0x00180000],  // 12.0 and 24.0 in Fixed
                Entries =
                [
                    new TrakTable.TrackEntry
                    {
                        Track         = 0,            // normal tracking
                        NameIndex     = 300,
                        PerSizeValues = [0, -20],      // tighter at 24pt
                    },
                ],
            },
        };

        byte[] bytes = Serialize(source);

        var table = new TrakTable();
        table.ReadData(MakeReader(bytes));

        Assert.IsNotNull(table.HorizData);
        Assert.IsNull(table.VertData);
        Assert.AreEqual(1, table.HorizData.Entries.Length);
        Assert.AreEqual(2, table.HorizData.Sizes.Length);
        Assert.AreEqual(0x000C0000, table.HorizData.Sizes[0]);
        Assert.AreEqual(0x00180000, table.HorizData.Sizes[1]);
        Assert.AreEqual(0,    table.HorizData.Entries[0].Track);
        Assert.AreEqual((ushort)300, table.HorizData.Entries[0].NameIndex);
        Assert.AreEqual((short)0,   table.HorizData.Entries[0].PerSizeValues[0]);
        Assert.AreEqual((short)-20, table.HorizData.Entries[0].PerSizeValues[1]);
    }

    [TestMethod]
    public void Trak_BothDirections_RoundTrip()
    {
        var source = new TrakTable
        {
            HorizData = new TrakTable.TrackData
            {
                Sizes   = [0x000C0000],
                Entries = [new TrakTable.TrackEntry { Track = 0, NameIndex = 300, PerSizeValues = [10] }],
            },
            VertData = new TrakTable.TrackData
            {
                Sizes   = [0x000C0000],
                Entries = [new TrakTable.TrackEntry { Track = 0, NameIndex = 301, PerSizeValues = [5] }],
            },
        };

        byte[] bytes = Serialize(source);
        var table = new TrakTable();
        table.ReadData(MakeReader(bytes));

        Assert.IsNotNull(table.HorizData);
        Assert.IsNotNull(table.VertData);
        Assert.AreEqual((short)10, table.HorizData.Entries[0].PerSizeValues[0]);
        Assert.AreEqual((short)5,  table.VertData.Entries[0].PerSizeValues[0]);
    }
}
