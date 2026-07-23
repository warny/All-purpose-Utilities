using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

/// <summary>
/// Regression tests for finding #11 — Porter-Duff Over must use the correct
/// straight-alpha formula for all three color types:
///   α_out = α_src + α_dst × (1 − α_src)
///   C_out = (C_src × α_src + C_dst × α_dst × (1 − α_src)) / α_out
/// </summary>
[TestClass]
public class PorterDuffOverTests
{
    // ── ColorArgb (double, [0,1]) ─────────────────────────────────────────────

    [TestMethod]
    public void ColorArgb_Over_OpaqueOnOpaque_ReturnsSource()
    {
        // When foreground is fully opaque, Over must return the foreground exactly.
        var fg = new ColorArgb(1.0, 0.8, 0.2, 0.4);
        var bg = new ColorArgb(1.0, 0.1, 0.9, 0.5);
        var result = (ColorArgb)fg.Over(bg);

        Assert.AreEqual(1.0, result.Alpha, 1e-9);
        Assert.AreEqual(0.8, result.Red,   1e-9);
        Assert.AreEqual(0.2, result.Green, 1e-9);
        Assert.AreEqual(0.4, result.Blue,  1e-9);
    }

    [TestMethod]
    public void ColorArgb_Over_TransparentFg_ReturnsBackground()
    {
        // Fully transparent foreground: Over must return background unchanged.
        var fg = new ColorArgb(0.0, 1.0, 0.0, 0.0);
        var bg = new ColorArgb(0.6, 0.1, 0.9, 0.3);
        var result = (ColorArgb)fg.Over(bg);

        Assert.AreEqual(0.6, result.Alpha, 1e-9);
        Assert.AreEqual(0.1, result.Red,   1e-9);
        Assert.AreEqual(0.9, result.Green, 1e-9);
        Assert.AreEqual(0.3, result.Blue,  1e-9);
    }

    [TestMethod]
    public void ColorArgb_Over_BothTransparent_ReturnsBlack()
    {
        var fg = new ColorArgb(0.0, 0.5, 0.5, 0.5);
        var bg = new ColorArgb(0.0, 0.5, 0.5, 0.5);
        var result = (ColorArgb)fg.Over(bg);

        Assert.AreEqual(0.0, result.Alpha, 1e-9);
    }

    [TestMethod]
    public void ColorArgb_Over_SemiTransparentFgOnOpaqueBackground()
    {
        // α_src=0.5, α_dst=1.0 → α_out=1.0
        // C_out = (C_src × 0.5 + C_dst × 1.0 × 0.5) / 1.0 = (C_src + C_dst) / 2
        var fg = new ColorArgb(0.5, 1.0, 0.0, 0.0); // red, half transparent
        var bg = new ColorArgb(1.0, 0.0, 0.0, 1.0); // blue, fully opaque
        var result = (ColorArgb)fg.Over(bg);

        Assert.AreEqual(1.0, result.Alpha, 1e-9, "alpha");
        Assert.AreEqual(0.5, result.Red,   1e-9, "red:   (1×0.5 + 0×0.5)/1");
        Assert.AreEqual(0.0, result.Green, 1e-9, "green: (0×0.5 + 0×0.5)/1");
        Assert.AreEqual(0.5, result.Blue,  1e-9, "blue:  (0×0.5 + 1×0.5)/1");
    }

    [TestMethod]
    public void ColorArgb_Over_SemiTransparentFgOnSemiTransparentBackground()
    {
        // α_src=0.5, α_dst=0.5 → α_out = 0.5 + 0.5×0.5 = 0.75
        // kSrc = 0.5/0.75 = 2/3,  kDst = 0.5×0.5/0.75 = 1/3
        var fg = new ColorArgb(0.5, 1.0, 0.0, 0.0);
        var bg = new ColorArgb(0.5, 0.0, 0.0, 1.0);
        var result = (ColorArgb)fg.Over(bg);

        Assert.AreEqual(0.75, result.Alpha, 1e-9, "alpha");
        Assert.AreEqual(2.0 / 3.0, result.Red,   1e-9, "red");
        Assert.AreEqual(0.0,        result.Green, 1e-9, "green");
        Assert.AreEqual(1.0 / 3.0, result.Blue,  1e-9, "blue");
    }

    [TestMethod]
    public void ColorArgb_Over_AlphaFormula_IsCorrect()
    {
        // α_out = α_src + α_dst × (1 − α_src) must hold for arbitrary inputs.
        var fg = new ColorArgb(0.3, 0.8, 0.2, 0.4);
        var bg = new ColorArgb(0.7, 0.1, 0.9, 0.5);
        var result = (ColorArgb)fg.Over(bg);

        double expectedAlpha = 0.3 + 0.7 * (1.0 - 0.3);
        Assert.AreEqual(expectedAlpha, result.Alpha, 1e-9);
    }

    // ── ColorArgb32 (byte, [0,255]) ───────────────────────────────────────────

    [TestMethod]
    public void ColorArgb32_Over_OpaqueOnOpaque_ReturnsSource()
    {
        var fg = new ColorArgb32(255, 200, 50, 100);
        var bg = new ColorArgb32(255, 10, 230, 130);
        var result = (ColorArgb32)fg.Over(bg);

        Assert.AreEqual(255, result.Alpha);
        Assert.AreEqual(200, result.Red);
        Assert.AreEqual(50,  result.Green);
        Assert.AreEqual(100, result.Blue);
    }

    [TestMethod]
    public void ColorArgb32_Over_TransparentFg_ReturnsBackground()
    {
        var fg = new ColorArgb32(0,   255, 0, 0);
        var bg = new ColorArgb32(128, 10, 230, 130);
        var result = (ColorArgb32)fg.Over(bg);

        Assert.AreEqual(128, result.Alpha);
        Assert.AreEqual(10,  result.Red,   3, "red within 3 LSB (rounding)");
        Assert.AreEqual(230, result.Green, 3);
        Assert.AreEqual(130, result.Blue,  3);
    }

    [TestMethod]
    public void ColorArgb32_Over_AlphaFormula_IsCorrect()
    {
        var fg = new ColorArgb32(128, 200, 50, 100);
        var bg = new ColorArgb32(200, 10,  230, 130);
        var result = (ColorArgb32)fg.Over(bg);

        // α_out = 128/255 + 200/255 × (1 − 128/255)
        double aS = 128.0 / 255.0;
        double aD = 200.0 / 255.0;
        int expectedAlpha = (int)System.Math.Round((aS + aD * (1.0 - aS)) * 255.0);
        Assert.AreEqual(expectedAlpha, result.Alpha, 1, "alpha within 1 LSB (rounding)");
    }

    [TestMethod]
    public void ColorArgb32_Over_SemiTransparentFgOnOpaqueBackground_CorrectBlend()
    {
        // α_src=128≈0.502, α_dst=255=1 → α_out ≈ 1
        // C_out ≈ (C_src × 0.502 + C_dst × 0.498)
        var fg = new ColorArgb32(128, 255, 0, 0);  // red
        var bg = new ColorArgb32(255, 0,   0, 255); // blue
        var result = (ColorArgb32)fg.Over(bg);

        // Red channel: ~0.502 × 1 + 0.498 × 0 ≈ 128
        // Blue channel: ~0.502 × 0 + 0.498 × 1 ≈ 127
        Assert.IsTrue(result.Red  > 100, "red must be significant");
        Assert.IsTrue(result.Blue > 100, "blue must be significant");
        Assert.IsTrue(result.Red + result.Blue > 230, "total must be near max");
    }

    // ── ColorArgb64 (ushort, [0,65535]) ──────────────────────────────────────

    [TestMethod]
    public void ColorArgb64_Over_OpaqueOnOpaque_ReturnsSource()
    {
        var fg = new ColorArgb64(ushort.MaxValue, 3000, 4000, 5000);
        var bg = new ColorArgb64(1000, 1000, 2000, 3000);
        var result = (ColorArgb64)fg.Over(bg);

        Assert.AreEqual(ushort.MaxValue, result.Alpha);
        Assert.AreEqual((ushort)3000,   result.Red);
        Assert.AreEqual((ushort)4000,   result.Green);
        Assert.AreEqual((ushort)5000,   result.Blue);
    }

    [TestMethod]
    public void ColorArgb64_Over_TransparentFg_ReturnsBackground()
    {
        var fg = new ColorArgb64(0, 65535, 0, 0);
        var bg = new ColorArgb64(10000, 1000, 2000, 3000);
        var result = (ColorArgb64)fg.Over(bg);

        Assert.AreEqual((ushort)10000, result.Alpha);
        Assert.AreEqual((ushort)1000,  result.Red,   2, "red within 2 ULP");
        Assert.AreEqual((ushort)2000,  result.Green, 2);
        Assert.AreEqual((ushort)3000,  result.Blue,  2);
    }

    [TestMethod]
    public void ColorArgb64_Over_AlphaFormula_IsCorrect()
    {
        var fg = new ColorArgb64(32768, 10000, 20000, 30000);
        var bg = new ColorArgb64(50000,  5000, 15000, 25000);
        var result = (ColorArgb64)fg.Over(bg);

        double aS = 32768.0 / 65535.0;
        double aD = 50000.0 / 65535.0;
        int expectedAlpha = (int)System.Math.Round((aS + aD * (1.0 - aS)) * 65535.0);
        Assert.AreEqual(expectedAlpha, result.Alpha, 1, "alpha within 1 ULP");
    }

    [TestMethod]
    public void ColorArgb64_Over_SemiOnSemi_ColorUsesCorrectWeights()
    {
        // α_src = α_dst = 32768 ≈ 0.5 → α_out = 0.5 + 0.5×0.5 = 0.75
        // kSrc = 0.5/0.75 = 2/3,  kDst = 0.25/0.75 = 1/3
        // C_src = 65535 (max), C_dst = 0 → C_out = max × 2/3
        var fg = new ColorArgb64(32768, 65535, 0, 0);
        var bg = new ColorArgb64(32768, 0,     0, 0);
        var result = (ColorArgb64)fg.Over(bg);

        int expectedRed = (int)System.Math.Round(65535.0 * 2.0 / 3.0);
        Assert.AreEqual(expectedRed, result.Red, 2, "red: C_src×kSrc");
    }
}
