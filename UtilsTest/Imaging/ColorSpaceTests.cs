using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ColorSpaceTests
{
    [TestMethod]
    public void AlabStoresValues()
    {
        var color = new ColorAlab(0.5, 50, 10, -10);
        Assert.AreEqual(0.5, color.Alpha, 1e-6);
        Assert.AreEqual(50, color.L, 1e-6);
        Assert.AreEqual(10, color.A, 1e-6);
        Assert.AreEqual(-10, color.B, 1e-6);
    }

    [TestMethod]
    public void AcymStoresValues()
    {
        var color = new ColorAcym(0.5, 0.2, 0.7, 0.3);
        Assert.AreEqual(0.5, color.Alpha, 1e-6);
        Assert.AreEqual(0.2, color.Cyan, 1e-6);
        Assert.AreEqual(0.7, color.Yellow, 1e-6);
        Assert.AreEqual(0.3, color.Magenta, 1e-6);
    }

    [TestMethod]
    public void AcmykStoresValues()
    {
        var color = new ColorAcmyk(0.6, 0.1, 0.2, 0.3, 0.4);
        Assert.AreEqual(0.6, color.Alpha, 1e-6);
        Assert.AreEqual(0.1, color.Cyan, 1e-6);
        Assert.AreEqual(0.2, color.Magenta, 1e-6);
        Assert.AreEqual(0.3, color.Yellow, 1e-6);
        Assert.AreEqual(0.4, color.Key, 1e-6);
    }

    [TestMethod]
    public void AlabArgbRoundTrip()
    {
        ColorArgb argb = new(1, 0.3, 0.4, 0.5);
        ColorAlab lab = new(argb);
        ColorArgb result = new(lab);
        Assert.AreEqual(argb.Alpha, result.Alpha, 1e-5);
        Assert.AreEqual(argb.Red, result.Red, 1e-5);
        Assert.AreEqual(argb.Green, result.Green, 1e-5);
        Assert.AreEqual(argb.Blue, result.Blue, 1e-5);
    }

    [TestMethod]
    public void AcymArgbRoundTrip()
    {
        ColorArgb argb = new(1, 0.2, 0.4, 0.6);
        ColorAcym acym = new(argb);
        ColorArgb result = new(acym);
        Assert.AreEqual(argb.Alpha, result.Alpha, 1e-6);
        Assert.AreEqual(argb.Red, result.Red, 1e-6);
        Assert.AreEqual(argb.Green, result.Green, 1e-6);
        Assert.AreEqual(argb.Blue, result.Blue, 1e-6);
    }

    [TestMethod]
    public void AcmykArgbRoundTrip()
    {
        ColorArgb argb = new(1, 0.1, 0.2, 0.3);
        ColorAcmyk cmyk = new(argb);
        ColorArgb result = new(cmyk);
        Assert.AreEqual(argb.Alpha, result.Alpha, 1e-6);
        Assert.AreEqual(argb.Red, result.Red, 1e-6);
        Assert.AreEqual(argb.Green, result.Green, 1e-6);
        Assert.AreEqual(argb.Blue, result.Blue, 1e-6);
    }
}

