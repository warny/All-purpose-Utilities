using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts;
using Utils.Fonts.TTF;
using Utils.Fonts.TTF.Tables.Glyf;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class GlyphSimpleTests
{
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    // Builds the raw bytes for a single-contour simple glyph with 3 on-curve points, encoded as
    // deltas from (0,0): (0,0) -> (100,0) -> (100,-200). This deliberately exercises the three
    // coordinate encodings the format supports: a zero delta ("same"), a small positive delta
    // (byte + sign bit set), and a small negative delta (byte + sign bit clear).
    private static byte[] BuildTriangleGlyphBytes()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);

        w.Write<short>(1);      // numberOfContours
        w.Write<short>(0);      // xMin
        w.Write<short>(-200);   // yMin
        w.Write<short>(100);    // xMax
        w.Write<short>(0);      // yMax

        w.Write<short>(2);      // endPtsOfContours[0] = index of last point (0-based) -> 3 points
        w.Write<short>(0);      // instructionLength

        // Flags: P0 (0,0): same/same. P1 (dx=+100): byte+sign. P2 (dy=-200): byte, no sign.
        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsSame | OutlineFlags.OnCurve));
        w.WriteByte((byte)(OutlineFlags.XIsByte | OutlineFlags.XIsSame | OutlineFlags.YIsSame | OutlineFlags.OnCurve));
        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsByte | OutlineFlags.OnCurve));

        // X deltas: P0 none (same), P1 = 100 (byte), P2 none (same).
        w.WriteByte(100);
        // Y deltas: P0 none (same), P1 none (same), P2 = 200 (byte, sign bit clear => negative).
        w.WriteByte(200);

        return ms.ToArray();
    }

    [TestMethod]
    public void ReadThenWrite_ProducesIdenticalBytes()
    {
        byte[] original = BuildTriangleGlyphBytes();
        var glyph = (GlyphSimple)GlyphBase.CreateGlyf(MakeReader(original), null);

        var contour = glyph.Contours.Single().ToArray();
        Assert.AreEqual(3, contour.Length);
        Assert.AreEqual(new TTFPoint(0, 0, true), contour[0]);
        Assert.AreEqual(new TTFPoint(100, 0, true), contour[1]);
        Assert.AreEqual(new TTFPoint(100, -200, true), contour[2]);

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        glyph.WriteData(writer);
        byte[] rewritten = ms.ToArray();

        CollectionAssert.AreEqual(original, rewritten);
    }

    [TestMethod]
    public void WriteThenRead_RoundTripsThroughLargeDelta()
    {
        // A delta of 1000 does not fit in a byte, so this exercises the Int16 coordinate path
        // (XIsByte/YIsByte both clear) in both GetFlags and WriteData/ReadData.
        byte[] original = BuildTriangleGlyphBytesWithLargeDelta();
        var glyph = (GlyphSimple)GlyphBase.CreateGlyf(MakeReader(original), null);

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        glyph.WriteData(writer);

        var reread = (GlyphSimple)GlyphBase.CreateGlyf(MakeReader(ms.ToArray()), null);
        var contour = reread.Contours.Single().ToArray();
        Assert.AreEqual(new TTFPoint(0, 0, true), contour[0]);
        Assert.AreEqual(new TTFPoint(1000, 0, true), contour[1]);
        Assert.AreEqual(new TTFPoint(1000, -200, true), contour[2]);
    }

    // Regression test: WriteData used to write each contour's *local* last-point index
    // (contour.Length - 1) instead of the *cumulative* index (within the glyph's whole flattened
    // point array) that the endPtsOfContours format actually requires -- correct by coincidence
    // for a glyph's first contour (whose local and cumulative indices are identical), but wrong for
    // every subsequent one. A two-contour glyph with lengths [2, 3] must produce endPtsOfContours
    // [1, 4], not [1, 2].
    [TestMethod]
    public void ReadThenWrite_TwoContours_ProducesCumulativeEndPoints()
    {
        byte[] original = BuildTwoContourGlyphBytes();
        var glyph = (GlyphSimple)GlyphBase.CreateGlyf(MakeReader(original), null);

        var contours = glyph.Contours.Select(c => c.ToArray()).ToArray();
        Assert.AreEqual(2, contours.Length);
        Assert.AreEqual(2, contours[0].Length);
        Assert.AreEqual(3, contours[1].Length);
        Assert.AreEqual(new TTFPoint(0, 0, true), contours[0][0]);
        Assert.AreEqual(new TTFPoint(50, 0, true), contours[0][1]);
        Assert.AreEqual(new TTFPoint(50, 50, true), contours[1][0]);
        Assert.AreEqual(new TTFPoint(100, 50, true), contours[1][1]);
        Assert.AreEqual(new TTFPoint(100, 100, true), contours[1][2]);

        using var ms = new MemoryStream();
        var writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        glyph.WriteData(writer);

        CollectionAssert.AreEqual(original, ms.ToArray());
    }

    private static byte[] BuildTwoContourGlyphBytes()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);

        w.Write<short>(2);      // numberOfContours
        w.Write<short>(0);      // xMin
        w.Write<short>(0);      // yMin
        w.Write<short>(100);    // xMax
        w.Write<short>(100);    // yMax

        // Cumulative (not per-contour) last-point index: contour 0 has 2 points (indices 0-1),
        // contour 1 has 3 points (indices 2-4).
        w.Write<short>(1);      // endPtsOfContours[0]
        w.Write<short>(4);      // endPtsOfContours[1]
        w.Write<short>(0);      // instructionLength

        var same = OutlineFlags.OnCurve;
        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsSame | same));                    // P0 (0,0)
        w.WriteByte((byte)(OutlineFlags.XIsByte | OutlineFlags.XIsSame | OutlineFlags.YIsSame | same)); // P1 dx=+50
        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsByte | OutlineFlags.YIsSame | same)); // P2 dy=+50
        w.WriteByte((byte)(OutlineFlags.XIsByte | OutlineFlags.XIsSame | OutlineFlags.YIsSame | same)); // P3 dx=+50
        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsByte | OutlineFlags.YIsSame | same)); // P4 dy=+50

        w.WriteByte(50); // X delta for P1
        w.WriteByte(50); // X delta for P3
        w.WriteByte(50); // Y delta for P2
        w.WriteByte(50); // Y delta for P4

        return ms.ToArray();
    }

    private static byte[] BuildTriangleGlyphBytesWithLargeDelta()
    {
        using var ms = new MemoryStream();
        var w = new Writer(ms, BigEndianWriter.WriterDelegates);

        w.Write<short>(1);      // numberOfContours
        w.Write<short>(0);      // xMin
        w.Write<short>(-200);   // yMin
        w.Write<short>(1000);   // xMax
        w.Write<short>(0);      // yMax

        w.Write<short>(2);      // endPtsOfContours[0]
        w.Write<short>(0);      // instructionLength

        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsSame | OutlineFlags.OnCurve));
        w.WriteByte((byte)(OutlineFlags.YIsSame | OutlineFlags.OnCurve)); // X needs Int16 (dx=1000)
        w.WriteByte((byte)(OutlineFlags.XIsSame | OutlineFlags.YIsByte | OutlineFlags.OnCurve));

        w.Write<short>(1000); // X delta for P1 (Int16, since XIsByte is clear)
        w.WriteByte(200);     // Y delta for P2 (byte, sign bit clear => negative)

        return ms.ToArray();
    }
}
