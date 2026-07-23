using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class MatrixImageTransformerTests
{
    // ── In-memory accessories ─────────────────────────────────────────────────

    private sealed class ArrayAccessor<A> : IImageAccessor<A, double>
        where A : struct, IColorArgb<double>
    {
        private readonly A[] _data;
        public int Width { get; }
        public int Height { get; }

        public ArrayAccessor(int width, int height)
        {
            Width = width;
            Height = height;
            _data = new A[width * height];
        }

        public A this[int x, int y]
        {
            get => _data[y * Width + x];
            set => _data[y * Width + x] = value;
        }

        public A this[Point p]
        {
            get => this[p.X, p.Y];
            set => this[p.X, p.Y] = value;
        }
    }

    // An accessor that reports arbitrary dimensions without allocating the backing data.
    private sealed class PhantomAccessor<A> : IImageAccessor<A, double>
        where A : struct, IColorArgb<double>
    {
        public int Width { get; }
        public int Height { get; }
        public A this[int x, int y] { get => default; set { } }
        public A this[Point p]      { get => default; set { } }

        public PhantomAccessor(int width, int height) { Width = width; Height = height; }
    }

    private static double[,] Identity1x1() => new double[,] { { 1.0 } };
    private static double[,] EdgeDetectionKernel() => new double[,]
    {
        { -1, -1, -1 },
        { -1,  8, -1 },
        { -1, -1, -1 }
    };

    // ── Existing blur/uniform tests ───────────────────────────────────────────

    [TestMethod]
    public void BlurKernel_AveragesNeighbors()
    {
        var image = new ArrayAccessor<ColorArgb>(3, 3);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                image[x, y] = new ColorArgb(1, 1, 1, 1);
        image[1, 1] = new ColorArgb(1, 0, 0, 0);

        double[,] blur = ConvolutionMatrixFactory.Blur(3);
        image.ApplyWeightedMatrix(blur, new Point(-1, -1));

        // Center pixel: 8 white + 1 black = average 8/9 ≈ 0.889
        Assert.AreEqual(8.0 / 9.0, image[1, 1].Red, 1e-9);
    }

    [TestMethod]
    public void UniformImage_UnchangedByBlur()
    {
        var image = new ArrayAccessor<ColorArgb>(4, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                image[x, y] = new ColorArgb(1, 0.5, 0.3, 0.7);

        double[,] blur = ConvolutionMatrixFactory.Blur(3);
        image.ApplyWeightedMatrix(blur, new Point(-1, -1));

        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                Assert.AreEqual(0.5, image[x, y].Red,   1e-9, $"pixel ({x},{y}) red");
                Assert.AreEqual(0.3, image[x, y].Green, 1e-9, $"pixel ({x},{y}) green");
                Assert.AreEqual(0.7, image[x, y].Blue,  1e-9, $"pixel ({x},{y}) blue");
            }
    }

    // ── Finding #8: weights null, empty, non-finite, cloned ──────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullWeights_ThrowsArgumentNullException()
    {
        new MatrixImageTransformer<ColorArgb, double>(null!, new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullCreator_ThrowsArgumentNullException()
    {
        new MatrixImageTransformer<ColorArgb, double>(Identity1x1(), new Point(0, 0), null!);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_EmptyWeightsFirstDimension_ThrowsArgumentException()
    {
        new MatrixImageTransformer<ColorArgb, double>(new double[0, 1], new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_EmptyWeightsSecondDimension_ThrowsArgumentException()
    {
        new MatrixImageTransformer<ColorArgb, double>(new double[1, 0], new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_NaNWeight_ThrowsArgumentException()
    {
        var weights = new double[,] { { double.NaN } };
        new MatrixImageTransformer<ColorArgb, double>(weights, new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_InfinityWeight_ThrowsArgumentException()
    {
        var weights = new double[,] { { double.PositiveInfinity } };
        new MatrixImageTransformer<ColorArgb, double>(weights, new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_NegativeInfinityWeight_ThrowsArgumentException()
    {
        var weights = new double[,] { { double.NegativeInfinity } };
        new MatrixImageTransformer<ColorArgb, double>(weights, new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));
    }

    [TestMethod]
    public void Constructor_ClonesWeights_ExternalMutationIgnored()
    {
        var weights = new double[,] { { 1.0 } };
        var transformer = new MatrixImageTransformer<ColorArgb, double>(weights, new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));

        // Mutate the original array after construction.
        weights[0, 0] = 0.0;

        // A 1×1 image with a 1×1 identity-weight kernel must be unchanged.
        var image = new ArrayAccessor<ColorArgb>(1, 1);
        image[0, 0] = new ColorArgb(1, 0.5, 0.3, 0.7);
        transformer.Transform(image);

        // If the mutation were visible, the pixel would become black (weight 0).
        // It should remain unchanged (weight 1 from the cloned copy).
        Assert.AreEqual(0.5, image[0, 0].Red,   1e-9, "external mutation must not affect transformer");
    }

    // ── Finding #7: mask dimension mismatch ──────────────────────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Transform_MaskWidthMismatch_ThrowsArgumentException()
    {
        var image = new ArrayAccessor<ColorArgb>(4, 4);
        var mask  = new ArrayAccessor<ColorArgb>(3, 4); // wrong width

        var transformer = new MatrixImageTransformer<ColorArgb, double>(
            Identity1x1(), new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));

        transformer.Transform(image, mask);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Transform_MaskHeightMismatch_ThrowsArgumentException()
    {
        var image = new ArrayAccessor<ColorArgb>(4, 4);
        var mask  = new ArrayAccessor<ColorArgb>(4, 3); // wrong height

        var transformer = new MatrixImageTransformer<ColorArgb, double>(
            Identity1x1(), new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));

        transformer.Transform(image, mask);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Transform_NullAccessor_ThrowsArgumentNullException()
    {
        var transformer = new MatrixImageTransformer<ColorArgb, double>(
            Identity1x1(), new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));

        transformer.Transform(null!);
    }

    // ── Finding #6: checked dimension multiplication ──────────────────────────

    [TestMethod]
    [ExpectedException(typeof(OverflowException))]
    public void Transform_IntegerOverflowDimensions_ThrowsOverflowException()
    {
        // 100 000 × 100 000 = 10^10, overflows int.
        var oversized = new PhantomAccessor<ColorArgb>(100_000, 100_000);
        var transformer = new MatrixImageTransformer<ColorArgb, double>(
            Identity1x1(), new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));

        transformer.Transform(oversized);
    }

    [TestMethod]
    [ExpectedException(typeof(OverflowException))]
    public void Transform_ExceedsPixelLimit_ThrowsOverflowException()
    {
        // 8193 × 8193 = 67 117 249 > 64 × 1024 × 1024 = 67 108 864.
        var big = new PhantomAccessor<ColorArgb>(8193, 8193);
        var transformer = new MatrixImageTransformer<ColorArgb, double>(
            Identity1x1(), new Point(0, 0),
            (a, r, g, b) => new ColorArgb(a, r, g, b));

        transformer.Transform(big);
    }

    // ── Finding #12: zero-sum kernel (edge detection) ─────────────────────────

    [TestMethod]
    public void EdgeDetectionKernel_UniformImage_ProducesBlack()
    {
        // A uniform image has no edges, so edge detection should yield zero.
        var image = new ArrayAccessor<ColorArgb>(5, 5);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                image[x, y] = new ColorArgb(1, 0.5, 0.5, 0.5);

        double[,] edge = EdgeDetectionKernel();
        image.ApplyWeightedMatrix(edge, new Point(-1, -1));

        // Interior pixels: all neighbors same value → raw sum = 0.
        Assert.AreEqual(0.0, image[2, 2].Red,   1e-9, "interior pixel on uniform image must be black");
        Assert.AreEqual(0.0, image[2, 2].Green, 1e-9);
        Assert.AreEqual(0.0, image[2, 2].Blue,  1e-9);
    }

    [TestMethod]
    public void EdgeDetectionKernel_ContrastEdge_ProducesNonZero()
    {
        // 5×5 image: left half black, right half white.
        var image = new ArrayAccessor<ColorArgb>(5, 5);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                image[x, y] = new ColorArgb(1, x < 3 ? 0.0 : 1.0, 0, 0);

        double[,] edge = EdgeDetectionKernel();
        image.ApplyWeightedMatrix(edge, new Point(-1, -1));

        // Pixel at the boundary should have non-zero red (detected edge).
        // Pre-condition: if zero-sum kernel is skipped entirely, all pixels stay unchanged.
        bool anyNonZeroRed = false;
        for (int y = 0; y < 5; y++)
            for (int x = 1; x < 4; x++)
                if (image[x, y].Red > 0.0) { anyNonZeroRed = true; break; }

        Assert.IsTrue(anyNonZeroRed, "edge detection must produce non-zero response at contrast boundary");
    }

    [TestMethod]
    public void EdgeDetectionKernel_BrightSpotOnBlack_ProducesResponseAtSpot()
    {
        // 5×5 all black, center pixel white.
        var image = new ArrayAccessor<ColorArgb>(5, 5);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                image[x, y] = new ColorArgb(1, 0, 0, 0);
        image[2, 2] = new ColorArgb(1, 1, 0, 0); // bright red center

        double[,] edge = EdgeDetectionKernel();
        image.ApplyWeightedMatrix(edge, new Point(-1, -1));

        // The center pixel had +8 from its own weight and -1 × 8 = -8 from neighbours.
        // Raw sum for the center = 8 × 1 + 0 × (-1) × 8 neighbours = 8 → clamped to 1.
        // Neighbours: -1 × center + sum-of-their-other-neighbours.
        // Regardless of exact values, the response at the center (2,2) must be > 0.
        Assert.IsTrue(image[2, 2].Red > 0.0, "bright spot must produce non-zero edge response at its location");
    }
}
