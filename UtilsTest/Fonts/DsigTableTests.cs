using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class DsigTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static (DsigTable table, byte[] bytes) RoundTrip(DsigTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new DsigTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — Empty signature blob ─────────────────────────────────────

    [TestMethod]
    public void EmptyBlob_RoundTrip()
    {
        var source = new DsigTable
        {
            DsigVersion   = 1,
            NumSigs       = 0,
            Flags         = 1,
            SignatureData = [],
        };

        // 8-byte header only
        Assert.AreEqual(8, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(8, bytes.Length);
        Assert.AreEqual(1u,  table.DsigVersion);
        Assert.AreEqual(0,   table.SignatureData.Length);
    }

    // ── Test 2 — Opaque blob is preserved exactly ─────────────────────────

    [TestMethod]
    public void OpaqueBlob_PreservedExactly()
    {
        byte[] blob = [0x01, 0x02, 0x03, 0x04, 0xAB, 0xCD, 0xEF];

        var source = new DsigTable
        {
            DsigVersion   = 1,
            NumSigs       = 1,
            Flags         = 1,
            SignatureData = blob,
        };

        Assert.AreEqual(15, source.Length);  // 8 + 7

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(15, bytes.Length);
        Assert.AreEqual(1u, table.DsigVersion);
        Assert.AreEqual(7,  table.SignatureData.Length);
        CollectionAssert.AreEqual(blob, table.SignatureData);
    }

    // ── Test 3 — Binary decode of minimal DSIG header ────────────────────

    [TestMethod]
    public void ReadData_FromBinary_ParsesHeader()
    {
        byte[] data =
        [
            0x00, 0x00, 0x00, 0x01,  // version = 1
            0x00, 0x02,              // numSigs = 2
            0x00, 0x01,              // flags = 1
            0xDE, 0xAD, 0xBE, 0xEF, // signature data (opaque)
        ];

        var table = new DsigTable();
        table.ReadData(MakeReader(data));

        Assert.AreEqual(1u,  table.DsigVersion);
        Assert.AreEqual((ushort)2, table.NumSigs);
        Assert.AreEqual((ushort)1, table.Flags);
        Assert.AreEqual(4,   table.SignatureData.Length);
        Assert.AreEqual(0xDE, table.SignatureData[0]);
        Assert.AreEqual(0xEF, table.SignatureData[3]);
    }
}
