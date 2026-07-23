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

    // ── IntersectionMergeThreshold rejects NaN and Infinity ──────────────────

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void DrawI_IntersectionMergeThreshold_NaN_ThrowsArgumentOutOfRangeException()
    {
        var draw = new DrawI<ColorArgb32>(new DummyAccessor<ColorArgb32>());
        draw.IntersectionMergeThreshold = float.NaN;
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void DrawI_IntersectionMergeThreshold_PositiveInfinity_ThrowsArgumentOutOfRangeException()
    {
        var draw = new DrawI<ColorArgb32>(new DummyAccessor<ColorArgb32>());
        draw.IntersectionMergeThreshold = float.PositiveInfinity;
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void DrawI_IntersectionMergeThreshold_NegativeInfinity_ThrowsArgumentOutOfRangeException()
    {
        var draw = new DrawI<ColorArgb32>(new DummyAccessor<ColorArgb32>());
        draw.IntersectionMergeThreshold = float.NegativeInfinity;
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void DrawI_IntersectionMergeThreshold_Negative_ThrowsArgumentOutOfRangeException()
    {
        var draw = new DrawI<ColorArgb32>(new DummyAccessor<ColorArgb32>());
        draw.IntersectionMergeThreshold = -1f;
    }

    [TestMethod]
    public void DrawI_IntersectionMergeThreshold_Zero_IsAccepted()
    {
        var draw = new DrawI<ColorArgb32>(new DummyAccessor<ColorArgb32>());
        draw.IntersectionMergeThreshold = 0f;
        Assert.AreEqual(0f, draw.IntersectionMergeThreshold);
    }

    [TestMethod]
    public void DrawI_IntersectionMergeThreshold_PositiveFinite_IsAccepted()
    {
        var draw = new DrawI<ColorArgb32>(new DummyAccessor<ColorArgb32>());
        draw.IntersectionMergeThreshold = 1.5f;
        Assert.AreEqual(1.5f, draw.IntersectionMergeThreshold);
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

    // ── Float-to-int conversion rounding (PR #501 review) ────────────────────

    [TestMethod]
    public void ColorArgb32_FromFloat_HalfValue_Rounds_To_128()
    {
        var c = new ColorArgb32(new ColorArgb(0.5, 0.5, 0.5, 0.5));
        Assert.AreEqual((byte)128, c.Alpha, "alpha");
        Assert.AreEqual((byte)128, c.Red,   "red");
        Assert.AreEqual((byte)128, c.Green, "green");
        Assert.AreEqual((byte)128, c.Blue,  "blue");
    }

    [TestMethod]
    public void ColorArgb64_FromFloat_HalfValue_Rounds_To_32768()
    {
        var c = new ColorArgb64(new ColorArgb(0.5, 0.5, 0.5, 0.5));
        Assert.AreEqual((ushort)32768, c.Alpha, "alpha");
        Assert.AreEqual((ushort)32768, c.Red,   "red");
        Assert.AreEqual((ushort)32768, c.Green, "green");
        Assert.AreEqual((ushort)32768, c.Blue,  "blue");
    }

    [TestMethod]
    public void ColorArgb32_FromFloat_Zero_Produces_Zero()
    {
        var c = new ColorArgb32(new ColorArgb(0.0, 0.0, 0.0, 0.0));
        Assert.AreEqual((byte)0, c.Alpha, "alpha");
        Assert.AreEqual((byte)0, c.Red,   "red");
        Assert.AreEqual((byte)0, c.Green, "green");
        Assert.AreEqual((byte)0, c.Blue,  "blue");
    }

    [TestMethod]
    public void ColorArgb32_FromFloat_One_Produces_255()
    {
        var c = new ColorArgb32(new ColorArgb(1.0, 1.0, 1.0, 1.0));
        Assert.AreEqual(byte.MaxValue, c.Alpha, "alpha");
        Assert.AreEqual(byte.MaxValue, c.Red,   "red");
        Assert.AreEqual(byte.MaxValue, c.Green, "green");
        Assert.AreEqual(byte.MaxValue, c.Blue,  "blue");
    }

    [TestMethod]
    public void ColorArgb64_FromFloat_Zero_Produces_Zero()
    {
        var c = new ColorArgb64(new ColorArgb(0.0, 0.0, 0.0, 0.0));
        Assert.AreEqual((ushort)0, c.Alpha, "alpha");
        Assert.AreEqual((ushort)0, c.Red,   "red");
        Assert.AreEqual((ushort)0, c.Green, "green");
        Assert.AreEqual((ushort)0, c.Blue,  "blue");
    }

    [TestMethod]
    public void ColorArgb64_FromFloat_One_Produces_65535()
    {
        var c = new ColorArgb64(new ColorArgb(1.0, 1.0, 1.0, 1.0));
        Assert.AreEqual(ushort.MaxValue, c.Alpha, "alpha");
        Assert.AreEqual(ushort.MaxValue, c.Red,   "red");
        Assert.AreEqual(ushort.MaxValue, c.Green, "green");
        Assert.AreEqual(ushort.MaxValue, c.Blue,  "blue");
    }

    [TestMethod]
    public void ColorArgb32_FromFloat_JustBelowMidpoint_RoundsDown()
    {
        // The midpoint between 127 and 128 is 127.5/255. Just below → rounds to 127.
        double justBelow = 127.5 / 255.0 - 1e-10;
        var c = new ColorArgb32(new ColorArgb(justBelow, justBelow, justBelow, justBelow));
        Assert.AreEqual((byte)127, c.Red, "just below 127.5/255 should round down to 127");
    }

    [TestMethod]
    public void ColorArgb32_FromFloat_AtMidpoint_RoundsUp()
    {
        // Exact midpoint 127.5/255 → rounds away from zero → 128.
        double midpoint = 127.5 / 255.0;
        var c = new ColorArgb32(new ColorArgb(midpoint, midpoint, midpoint, midpoint));
        Assert.AreEqual((byte)128, c.Red, "exact midpoint 127.5/255 should round to 128");
    }

    [TestMethod]
    public void ColorArgb32_FromFloat_JustAboveMidpoint_RoundsUp()
    {
        // Just above 127.5/255 → still rounds to 128.
        double justAbove = 127.5 / 255.0 + 1e-10;
        var c = new ColorArgb32(new ColorArgb(justAbove, justAbove, justAbove, justAbove));
        Assert.AreEqual((byte)128, c.Red, "just above 127.5/255 should round up to 128");
    }

    [TestMethod]
    public void ColorArgb64_FromFloat_JustBelowMidpoint_RoundsDown()
    {
        // The midpoint between 32767 and 32768 is 32767.5/65535. Just below → rounds to 32767.
        double justBelow = 32767.5 / 65535.0 - 1e-10;
        var c = new ColorArgb64(new ColorArgb(justBelow, justBelow, justBelow, justBelow));
        Assert.AreEqual((ushort)32767, c.Red, "just below 32767.5/65535 should round down to 32767");
    }

    [TestMethod]
    public void ColorArgb64_FromFloat_AtMidpoint_RoundsUp()
    {
        // Exact midpoint 32767.5/65535 → rounds away from zero → 32768.
        double midpoint = 32767.5 / 65535.0;
        var c = new ColorArgb64(new ColorArgb(midpoint, midpoint, midpoint, midpoint));
        Assert.AreEqual((ushort)32768, c.Red, "exact midpoint 32767.5/65535 should round to 32768");
    }

    [TestMethod]
    public void ColorArgb64_FromFloat_JustAboveMidpoint_RoundsUp()
    {
        // Just above 32767.5/65535 → still rounds to 32768.
        double justAbove = 32767.5 / 65535.0 + 1e-10;
        var c = new ColorArgb64(new ColorArgb(justAbove, justAbove, justAbove, justAbove));
        Assert.AreEqual((ushort)32768, c.Red, "just above 32767.5/65535 should round up to 32768");
    }

    [TestMethod]
    public void ColorArgb32_RoundTripFloatToByteToFloat_IsConsistent()
    {
        // Representative values that survive a round-trip with at most ±1 LSB error.
        byte[] samples = { 0, 1, 64, 127, 128, 191, 254, 255 };
        foreach (byte b in samples)
        {
            double f = b / 255.0;
            var c = new ColorArgb32(new ColorArgb(f, f, f, f));
            Assert.AreEqual(b, c.Red, $"round-trip for byte {b}");
        }
    }

    [TestMethod]
    public void ColorArgb64_RoundTripFloatToUShortToFloat_IsConsistent()
    {
        ushort[] samples = { 0, 1, 256, 32767, 32768, 65534, 65535 };
        foreach (ushort u in samples)
        {
            double f = u / 65535.0;
            var c = new ColorArgb64(new ColorArgb(f, f, f, f));
            Assert.AreEqual(u, c.Red, $"round-trip for ushort {u}");
        }
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

    // ── Finding #24/#25: gradient validation and rounding ─────────────────────

    [TestMethod]
    public void ColorArgb32_LinearGradient_ClampsBelowZero()
    {
        ColorArgb32 white = new(255, 255, 255, 255);
        ColorArgb32 black = new(255, 0, 0, 0);
        var result = ColorArgb32.LinearGradient(white, black, -1f);
        Assert.AreEqual(white, result, "position < 0 should clamp to color1");
    }

    [TestMethod]
    public void ColorArgb32_LinearGradient_ClampsAboveOne()
    {
        ColorArgb32 white = new(255, 255, 255, 255);
        ColorArgb32 black = new(255, 0, 0, 0);
        var result = ColorArgb32.LinearGradient(white, black, 2f);
        Assert.AreEqual(black, result, "position > 1 should clamp to color2");
    }

    [TestMethod]
    public void ColorArgb32_LinearGradient_MidpointRoundsCorrectly()
    {
        ColorArgb32 a = new(255, 100, 200, 50);
        ColorArgb32 b = new(255, 101, 201, 51);
        var mid = ColorArgb32.LinearGradient(a, b, 0.5f);
        // (100+101)/2 = 100.5 → rounds to 101 (half-to-even or banker's rounding)
        // Math.Round uses MidpointRounding.ToEven by default
        Assert.AreEqual((byte)Math.Round(100.5), mid.Red,   "midpoint red");
        Assert.AreEqual((byte)Math.Round(200.5), mid.Green, "midpoint green");
        Assert.AreEqual((byte)Math.Round(50.5f), mid.Blue,  "midpoint blue");
    }

    [TestMethod]
    public void ColorArgb64_LinearGradient_ClampsBelowZero()
    {
        ColorArgb64 white = new(65535, 65535, 65535, 65535);
        ColorArgb64 black = new(65535, 0, 0, 0);
        var result = ColorArgb64.LinearGradient(white, black, -1f);
        Assert.AreEqual(white, result, "position < 0 should clamp to color1");
    }

    [TestMethod]
    public void ColorArgb64_LinearGradient_ClampsAboveOne()
    {
        ColorArgb64 white = new(65535, 65535, 65535, 65535);
        ColorArgb64 black = new(65535, 0, 0, 0);
        var result = ColorArgb64.LinearGradient(white, black, 2f);
        Assert.AreEqual(black, result, "position > 1 should clamp to color2");
    }

    [TestMethod]
    public void ColorArgb_LinearGradient_ClampsBelowZero()
    {
        ColorArgb white = new(1.0, 1.0, 1.0, 1.0);
        ColorArgb black = new(1.0, 0.0, 0.0, 0.0);
        var result = ColorArgb.LinearGradient(white, black, -1.0);
        Assert.AreEqual(1.0, result.Red, 1e-9, "position < 0 should clamp to color1");
    }

    [TestMethod]
    public void ColorArgb_LinearGradient_ClampsAboveOne()
    {
        ColorArgb white = new(1.0, 1.0, 1.0, 1.0);
        ColorArgb black = new(1.0, 0.0, 0.0, 0.0);
        var result = ColorArgb.LinearGradient(white, black, 2.0);
        Assert.AreEqual(0.0, result.Red, 1e-9, "position > 1 should clamp to color2");
    }

    [TestMethod]
    public void ColorArgb_PerceptualGradient_DiffersFromLinearForColorful()
    {
        // For equal mix of red and black, sqrt gives a different result than linear.
        ColorArgb red   = new(1.0, 1.0, 0.0, 0.0);
        ColorArgb black = new(1.0, 0.0, 0.0, 0.0);
        var perceptual = ColorArgb.Gradient(red, black, 0.5);
        var linear     = ColorArgb.LinearGradient(red, black, 0.5);
        // sqrt((1²×0.5 + 0²×0.5)) = sqrt(0.5) ≈ 0.707, linear = 0.5
        Assert.AreNotEqual(linear.Red, perceptual.Red, 1e-6, "perceptual and linear should differ");
        Assert.AreEqual(Math.Sqrt(0.5), perceptual.Red, 1e-9, "perceptual red should use sqrt formula");
    }
}
