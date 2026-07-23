using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using Utils.Drawing;
using Utils.Imaging;

namespace UtilsTest.Imaging;

/// <summary>
/// Tests for P2 audit findings #27 (null accessor), #28 (Blur overflow), #29 (HSV NaN).
/// </summary>
[TestClass]
public class P2FindingsTests
{
    private sealed class DummyAccessor<T> : IImageAccessor<T>
    {
        public int Width  { get; } = 10;
        public int Height { get; } = 10;
        public T this[int x, int y] { get => default!; set { } }
        public T this[Point p]      { get => default!; set { } }
    }

    // ── Finding #27: BaseDrawing validates null accessor ──────────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DrawI_NullAccessor_ThrowsArgumentNullException()
    {
        _ = new DrawI<ColorArgb32>(null!);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DrawF_NullAccessor_ThrowsArgumentNullException()
    {
        _ = new DrawF<ColorArgb32>(null!);
    }

    [TestMethod]
    public void DrawI_ValidAccessor_DoesNotThrow()
    {
        var accessor = new DummyAccessor<ColorArgb32>();
        var draw = new DrawI<ColorArgb32>(accessor);
        Assert.IsNotNull(draw.ImageAccessor);
    }

    // ── Finding #28: ConvolutionMatrixFactory.Blur validates size ─────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Blur_ZeroSize_ThrowsArgumentOutOfRangeException()
    {
        ConvolutionMatrixFactory.Blur(0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Blur_NegativeSize_ThrowsArgumentOutOfRangeException()
    {
        ConvolutionMatrixFactory.Blur(-1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Blur_SizeTooLarge_ThrowsArgumentOutOfRangeException()
    {
        ConvolutionMatrixFactory.Blur(1025); // > MaxKernelSize (1024)
    }

    [TestMethod]
    public void Blur_Size1_ReturnsSingleElementOne()
    {
        var k = ConvolutionMatrixFactory.Blur(1);
        Assert.AreEqual(1, k.GetLength(0));
        Assert.AreEqual(1, k.GetLength(1));
        Assert.AreEqual(1.0, k[0, 0], 1e-15);
    }

    [TestMethod]
    public void Blur_Size3_ReturnsnineEqualWeightsNormalized()
    {
        var k = ConvolutionMatrixFactory.Blur(3);
        Assert.AreEqual(3, k.GetLength(0));
        Assert.AreEqual(3, k.GetLength(1));
        double expected = 1.0 / 9.0;
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Assert.AreEqual(expected, k[x, y], 1e-15, $"k[{x},{y}]");
    }

    [TestMethod]
    public void Blur_MaxSize_DoesNotOverflow()
    {
        // size=1024 → 1024*1024 = 1,048,576 — must not overflow
        var k = ConvolutionMatrixFactory.Blur(1024);
        double expected = 1.0 / (1024.0 * 1024.0);
        Assert.AreEqual(expected, k[0, 0], 1e-20, "weight at max valid size");
    }

    // ── Finding #29: ColorAhsv rejects non-finite Hue ─────────────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void ColorAhsv_Constructor_NaNHue_ThrowsArgumentOutOfRangeException()
    {
        _ = new ColorAhsv(1.0, double.NaN, 0.5, 0.5);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void ColorAhsv_Constructor_InfinityHue_ThrowsArgumentOutOfRangeException()
    {
        _ = new ColorAhsv(1.0, double.PositiveInfinity, 0.5, 0.5);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void ColorAhsv_Constructor_NegativeInfinityHue_ThrowsArgumentOutOfRangeException()
    {
        _ = new ColorAhsv(1.0, double.NegativeInfinity, 0.5, 0.5);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void ColorAhsv_HueSetter_NaN_ThrowsArgumentOutOfRangeException()
    {
        var c = new ColorAhsv(1.0, 0.0, 0.5, 0.5);
        c.Hue = double.NaN;
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void ColorAhsv_HueSetter_Infinity_ThrowsArgumentOutOfRangeException()
    {
        var c = new ColorAhsv(1.0, 0.0, 0.5, 0.5);
        c.Hue = double.PositiveInfinity;
    }

    [TestMethod]
    public void ColorAhsv_Hue_NormalizesModulo360()
    {
        // Hue = 400 → 40 (400 % 360)
        var c = new ColorAhsv(1.0, 400.0, 0.5, 0.5);
        Assert.AreEqual(40.0, c.Hue, 1e-10, "400° normalises to 40°");
    }

    [TestMethod]
    public void ColorAhsv_Hue_NegativeNormalizes()
    {
        // Hue = -90 → 270 (MathEx.Mod convention)
        var c = new ColorAhsv(1.0, -90.0, 0.5, 0.5);
        Assert.IsTrue(c.Hue >= 0.0 && c.Hue < 360.0, $"Hue {c.Hue} must be in [0, 360)");
    }

    [TestMethod]
    public void ColorAhsv_ToArgbRoundTrip_StaysStable()
    {
        // An exact red: H=0, S=1, V=1 → R=1, G=0, B=0
        var hsv = new ColorAhsv(1.0, 0.0, 1.0, 1.0);
        ColorArgb rgb = hsv.ToArgbColor();
        Assert.AreEqual(1.0, rgb.Red,   1e-9, "red");
        Assert.AreEqual(0.0, rgb.Green, 1e-9, "green");
        Assert.AreEqual(0.0, rgb.Blue,  1e-9, "blue");
    }

    // ── Finding #22: packed value is canonical ARGB on little-endian ──────────

    [TestMethod]
    public void ColorArgb32_PackedValue_MatchesCanonicalArgbOnLittleEndian()
    {
        // Canonical ARGB: 0xAARRGGBB
        // (Alpha=0xAA, Red=0xBB, Green=0xCC, Blue=0xDD)
        var c = new ColorArgb32(0xAA, 0xBB, 0xCC, 0xDD);
        Assert.AreEqual(0xAA, c.Alpha, "alpha");
        Assert.AreEqual(0xBB, c.Red,   "red");
        Assert.AreEqual(0xCC, c.Green, "green");
        Assert.AreEqual(0xDD, c.Blue,  "blue");

        // On little-endian the Value uint equals (A<<24)|(R<<16)|(G<<8)|B
        if (System.BitConverter.IsLittleEndian)
        {
            uint expected = (0xAAu << 24) | (0xBBu << 16) | (0xCCu << 8) | 0xDDu;
            Assert.AreEqual(expected, c.Value, "packed value on little-endian");
        }
    }

    [TestMethod]
    public void ColorArgb64_PackedValue_MatchesCanonicalArgbOnLittleEndian()
    {
        // (Alpha=0xAAAA, Red=0xBBBB, Green=0xCCCC, Blue=0xDDDD)
        var c = new ColorArgb64(0xAAAA, 0xBBBB, 0xCCCC, 0xDDDD);
        Assert.AreEqual(0xAAAA, c.Alpha, "alpha");
        Assert.AreEqual(0xBBBB, c.Red,   "red");
        Assert.AreEqual(0xCCCC, c.Green, "green");
        Assert.AreEqual(0xDDDD, c.Blue,  "blue");

        // On little-endian: Value = (A<<48)|(R<<32)|(G<<16)|B
        if (System.BitConverter.IsLittleEndian)
        {
            ulong expected = ((ulong)0xAAAA << 48) | ((ulong)0xBBBB << 32) | ((ulong)0xCCCC << 16) | 0xDDDD;
            Assert.AreEqual(expected, c.Value, "packed value on little-endian");
        }
    }

    [TestMethod]
    public void ColorArgb32_RoundTripThroughValue_IsConsistent()
    {
        var original = new ColorArgb32(0xFF, 0x12, 0x34, 0x56);
        var roundTrip = new ColorArgb32(original.Value);
        Assert.AreEqual(original.Alpha, roundTrip.Alpha);
        Assert.AreEqual(original.Red,   roundTrip.Red);
        Assert.AreEqual(original.Green, roundTrip.Green);
        Assert.AreEqual(original.Blue,  roundTrip.Blue);
    }

    [TestMethod]
    public void ColorArgb64_RoundTripThroughValue_IsConsistent()
    {
        var original = new ColorArgb64(0xFFFF, 0x1234, 0x5678, 0x9ABC);
        var roundTrip = new ColorArgb64(original.Value);
        Assert.AreEqual(original.Alpha, roundTrip.Alpha);
        Assert.AreEqual(original.Red,   roundTrip.Red);
        Assert.AreEqual(original.Green, roundTrip.Green);
        Assert.AreEqual(original.Blue,  roundTrip.Blue);
    }
}
