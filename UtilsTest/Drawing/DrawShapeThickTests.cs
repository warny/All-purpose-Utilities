using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using Utils.Drawing;
using Utils.Imaging;

namespace UtilsTest.Drawing;

[TestClass]
public class DrawShapeThickTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class ArrayImage : IImageAccessor<int>
    {
        private readonly int[,] _data;
        public ArrayImage(int w, int h) { _data = new int[w, h]; Width = w; Height = h; }
        public int Width { get; }
        public int Height { get; }
        public int this[int x, int y] { get => _data[x, y]; set => _data[x, y] = value; }
        public int CountNonZero()
        {
            int n = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_data[x, y] != 0) n++;
            return n;
        }
    }

    private static DrawI<int> MakeCanvas(int w = 200, int h = 200) =>
        new DrawI<int>(new ArrayImage(w, h));

    // ── segment strip ────────────────────────────────────────────────────────

    [TestMethod]
    public void HorizontalSegment_PaintsSomePixels_WithSolidColor()
    {
        var canvas = MakeCanvas();
        var seg = new Segment(10f, 100f, 190f, 100f);
        canvas.DrawShapeThick(1, 6f, JoinStyle.Bevel, seg);
        var img = (ArrayImage)canvas.ImageAccessor;
        Assert.IsTrue(img.CountNonZero() > 0, "Expected pixels to be painted.");
    }

    [TestMethod]
    public void HorizontalSegment_WidthReflectedInPixelCount()
    {
        var img4 = new ArrayImage(200, 200);
        var img8 = new ArrayImage(200, 200);
        new DrawI<int>(img4).DrawShapeThick(1, 4f, JoinStyle.Bevel,
            new Segment(10f, 100f, 190f, 100f));
        new DrawI<int>(img8).DrawShapeThick(1, 8f, JoinStyle.Bevel,
            new Segment(10f, 100f, 190f, 100f));
        Assert.IsTrue(img8.CountNonZero() > img4.CountNonZero(),
            "Wider stroke should paint more pixels.");
    }

    [TestMethod]
    public void ColorFunction_ReceivesArcLengthAndDistance()
    {
        float maxArcLength = 0f;
        float maxDist = 0f;

        var canvas = MakeCanvas();
        var seg = new Segment(10f, 100f, 110f, 100f);  // length = 100
        canvas.DrawShapeThick(
            (arc, dist) => { maxArcLength = MathF.Max(maxArcLength, arc); maxDist = MathF.Max(maxDist, MathF.Abs(dist)); return 1; },
            10f, JoinStyle.Bevel, seg);

        Assert.IsTrue(maxArcLength > 0f && maxArcLength <= 100f,
            $"Arc length should be in (0, 100], got {maxArcLength}");
        Assert.IsTrue(maxDist > 0f && maxDist <= 5f,
            $"|perpDist| should be in (0, 5], got {maxDist}");
    }

    // ── join styles ──────────────────────────────────────────────────────────

    [TestMethod]
    public void BevelJoin_PaintsPixelsAroundCorner()
    {
        var canvas = MakeCanvas();
        var s1 = new Segment(100f, 150f, 100f, 100f);  // going up
        var s2 = new Segment(100f, 100f, 150f, 100f);  // going right
        canvas.DrawShapeThick(1, 8f, JoinStyle.Bevel, s1, s2);
        Assert.IsTrue(((ArrayImage)canvas.ImageAccessor).CountNonZero() > 0);
    }

    [TestMethod]
    public void MiterJoin_PaintsMorePixelsThanBevel_ForRightAngle()
    {
        var s1 = new Segment(100f, 150f, 100f, 100f);
        var s2 = new Segment(100f, 100f, 150f, 100f);

        var imgBevel = new ArrayImage(200, 200);
        new DrawI<int>(imgBevel).DrawShapeThick(1, 8f, JoinStyle.Bevel, s1, s2);

        var imgMiter = new ArrayImage(200, 200);
        new DrawI<int>(imgMiter).DrawShapeThick(1, 8f, JoinStyle.Miter, s1, s2);

        Assert.IsTrue(imgMiter.CountNonZero() >= imgBevel.CountNonZero(),
            "Miter join should produce at least as many pixels as bevel.");
    }

    [TestMethod]
    public void RoundJoin_PaintsPixelsAroundCorner()
    {
        var canvas = MakeCanvas();
        var s1 = new Segment(100f, 150f, 100f, 100f);
        var s2 = new Segment(100f, 100f, 150f, 100f);
        canvas.DrawShapeThick(1, 8f, JoinStyle.Round, s1, s2);
        Assert.IsTrue(((ArrayImage)canvas.ImageAccessor).CountNonZero() > 0);
    }

    [TestMethod]
    public void MiterLimit_ExceededForAcuteAngle_FallsBackToBevel()
    {
        // Very acute angle (nearly 180° turn back) → miter would be huge.
        var s1 = new Segment(50f, 100f, 100f, 100f);
        var s2 = new Segment(100f, 100f, 101f, 105f);  // sharp left turn

        var imgMiterSmallLimit = new ArrayImage(200, 200);
        new DrawI<int>(imgMiterSmallLimit)
            .DrawShapeThick(1, 8f, JoinStyle.Miter, 1f /*tight limit*/, s1, s2);

        var imgBevel = new ArrayImage(200, 200);
        new DrawI<int>(imgBevel).DrawShapeThick(1, 8f, JoinStyle.Bevel, s1, s2);

        // With a very tight miter limit the result should be the same as bevel.
        Assert.AreEqual(imgBevel.CountNonZero(), imgMiterSmallLimit.CountNonZero());
    }

    // ── IEnumerable overload ─────────────────────────────────────────────────

    [TestMethod]
    public void IEnumerableOverload_WorksIdenticallyToParamsOverload()
    {
        var seg = new Segment(10f, 50f, 190f, 50f);

        var img1 = new ArrayImage(200, 200);
        new DrawI<int>(img1).DrawShapeThick(1, 6f, JoinStyle.Bevel, seg);

        var img2 = new ArrayImage(200, 200);
        new DrawI<int>(img2).DrawShapeThick((_, _) => 1, 6f, JoinStyle.Bevel,
            (IEnumerable<IDrawable>)[seg]);

        Assert.AreEqual(img1.CountNonZero(), img2.CountNonZero());
    }
}
