using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ColorArgbTests
{
    [TestMethod]
    public void Gradient_AtZero_ReturnsFirstColor()
    {
        ColorArgb c1 = new(1, 1, 0, 0);
        ColorArgb c2 = new(1, 0, 0, 1);
        ColorArgb result = ColorArgb.Gradient(c1, c2, 0);
        Assert.AreEqual(c1.Red, result.Red, 1e-9);
        Assert.AreEqual(c1.Blue, result.Blue, 1e-9);
    }

    [TestMethod]
    public void Gradient_AtOne_ReturnsSecondColor()
    {
        ColorArgb c1 = new(1, 1, 0, 0);
        ColorArgb c2 = new(1, 0, 0, 1);
        ColorArgb result = ColorArgb.Gradient(c1, c2, 1);
        Assert.AreEqual(c2.Red, result.Red, 1e-9);
        Assert.AreEqual(c2.Blue, result.Blue, 1e-9);
    }

    [TestMethod]
    public void Gradient_AtHalf_IsPerceptualMidpoint()
    {
        // Perceptual midpoint between black (0) and white (1): sqrt(0.5) ≈ 0.707
        ColorArgb black = new(1, 0, 0, 0);
        ColorArgb white = new(1, 1, 1, 1);
        ColorArgb mid = ColorArgb.Gradient(black, white, 0.5);
        double expected = Math.Sqrt(0.5);
        Assert.AreEqual(expected, mid.Red, 1e-9);
        Assert.AreEqual(expected, mid.Green, 1e-9);
        Assert.AreEqual(expected, mid.Blue, 1e-9);
    }

    [TestMethod]
    public void Gradient_ClampsBelowZero()
    {
        ColorArgb c1 = new(1, 0.5, 0.5, 0.5);
        ColorArgb c2 = new(1, 0.5, 0.5, 0.5);
        // percent < 0 is clamped to 0 → same as c1
        ColorArgb result = ColorArgb.Gradient(c1, c2, -1);
        Assert.AreEqual(c1.Red, result.Red, 1e-9);
    }

    [TestMethod]
    public void Gradient_ClampsAboveOne()
    {
        ColorArgb c1 = new(1, 0.5, 0.5, 0.5);
        ColorArgb c2 = new(1, 0.5, 0.5, 0.5);
        ColorArgb result = ColorArgb.Gradient(c1, c2, 2);
        Assert.AreEqual(c2.Red, result.Red, 1e-9);
    }
}
