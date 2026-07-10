using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class KernTableTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    private static byte[] BuildKernBytes((ushort left, ushort right, short value)[] pairs)
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);
        w.Write<ushort>(0); // version
        w.Write<ushort>(1); // nTables
        w.Write<ushort>(0); // subVersion
        w.Write<ushort>((ushort)(14 + pairs.Length * 6)); // subtable length
        w.Write<ushort>(0); // coverage
        w.Write<ushort>((ushort)pairs.Length); // nPairs
        w.Write<ushort>(0); // searchRange (unused by ReadData)
        w.Write<ushort>(0); // entrySelector (unused by ReadData)
        w.Write<ushort>(0); // rangeShift (unused by ReadData)
        foreach (var (left, right, value) in pairs)
        {
            w.Write<ushort>(left);
            w.Write<ushort>(right);
            w.Write<short>(value);
        }
        return ms.ToArray();
    }

    [TestMethod]
    public void RoundTrip_PreservesKerningPairs()
    {
        (ushort left, ushort right, short value)[] pairs =
        [
            (65, 86, -50), // "AV"
            (65, 87, -30), // "AW"
        ];
        byte[] original = BuildKernBytes(pairs);

        var table = new KernTable();
        table.ReadData(MakeReader(original));

        Assert.AreEqual(-50f, table.GetSpacingCorrection((char)65, (char)86));
        Assert.AreEqual(-30f, table.GetSpacingCorrection((char)65, (char)87));
        Assert.AreEqual(0f, table.GetSpacingCorrection((char)65, (char)88)); // no pair -> no correction

        using var outMs = new MemoryStream();
        var outWriter = new Writer(outMs, BigEndianWriter.WriterDelegates);
        table.WriteData(outWriter);

        Assert.AreEqual(table.Length, outMs.Position);

        // Re-read the freshly written bytes and check the pairs survive a second round-trip
        // (the pair iteration order is not guaranteed, so compare by value rather than by bytes).
        var reread = new KernTable();
        reread.ReadData(MakeReader(outMs.ToArray()));
        Assert.AreEqual(-50f, reread.GetSpacingCorrection((char)65, (char)86));
        Assert.AreEqual(-30f, reread.GetSpacingCorrection((char)65, (char)87));
    }

    [TestMethod]
    public void RoundTrip_NoPairs()
    {
        // searchRange/entrySelector/rangeShift are recomputed by WriteData from the pair count
        // rather than preserved, so this only checks the parts ReadData actually consumes: the
        // pair count and the pairs themselves (there are none here).
        byte[] original = BuildKernBytes([]);
        var table = new KernTable();
        table.ReadData(MakeReader(original));

        Assert.AreEqual(0f, table.GetSpacingCorrection((char)65, (char)86));
        Assert.AreEqual(TableHeaderAndSubtableHeaderSize, table.Length);

        using var outMs = new MemoryStream();
        var outWriter = new Writer(outMs, BigEndianWriter.WriterDelegates);
        table.WriteData(outWriter);

        Assert.AreEqual(table.Length, outMs.Position);

        var reread = new KernTable();
        reread.ReadData(MakeReader(outMs.ToArray()));
        Assert.AreEqual(0f, reread.GetSpacingCorrection((char)65, (char)86));
    }

    private const int TableHeaderAndSubtableHeaderSize = 4 + 14;
}
