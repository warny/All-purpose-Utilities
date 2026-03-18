using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class LtshTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (LtshTable table, byte[] bytes) RoundTrip(LtshTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new LtshTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — Empty glyph array ───────────────────────────────────────────

    [TestMethod]
    public void EmptyTable_RoundTrip()
    {
        var source = new LtshTable { Version = 0, YPels = [] };

        Assert.AreEqual(4, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(4, bytes.Length);
        Assert.AreEqual((ushort)0, table.Version);
        Assert.AreEqual(0, table.YPels.Length);
    }

    // ── Test 2 — Known yPels values preserved ────────────────────────────────

    [TestMethod]
    public void KnownValues_RoundTrip()
    {
        var source = new LtshTable
        {
            Version = 0,
            YPels   = [0, 1, 9, 12, 255, 0, 48],
        };

        // 4 header + 7 glyph bytes = 11
        Assert.AreEqual(11, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(11, bytes.Length);
        Assert.AreEqual(7, table.YPels.Length);
        Assert.AreEqual(0,   table.YPels[0]);
        Assert.AreEqual(1,   table.YPels[1]);
        Assert.AreEqual(9,   table.YPels[2]);
        Assert.AreEqual(12,  table.YPels[3]);
        Assert.AreEqual(255, table.YPels[4]);
        Assert.AreEqual(0,   table.YPels[5]);
        Assert.AreEqual(48,  table.YPels[6]);
    }

    // ── Test 3 — Binary decode of hand-crafted bytes ─────────────────────────

    [TestMethod]
    public void ReadData_FromBinary_ParsesCorrectly()
    {
        // version=0, numGlyphs=3, yPels=[1, 20, 255]
        byte[] data = [0x00, 0x00,   // version = 0
                       0x00, 0x03,   // numGlyphs = 3
                       0x01, 0x14, 0xFF];  // yPels

        var table = new LtshTable();
        table.ReadData(MakeReader(data));

        Assert.AreEqual((ushort)0, table.Version);
        Assert.AreEqual(3, table.YPels.Length);
        Assert.AreEqual(1,   table.YPels[0]);
        Assert.AreEqual(20,  table.YPels[1]);
        Assert.AreEqual(255, table.YPels[2]);
    }
}
