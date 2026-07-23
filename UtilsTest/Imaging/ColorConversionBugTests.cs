using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

/// <summary>
/// Regression tests for the color conversion bugs fixed in the 2026-07 audit.
/// Covers findings #9 (wrong divisor in ColorArgb64→ColorArgb), #10 (wrong 8→16 expansion)
/// and #23 (wrong IColorArgb&lt;byte&gt; constructor).
/// </summary>
[TestClass]
public class ColorConversionBugTests
{
    // ── Finding #10: 8-bit → 16-bit expansion ────────────────────────────────

    [TestMethod]
    public void ExpandByte_Zero_MapsToZero()
    {
        ColorArgb32 src = new(0, 0, 0, 0);
        ColorArgb64 dst = src;
        Assert.AreEqual((ushort)0, dst.Alpha);
        Assert.AreEqual((ushort)0, dst.Red);
        Assert.AreEqual((ushort)0, dst.Green);
        Assert.AreEqual((ushort)0, dst.Blue);
    }

    [TestMethod]
    public void ExpandByte_MaxValue_MapsToUshortMaxValue()
    {
        ColorArgb32 src = new(255, 255, 255, 255);
        ColorArgb64 dst = src;
        Assert.AreEqual(ushort.MaxValue, dst.Alpha, "alpha must map 255→65535");
        Assert.AreEqual(ushort.MaxValue, dst.Red,   "red must map 255→65535");
        Assert.AreEqual(ushort.MaxValue, dst.Green, "green must map 255→65535");
        Assert.AreEqual(ushort.MaxValue, dst.Blue,  "blue must map 255→65535");
    }

    [TestMethod]
    public void ExpandByte_AllValues_Exhaustive()
    {
        // For every byte b, ExpandByte(b) must equal (b << 8) | b.
        for (int b = 0; b <= 255; b++)
        {
            byte value = (byte)b;
            ColorArgb32 src = new(value, value, value, value);
            ColorArgb64 dst = src;
            ushort expected = (ushort)((b << 8) | b);
            Assert.AreEqual(expected, dst.Red, $"ExpandByte({b}) red");
        }
    }

    [TestMethod]
    public void ExpandByte_RoundTrip_8to16to8_IsExact()
    {
        // Converting a32 → a64 → a32 must recover the original byte exactly.
        for (int b = 0; b <= 255; b++)
        {
            byte value = (byte)b;
            ColorArgb32 orig = new(value, value, value, value);
            ColorArgb64 mid = orig;
            ColorArgb32 back = mid;
            Assert.AreEqual(value, back.Red, $"round-trip for byte {b}");
        }
    }

    [TestMethod]
    public void ExpandByte_KnownValues()
    {
        Assert.AreEqual((ushort)0,     ((ColorArgb64)new ColorArgb32(0,   0,   0,   0  )).Alpha);
        Assert.AreEqual((ushort)257,   ((ColorArgb64)new ColorArgb32(1,   1,   1,   1  )).Alpha);
        Assert.AreEqual((ushort)32896, ((ColorArgb64)new ColorArgb32(128, 128, 128, 128)).Alpha);
        Assert.AreEqual((ushort)65535, ((ColorArgb64)new ColorArgb32(255, 255, 255, 255)).Alpha);
    }

    // ── Finding #9: ColorArgb64 → ColorArgb wrong divisor ────────────────────

    [TestMethod]
    public void ColorArgbFromColorArgb64_MaxValue_ReturnsOne()
    {
        ColorArgb64 src = new(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
        ColorArgb dst = src;
        Assert.AreEqual(1.0, dst.Alpha, 1e-9, "alpha 65535 must map to 1.0");
        Assert.AreEqual(1.0, dst.Red,   1e-9, "red 65535 must map to 1.0");
        Assert.AreEqual(1.0, dst.Green, 1e-9, "green 65535 must map to 1.0");
        Assert.AreEqual(1.0, dst.Blue,  1e-9, "blue 65535 must map to 1.0");
    }

    [TestMethod]
    public void ColorArgbFromColorArgb64_Zero_ReturnsZero()
    {
        ColorArgb64 src = new(0, 0, 0, 0);
        ColorArgb dst = src;
        Assert.AreEqual(0.0, dst.Alpha, 1e-9);
        Assert.AreEqual(0.0, dst.Red,   1e-9);
        Assert.AreEqual(0.0, dst.Green, 1e-9);
        Assert.AreEqual(0.0, dst.Blue,  1e-9);
    }

    [TestMethod]
    public void ColorArgbFromColorArgb64_HalfMaxValue_ReturnsHalf()
    {
        // 32767.5 → choose 32768 (ushort.MaxValue / 2 + 1) since exact half
        // is not representable; at minimum must be close to 0.5.
        ColorArgb64 src = new(32768, 32768, 32768, 32768);
        ColorArgb dst = src;
        Assert.AreEqual(32768.0 / 65535.0, dst.Red, 1e-9, "mid-range must use 65535 as divisor");
    }

    [TestMethod]
    public void ColorArgbFromColorArgb64_RoundTrip_IsClose()
    {
        // ColorArgb64 → ColorArgb → ColorArgb64 round trip should stay within 1 ULP.
        ColorArgb64 orig = new(1000, 2000, 3000, 4000);
        ColorArgb mid = orig;
        ColorArgb64 back = mid;
        Assert.IsTrue(System.Math.Abs(orig.Alpha - back.Alpha) <= 1, "alpha round-trip");
        Assert.IsTrue(System.Math.Abs(orig.Red   - back.Red)   <= 1, "red round-trip");
        Assert.IsTrue(System.Math.Abs(orig.Green - back.Green) <= 1, "green round-trip");
        Assert.IsTrue(System.Math.Abs(orig.Blue  - back.Blue)  <= 1, "blue round-trip");
    }

    // ── Finding #23: ColorArgb32(IColorArgb<byte>) wrong scaling ─────────────

    [TestMethod]
    public void ColorArgb32FromIColorArgbByte_PreservesComponents()
    {
        // IColorArgb<byte> copy must not multiply by 255.
        IColorArgb<byte> src = new ColorArgb32(200, 10, 50, 150);
        ColorArgb32 dst = new(src);
        Assert.AreEqual(200, dst.Alpha, "alpha must be copied directly");
        Assert.AreEqual(10,  dst.Red,   "red must be copied directly");
        Assert.AreEqual(50,  dst.Green, "green must be copied directly");
        Assert.AreEqual(150, dst.Blue,  "blue must be copied directly");
    }

    [TestMethod]
    public void ColorArgb32FromIColorArgbByte_MaxValue_DoesNotOverflow()
    {
        IColorArgb<byte> src = new ColorArgb32(255, 255, 255, 255);
        ColorArgb32 dst = new(src);
        Assert.AreEqual(255, dst.Alpha);
        Assert.AreEqual(255, dst.Red);
        Assert.AreEqual(255, dst.Green);
        Assert.AreEqual(255, dst.Blue);
    }
}
