using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class AcntTableTests
{
    // Big-endian reader/writer delegates matching TrueType font encoding.
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
    {
        return new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);
    }

    private static (AcntTable table, byte[] bytes) RoundTrip(AcntTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = new AcntTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // ── Test 1 — ReadData Format 0 (single secondary component) ───────────

    [TestMethod]
    public void ReadData_Format0_SingleEntry()
    {
        // 1 accented glyph (index 100), 1 Format-0 description, 1 secondary entry.
        // Binary layout (27 bytes, big-endian):
        //   header: version=0x00010000, first=100, last=100, descOff=20, extOff=24, secOff=24
        //   desc:   word0=0x0032(primary=50,fmt0), att=1, secIdx=0
        //   sec:    glyph=200, att=2
        byte[] data =
        [
            0x00, 0x01, 0x00, 0x00,  // version = 0x00010000
            0x00, 0x64, 0x00, 0x64,  // first=100, last=100
            0x00, 0x00, 0x00, 0x14,  // descOffset = 20
            0x00, 0x00, 0x00, 0x18,  // extOffset  = 24  (no extensions)
            0x00, 0x00, 0x00, 0x18,  // secOffset  = 24
            0x00, 0x32, 0x01, 0x00,  // desc: primary=50, att=1, secIdx=0
            0x00, 0xC8, 0x02         // secondary: glyph=200, att=2
        ];

        var table = new AcntTable();
        table.ReadData(MakeReader(data));

        Assert.AreEqual(1, table.Descriptions.Length);
        var single = (AcntTable.AccentDescription.Single)table.Descriptions[0];
        Assert.AreEqual((ushort)50, single.PrimaryGlyphIndex);
        Assert.AreEqual(1, single.PrimaryAttachmentPoint);
        Assert.AreEqual(0, single.SecondaryInfoIndex);
        Assert.AreEqual(1, table.SecondaryEntries.Length);
        Assert.AreEqual((ushort)200, table.SecondaryEntries[0].SecondaryGlyphIndex);
        Assert.AreEqual(2, table.SecondaryEntries[0].AttachmentPoint);
    }

    // ── Test 2 — ReadData Format 1 (multiple secondary components) ────────

    [TestMethod]
    public void ReadData_Format1_MultipleExtensions()
    {
        // 1 accented glyph (index 200), 1 Format-1 description with 2 extension entries,
        // 2 secondary entries.
        // Binary layout (36 bytes, big-endian):
        //   header: version=0x00010000, first=200, last=200, descOff=20, extOff=26, secOff=30
        //   desc:   word0=0x804B(fmt1,primary=75), extOffset=26
        //   ext[0]: 0x0001  (not last, secIdx=0, att=1)
        //   ext[1]: 0x8102  (last,     secIdx=1, att=2)
        //   sec[0]: glyph=300, att=3
        //   sec[1]: glyph=301, att=4
        byte[] data =
        [
            0x00, 0x01, 0x00, 0x00,        // version = 0x00010000
            0x00, 0xC8, 0x00, 0xC8,        // first=200, last=200
            0x00, 0x00, 0x00, 0x14,        // descOffset = 20
            0x00, 0x00, 0x00, 0x1A,        // extOffset  = 26
            0x00, 0x00, 0x00, 0x1E,        // secOffset  = 30
            0x80, 0x4B, 0x00, 0x00, 0x00, 0x1A,  // desc: fmt1, primary=75, extOff=26
            0x00, 0x01,                    // ext[0]: secIdx=0, att=1
            0x81, 0x02,                    // ext[1]: last, secIdx=1, att=2
            0x01, 0x2C, 0x03,              // secondary[0]: glyph=300, att=3
            0x01, 0x2D, 0x04               // secondary[1]: glyph=301, att=4
        ];

        var table = new AcntTable();
        table.ReadData(MakeReader(data));

        Assert.AreEqual(1, table.Descriptions.Length);
        var multiple = (AcntTable.AccentDescription.Multiple)table.Descriptions[0];
        Assert.AreEqual((ushort)75, multiple.PrimaryGlyphIndex);
        Assert.AreEqual(2, multiple.Extensions.Count);
        Assert.AreEqual(0, multiple.Extensions[0].SecondaryInfoIndex);
        Assert.AreEqual(1, multiple.Extensions[0].PrimaryAttachmentPoint);
        Assert.AreEqual(1, multiple.Extensions[1].SecondaryInfoIndex);
        Assert.AreEqual(2, multiple.Extensions[1].PrimaryAttachmentPoint);
        Assert.AreEqual(2, table.SecondaryEntries.Length);
        Assert.AreEqual((ushort)300, table.SecondaryEntries[0].SecondaryGlyphIndex);
        Assert.AreEqual(3, table.SecondaryEntries[0].AttachmentPoint);
        Assert.AreEqual((ushort)301, table.SecondaryEntries[1].SecondaryGlyphIndex);
        Assert.AreEqual(4, table.SecondaryEntries[1].AttachmentPoint);
    }

    // ── Test 3 — RoundTrip (WriteData → ReadData) ─────────────────────────

    [TestMethod]
    public void WriteData_ThenReadData_RoundTrip()
    {
        var source = new AcntTable
        {
            FirstAccentGlyphIndex = 10,
            LastAccentGlyphIndex = 11,
            Descriptions =
            [
                new AcntTable.AccentDescription.Single(5, 2, 0),
                new AcntTable.AccentDescription.Multiple(15,
                [
                    new AcntTable.ExtensionEntry(0, 3),
                    new AcntTable.ExtensionEntry(1, 4)
                ])
            ],
            SecondaryEntries =
            [
                new AcntTable.SecondaryEntry(100, 5),
                new AcntTable.SecondaryEntry(200, 6)
            ]
        };

        // Verify expected Length before round-trip
        // 20 header + 4 (Single) + 6 (Multiple) + 4 (2 ext * 2 bytes) + 6 (2 sec * 3 bytes) = 40
        Assert.AreEqual(40, source.Length);

        var (table, _) = RoundTrip(source);

        Assert.AreEqual(source.Version, table.Version);
        Assert.AreEqual(source.FirstAccentGlyphIndex, table.FirstAccentGlyphIndex);
        Assert.AreEqual(source.LastAccentGlyphIndex, table.LastAccentGlyphIndex);
        Assert.AreEqual(2, table.Descriptions.Length);

        var single = (AcntTable.AccentDescription.Single)table.Descriptions[0];
        Assert.AreEqual((ushort)5, single.PrimaryGlyphIndex);
        Assert.AreEqual(2, single.PrimaryAttachmentPoint);
        Assert.AreEqual(0, single.SecondaryInfoIndex);

        var multiple = (AcntTable.AccentDescription.Multiple)table.Descriptions[1];
        Assert.AreEqual((ushort)15, multiple.PrimaryGlyphIndex);
        Assert.AreEqual(2, multiple.Extensions.Count);
        Assert.AreEqual(0, multiple.Extensions[0].SecondaryInfoIndex);
        Assert.AreEqual(3, multiple.Extensions[0].PrimaryAttachmentPoint);
        Assert.AreEqual(1, multiple.Extensions[1].SecondaryInfoIndex);
        Assert.AreEqual(4, multiple.Extensions[1].PrimaryAttachmentPoint);

        Assert.AreEqual(2, table.SecondaryEntries.Length);
        Assert.AreEqual((ushort)100, table.SecondaryEntries[0].SecondaryGlyphIndex);
        Assert.AreEqual(5, table.SecondaryEntries[0].AttachmentPoint);
        Assert.AreEqual((ushort)200, table.SecondaryEntries[1].SecondaryGlyphIndex);
        Assert.AreEqual(6, table.SecondaryEntries[1].AttachmentPoint);
    }

    // ── Test 4 — Empty range (last < first) ───────────────────────────────

    [TestMethod]
    public void ReadData_EmptyRange_NoEntriesRead()
    {
        // last=99 < first=100 → count=0, nothing should be read.
        byte[] data =
        [
            0x00, 0x01, 0x00, 0x00,  // version = 0x00010000
            0x00, 0x64, 0x00, 0x63,  // first=100, last=99
            0x00, 0x00, 0x00, 0x14,  // descOffset = 20
            0x00, 0x00, 0x00, 0x14,  // extOffset  = 20
            0x00, 0x00, 0x00, 0x14   // secOffset  = 20
        ];

        var table = new AcntTable();
        table.ReadData(MakeReader(data));

        Assert.AreEqual(0, table.Descriptions.Length);
        Assert.AreEqual(0, table.SecondaryEntries.Length);
    }
}
