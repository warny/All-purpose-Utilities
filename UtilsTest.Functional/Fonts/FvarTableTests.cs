using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class FvarTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (FvarTable table, byte[] bytes) RoundTrip(FvarTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new FvarTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // F2Dot14 helper: 1.0 in 16.16 fixed-point
    private static int Fixed(double v) => FvarTable.DoubleToFixed(v);

    // ── Test 1 — Empty (no axes, no instances) ────────────────────────────

    [TestMethod]
    public void EmptyTable_RoundTrip()
    {
        var source = new FvarTable { Axes = [], Instances = [] };

        // Header only: 16 bytes
        Assert.AreEqual(16, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(16, bytes.Length);
        Assert.AreEqual((ushort)1, table.MajorVersion);
        Assert.AreEqual((ushort)0, table.MinorVersion);
        Assert.AreEqual(0, table.Axes.Length);
        Assert.AreEqual(0, table.Instances.Length);
    }

    // ── Test 2 — Two axes, two instances (no PostScript name) ────────────

    [TestMethod]
    public void TwoAxes_TwoInstances_RoundTrip()
    {
        // "wght" axis: 100–900, default 400
        // "wdth" axis: 75–125, default 100
        // Instance "Regular": wght=400, wdth=100
        // Instance "Bold":    wght=700, wdth=100

        var source = new FvarTable
        {
            Axes =
            [
                new FvarTable.VariationAxisRecord
                {
                    AxisTag      = "wght",
                    MinValue     = Fixed(100),
                    DefaultValue = Fixed(400),
                    MaxValue     = Fixed(900),
                    Flags        = 0,
                    AxisNameID   = 256,
                },
                new FvarTable.VariationAxisRecord
                {
                    AxisTag      = "wdth",
                    MinValue     = Fixed(75),
                    DefaultValue = Fixed(100),
                    MaxValue     = Fixed(125),
                    Flags        = 0,
                    AxisNameID   = 257,
                },
            ],
            Instances =
            [
                new FvarTable.InstanceRecord
                {
                    SubfamilyNameID = 258,
                    Coordinates     = [Fixed(400), Fixed(100)],
                    HasPostScriptName = false,
                },
                new FvarTable.InstanceRecord
                {
                    SubfamilyNameID = 259,
                    Coordinates     = [Fixed(700), Fixed(100)],
                    HasPostScriptName = false,
                },
            ]
        };

        // 16 (hdr) + 2×20 (axes) + 2×(4+2×4) (instances without PS name) = 16+40+24 = 80
        Assert.AreEqual(80, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(80, bytes.Length);
        Assert.AreEqual(2, table.Axes.Length);
        Assert.AreEqual(2, table.Instances.Length);

        Assert.AreEqual("wght", table.Axes[0].AxisTag);
        Assert.AreEqual(Fixed(400), table.Axes[0].DefaultValue);
        Assert.AreEqual((ushort)256, table.Axes[0].AxisNameID);

        Assert.AreEqual("wdth", table.Axes[1].AxisTag);
        Assert.AreEqual(Fixed(100), table.Axes[1].DefaultValue);

        Assert.AreEqual(Fixed(400), table.Instances[0].Coordinates[0]);
        Assert.AreEqual(Fixed(100), table.Instances[0].Coordinates[1]);
        Assert.AreEqual(Fixed(700), table.Instances[1].Coordinates[0]);

        Assert.IsFalse(table.Instances[0].HasPostScriptName);
    }

    // ── Test 3 — Instance with PostScript name ID ─────────────────────────

    [TestMethod]
    public void Instance_WithPostScriptName_RoundTrip()
    {
        var source = new FvarTable
        {
            Axes =
            [
                new FvarTable.VariationAxisRecord
                {
                    AxisTag = "wght", MinValue = Fixed(100),
                    DefaultValue = Fixed(400), MaxValue = Fixed(900),
                },
            ],
            Instances =
            [
                new FvarTable.InstanceRecord
                {
                    SubfamilyNameID  = 260,
                    Coordinates      = [Fixed(400)],
                    HasPostScriptName = true,
                    PostScriptNameID = 261,
                },
            ]
        };

        // 16 + 1×20 + 1×(4+1×4+2) = 16+20+10 = 46
        Assert.AreEqual(46, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(46, bytes.Length);
        Assert.IsTrue(table.Instances[0].HasPostScriptName);
        Assert.AreEqual((ushort)261, table.Instances[0].PostScriptNameID);
    }

    // ── Test 4 — FixedToDouble / DoubleToFixed helpers ───────────────────

    [TestMethod]
    public void FixedPointHelpers_RoundTrip()
    {
        Assert.AreEqual(0x00010000, FvarTable.DoubleToFixed(1.0));
        Assert.AreEqual(0x00020000, FvarTable.DoubleToFixed(2.0));
        Assert.AreEqual(0x00000000, FvarTable.DoubleToFixed(0.0));
        Assert.AreEqual(1.0,  FvarTable.FixedToDouble(0x00010000), 1e-9);
        Assert.AreEqual(0.5,  FvarTable.FixedToDouble(0x00008000), 1e-9);
        Assert.AreEqual(100.0, FvarTable.FixedToDouble(FvarTable.DoubleToFixed(100.0)), 1e-6);
    }
}
