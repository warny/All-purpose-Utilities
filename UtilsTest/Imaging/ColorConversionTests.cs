using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ColorConversionTests
{
    [TestMethod]
    public void RoundTripDouble()
    {
        ColorArgb orig = new(0.8, 0.1, 0.2, 0.3);
        ColorAhsv hsv = ColorAhsv.FromArgbColor(orig);
        ColorArgb result = hsv.ToArgbColor();
        Assert.AreEqual(orig.Alpha, result.Alpha, 1e-6);
        Assert.AreEqual(orig.Red, result.Red, 1e-6);
        Assert.AreEqual(orig.Green, result.Green, 1e-6);
        Assert.AreEqual(orig.Blue, result.Blue, 1e-6);
    }

    [TestMethod]
    public void RoundTripByte()
    {
        ColorArgb32 orig = new(byte.MaxValue, 10, 20, 30);
        ColorAhsv32 hsv = ColorAhsv32.FromArgbColor(orig);
        ColorArgb32 result = hsv.ToArgbColor();
        Assert.IsTrue(Math.Abs(orig.Alpha - result.Alpha) <= 1);
        Assert.IsTrue(Math.Abs(orig.Red - result.Red) <= 1);
        Assert.IsTrue(Math.Abs(orig.Green - result.Green) <= 1);
        Assert.IsTrue(Math.Abs(orig.Blue - result.Blue) <= 1);
    }

    [TestMethod]
    public void RoundTripUShort()
    {
        ColorArgb64 orig = new(ushort.MaxValue, 1000, 2000, 3000);
        ColorAhsv64 hsv = ColorAhsv64.FromArgbColor(orig);
        ColorArgb64 result = hsv.ToArgbColor();
        Assert.IsTrue(Math.Abs(orig.Alpha - result.Alpha) <= 1);
        Assert.IsTrue(Math.Abs(orig.Red - result.Red) <= 1);
        Assert.IsTrue(Math.Abs(orig.Green - result.Green) <= 1);
        Assert.IsTrue(Math.Abs(orig.Blue - result.Blue) <= 1);
    }
}

