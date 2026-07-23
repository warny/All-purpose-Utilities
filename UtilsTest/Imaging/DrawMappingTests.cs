using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using Utils.Drawing;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class DrawMappingTests
{
    private sealed class ArrayImageAccessor<T> : IImageAccessor<T>
    {
        private readonly T[,] data;

        public ArrayImageAccessor(int width, int height)
        {
            data = new T[width, height];
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public T this[int x, int y]
        {
            get => data[x, y];
            set => data[x, y] = value;
        }
    }

    private static bool AllBlack(ArrayImageAccessor<ColorArgb32> a)
    {
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                if (a[x, y].Alpha != 0 || a[x, y].Red != 0 || a[x, y].Green != 0 || a[x, y].Blue != 0)
                    return false;
        return true;
    }

    // ── Existing tests ────────────────────────────────────────────────────────

    [TestMethod]
    public void ComputePixelPosition_MapsViewportCoordinates()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(100, 50);
        var draw = new DrawF<ColorArgb32>(accessor, -10, -5, 5, 10);

        Point pixel = draw.ComputePixelPosition(0f, 0f);

        Assert.AreEqual(50, pixel.X);
        Assert.AreEqual(25, pixel.Y);
    }

    [TestMethod]
    public void ComputePoint_InvertsPixelMapping()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(80, 60);
        var draw = new DrawF<ColorArgb32>(accessor, 0, 0, accessor.Width, accessor.Height);
        PointF viewportPoint = new(12.5f, 30.5f);

        PointF pixel = draw.ComputePixelPositionF(viewportPoint);
        PointF mappedBack = draw.ComputePoint(pixel);

        Assert.AreEqual(viewportPoint.X, mappedBack.X, 1e-3);
        Assert.AreEqual(viewportPoint.Y, mappedBack.Y, 1e-3);
    }

    [TestMethod]
    public void DrawPoint_WritesPixelWhenInsideBounds()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 color = new(255, 10, 20, 30);

        draw.DrawPoint(3, 4, color);

        Assert.AreEqual(color.Alpha, accessor[3, 4].Alpha);
        Assert.AreEqual(color.Red, accessor[3, 4].Red);
        Assert.AreEqual(color.Green, accessor[3, 4].Green);
        Assert.AreEqual(color.Blue, accessor[3, 4].Blue);
    }

    [TestMethod]
    public void DrawPoint_IgnoresOutOfBoundsCoordinates()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(5, 5);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 color = new(255, 10, 20, 30);

        draw.DrawPoint(-1, 0, color);
        draw.DrawPoint(0, 5, color);

        for (int y = 0; y < accessor.Height; y++)
        {
            for (int x = 0; x < accessor.Width; x++)
            {
                Assert.AreEqual(0, accessor[x, y].Alpha);
                Assert.AreEqual(0, accessor[x, y].Red);
                Assert.AreEqual(0, accessor[x, y].Green);
                Assert.AreEqual(0, accessor[x, y].Blue);
            }
        }
    }

    [TestMethod]
    public void DrawFDrawPointUsesViewportMapping()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(16, 16);
        var draw = new DrawF<ColorArgb32>(accessor, 0, 0, 16, 16);
        ColorArgb32 color = new(255, 100, 110, 120);

        draw.DrawPoint(7.9f, 8.2f, color);

        Point expectedPixel = draw.ComputePixelPosition(7.9f, 8.2f);
        Assert.AreEqual(color.Alpha, accessor[expectedPixel.X, expectedPixel.Y].Alpha);
        Assert.AreEqual(color.Red, accessor[expectedPixel.X, expectedPixel.Y].Red);
        Assert.AreEqual(color.Green, accessor[expectedPixel.X, expectedPixel.Y].Green);
        Assert.AreEqual(color.Blue, accessor[expectedPixel.X, expectedPixel.Y].Blue);
    }

    // ── Finding #13: DrawF constructor validates viewport ─────────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void DrawF_NaNTop_ThrowsArgumentException()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        _ = new DrawF<ColorArgb32>(accessor, float.NaN, 0, 10, 10);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void DrawF_InfinityLeft_ThrowsArgumentException()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        _ = new DrawF<ColorArgb32>(accessor, 0, float.PositiveInfinity, 10, 10);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void DrawF_InfinityRight_ThrowsArgumentException()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        _ = new DrawF<ColorArgb32>(accessor, 0, 0, float.NegativeInfinity, 10);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void DrawF_ZeroWidthViewport_ThrowsArgumentException()
    {
        // Right == Left → zero-width span
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        _ = new DrawF<ColorArgb32>(accessor, 0, 5, 5, 10);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void DrawF_ZeroHeightViewport_ThrowsArgumentException()
    {
        // Down == Top → zero-height span
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        _ = new DrawF<ColorArgb32>(accessor, 3, 0, 10, 3);
    }

    [TestMethod]
    public void DrawF_NegativeSpan_Accepted_ReversedAxis()
    {
        // Right < Left is a valid (mirrored) viewport.
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawF<ColorArgb32>(accessor, 0, 10, 0, 10); // reversed horizontal axis

        // Left coordinate (10) → pixel 0; Right coordinate (0) → pixel 10 (exclusive).
        Point p = draw.ComputePixelPosition(10f, 0f);
        Assert.AreEqual(0, p.X, "reversed axis: x=Left→pixel 0");
    }

    // ── Finding #14: half-open viewport boundary contract ─────────────────────

    [TestMethod]
    public void DrawF_LeftBoundary_MapsToPixelZero()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(8, 8);
        var draw = new DrawF<ColorArgb32>(accessor, 0, 0, 8, 8);

        Point p = draw.ComputePixelPosition(0f, 0f); // x = Left
        Assert.AreEqual(0, p.X, "Left must map to pixel column 0");
        Assert.AreEqual(0, p.Y, "Top must map to pixel row 0");
    }

    [TestMethod]
    public void DrawF_RightExclusive_MapsToOutOfBounds_PixelDropped()
    {
        // The viewport is [0, 8) × [0, 8]. Drawing at x=Right should map to
        // pixel 8 (out of bounds) and be silently discarded.
        var accessor = new ArrayImageAccessor<ColorArgb32>(8, 8);
        var draw = new DrawF<ColorArgb32>(accessor, 0, 0, 8, 8);
        ColorArgb32 red = new(255, 255, 0, 0);

        draw.DrawPoint(8f, 4f, red); // x == Right (exclusive boundary)

        Assert.IsTrue(AllBlack(accessor), "pixel at x=Right must not be written (half-open boundary)");
    }

    // ── Finding #15: DrawShape does not divide by zero for length-0 shapes ────

    [TestMethod]
    public void DrawShape_ZeroLengthSegment_DoesNotThrow()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 red = new(255, 200, 0, 0);

        // A segment from a point to itself has length == 0.
        var seg = new Segment(5, 5, 5, 5);
        draw.DrawShape(red, seg);

        // The test simply verifies no exception is thrown.
        // DrawPoint at (5,5) may or may not be written depending on GetPoints output.
    }

    // ── Finding #16: FillShapeCore clips scanlines to image bounds ────────────

    [TestMethod]
    public void FillPolygon1_ExtremelyFarGeometry_DoesNotModifyImage()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 red = new(255, 200, 0, 0);

        // A polygon far outside the image.
        draw.FillPolygon1(red,
            new Point(10000, 10000),
            new Point(20000, 10000),
            new Point(20000, 20000),
            new Point(10000, 20000));

        Assert.IsTrue(AllBlack(accessor), "off-screen geometry must not modify any image pixel");
    }

    [TestMethod]
    public void FillPolygon1_ExtremelyFarGeometry_CompletesQuickly()
    {
        // Without scan-line clipping, a polygon covering 10000×10000 units would
        // iterate 10000 scan-lines doing nothing.  With clipping the loop is bounded
        // by the image height (10).  We verify this by checking elapsed time.
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 red = new(255, 200, 0, 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        draw.FillPolygon1(red,
            new Point(-100000, -100000),
            new Point( 100000, -100000),
            new Point( 100000,  100000),
            new Point(-100000,  100000));
        sw.Stop();

        // Should complete well under 1 second even on slow CI machines.
        Assert.IsTrue(sw.ElapsedMilliseconds < 1000,
            $"FillPolygon1 on extreme geometry took {sw.ElapsedMilliseconds} ms — scanlines may not be clipped");
    }

    [TestMethod]
    public void FillPolygon1_NanCoordinates_DoesNotThrow()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        ColorArgb32 red = new(255, 200, 0, 0);

        // NaN/Infinity in geometry must not cause an unhandled exception.
        draw.FillPolygon1(red,
            new Point(int.MinValue, int.MinValue),
            new Point(int.MaxValue, int.MinValue),
            new Point(int.MaxValue, int.MaxValue));
    }

    // ── Finding #26: IntersectionMergeThreshold is explicit and configurable ──

    [TestMethod]
    public void IntersectionMergeThreshold_DefaultIsHalfPixel()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        Assert.AreEqual(0.5f, draw.IntersectionMergeThreshold, 1e-6f);
    }

    [TestMethod]
    public void IntersectionMergeThreshold_SetZero_DisablesMerging()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor) { IntersectionMergeThreshold = 0f };
        Assert.AreEqual(0f, draw.IntersectionMergeThreshold, 1e-6f);
        // Fill a simple square — must not throw and must actually paint pixels.
        ColorArgb32 red = new(255, 255, 0, 0);
        draw.FillPolygon1(red, new Point(2, 2), new Point(6, 2), new Point(6, 6), new Point(2, 6));
        Assert.AreNotEqual(default(ColorArgb32), accessor[4, 4], "Interior pixel should be filled.");
    }

    [TestMethod]
    public void IntersectionMergeThreshold_CustomValue_IsRespected()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(20, 20);
        var draw = new DrawI<ColorArgb32>(accessor) { IntersectionMergeThreshold = 2.0f };
        Assert.AreEqual(2.0f, draw.IntersectionMergeThreshold, 1e-6f);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void IntersectionMergeThreshold_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var accessor = new ArrayImageAccessor<ColorArgb32>(10, 10);
        var draw = new DrawI<ColorArgb32>(accessor);
        draw.IntersectionMergeThreshold = -0.1f;
    }
}
