using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Parsing;
using Utils.Fonts.TTF.Tables;
using Utils.Fonts.TTF.Tables.CMap;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class CmapTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static CmapTable NewTable() => (CmapTable)new TrueTypeFont(0).CreateTable(TableTypes.CMAP);

    private static (CmapTable table, byte[] bytes) RoundTrip(CmapTable source)
    {
        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();
        var table = NewTable();
        table.ReadData(MakeReader(bytes));
        return (table, bytes);
    }

    // Regression test: CmapTable.ReadData used to compute each subtable's length from the
    // *previous* subtable's offset instead of the *next* one, so with more than one subtable the
    // first subtable's length was wrongly set to its own file offset (a small number, far below
    // the 262 bytes a format-0 subtable needs), causing it to be silently skipped as malformed.
    [TestMethod]
    public void MultipleSubtables_AllSurviveRoundTrip()
    {
        var source = NewTable();
        var mac = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        mac.SetMap((byte)'A', 10);
        var windows = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        windows.SetMap((byte)'A', 20);

        source.AddCMap(1, 0, mac);     // Macintosh Roman
        source.AddCMap(3, 1, windows); // Windows Unicode BMP

        var (table, _) = RoundTrip(source);

        Assert.AreEqual((ushort)2, table.NumberSubtables);

        var readMac = (CMapFormat0)table.GetCMap(1, 0);
        var readWindows = (CMapFormat0)table.GetCMap(3, 1);
        Assert.IsNotNull(readMac);
        Assert.IsNotNull(readWindows);
        Assert.AreEqual((short)10, readMac.Map('A'));
        Assert.AreEqual((short)20, readWindows.Map('A'));
    }

    [TestMethod]
    public void SingleSubtable_RoundTrip()
    {
        var source = NewTable();
        var mac = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        mac.SetMap((byte)'Z', 42);
        source.AddCMap(1, 0, mac);

        var (table, _) = RoundTrip(source);

        Assert.AreEqual((ushort)1, table.NumberSubtables);
        var read = (CMapFormat0)table.GetCMap(1, 0);
        Assert.IsNotNull(read);
        Assert.AreEqual((short)42, read.Map('Z'));
    }

    // TODO-pass2 item 34: numberSubtables is unsigned and bounded before any allocation.
    [TestMethod]
    public void NumberSubtables_ExceedingLimit_Throws()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0); // version
        w.Write<ushort>(2000); // numberSubtables, far above the default MaximumCmapSubtables (1024)

        var table = NewTable();
        Assert.ThrowsExactly<FontParseException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    [TestMethod]
    public void TruncatedSubtableDirectory_Throws()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0);
        w.Write<ushort>(5); // declares 5 records (40 bytes) but the table ends right after the count

        var table = NewTable();
        Assert.ThrowsExactly<FontParseException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    // Offset/malformed-subtable anomalies are policy-dependent (strict/permissive): a CmapTable
    // read directly, outside TrueTypeFont.ParseFont's parsing context (as in these tests, and as
    // any caller that only has a Reader and no TrueTypeFontParsingOptions), always throws a plain
    // InvalidDataException -- there is no strict/permissive mode to consult. Going through
    // TrueTypeFont.ParseFont instead surfaces the same anomalies as a strict-mode FontParseException
    // or a permissive-mode diagnostic; see TrueTypeFontDirectoryTests for that end-to-end coverage.
    [TestMethod]
    public void SubtableOffset_OutsideCmapTable_Throws()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0);
        w.Write<ushort>(1);
        w.Write<ushort>(3); w.Write<ushort>(1); // platformID/platformSpecificID
        w.Write<uint>(1_000_000); // offset far beyond the table's own length

        var table = NewTable();
        Assert.ThrowsExactly<InvalidDataException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    [TestMethod]
    public void SubtableOffset_InsideDirectory_Throws()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0);
        w.Write<ushort>(1);
        w.Write<ushort>(3); w.Write<ushort>(1);
        w.Write<uint>(2); // inside the 12-byte directory (4 + 1*8) itself

        var table = NewTable();
        Assert.ThrowsExactly<InvalidDataException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    [TestMethod]
    public void MalformedSubtable_Throws()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0);
        w.Write<ushort>(1);
        w.Write<ushort>(3); w.Write<ushort>(1);
        w.Write<uint>(12); // directoryEnd for 1 subtable
        w.Write<short>(999); // unsupported cmap subtable format
        w.Write<short>(0);
        w.Write<short>(0);

        var table = NewTable();
        Assert.ThrowsExactly<InvalidDataException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    // Regression test: a format-4 subtable whose own header declares a length consistent with its
    // slice (so CMapFormatBase.GetMap's length check passes), but whose internal segCountX2 implies
    // more segment data than actually fits in that slice, used to make ReadData's inner Reader.Read
    // calls throw an uncaught EndOfStreamException (not in the CmapTable.ReadData catch filter),
    // instead of being reported as a MalformedCmapSubtable diagnostic like every other malformed
    // subtable shape.
    [TestMethod]
    public void TruncatedFormat4Payload_IsReportedAsMalformed_NotUncaughtEndOfStream()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0);
        w.Write<ushort>(1);
        w.Write<ushort>(3); w.Write<ushort>(1);
        w.Write<uint>(12); // directoryEnd for 1 subtable; subtable slice is exactly 14 bytes (offset 12..26)
        // Format 4 header: format, length (== the whole 14-byte slice), language.
        w.Write<short>(4); w.Write<short>(14); w.Write<short>(0);
        // segCountX2=4 (segCount=2), searchRange/entrySelector/rangeShift: 8 bytes, filling the
        // declared 14-byte slice exactly -- leaving no room for the endCode[2] array ReadData
        // requires next, so the very next read runs off the end of the subtable's bounded slice.
        w.Write<short>(4); w.Write<short>(0); w.Write<short>(0); w.Write<short>(0);

        var table = NewTable();
        Assert.ThrowsExactly<InvalidDataException>(() => table.ReadData(MakeReader(ms.ToArray())));
    }

    // TODO-pass2 item 15.3: two platform/encoding records legitimately sharing the same offset must
    // be parsed once and share the same subtable instance, not be parsed (and materialized) twice.
    [TestMethod]
    public void SharedOffset_ParsedOnce_SameInstanceForBothRecords()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0);
        w.Write<ushort>(2);
        w.Write<ushort>(1); w.Write<ushort>(0); w.Write<uint>(20); // Macintosh -> shared subtable
        w.Write<ushort>(3); w.Write<ushort>(1); w.Write<uint>(20); // Windows -> same shared subtable
        // Format 0 subtable at offset 20 (directoryEnd = 4 + 2*8 = 20).
        w.Write<short>(0); w.Write<short>(262); w.Write<short>(0);
        for (int i = 0; i < 256; i++) w.WriteByte((byte)i);

        var table = NewTable();
        table.ReadData(MakeReader(ms.ToArray()));

        var mac = table.GetCMap(1, 0);
        var win = table.GetCMap(3, 1);
        Assert.IsNotNull(mac);
        Assert.AreSame(mac, win);
    }

    // TODO-pass2 item 36: CMaps must not let a caller observe a mutation made after the snapshot
    // was taken -- each call reflects the state at call time, and the returned list itself cannot
    // be appended to or cleared by the caller (IReadOnlyList<T> has no such members).
    [TestMethod]
    public void CMaps_SnapshotDoesNotReflectLaterMutation()
    {
        var table = NewTable();
        var mac = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        table.AddCMap(1, 0, mac);

        var snapshot = table.CMaps;
        Assert.AreEqual(1, snapshot.Count);

        var windows = (CMapFormat0)CMapFormatBase.CreateCMap(0, 0);
        table.AddCMap(3, 1, windows);

        Assert.AreEqual(1, snapshot.Count); // the earlier snapshot is untouched
        Assert.AreEqual(2, table.CMaps.Count); // a fresh call observes the new state
    }
}
