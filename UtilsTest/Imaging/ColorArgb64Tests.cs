using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ColorArgb64Tests
{
    [TestMethod]
    public void ImplicitConversionFromColorArgb32ExpandsComponents()
    {
        ColorArgb32 source = new(128, 10, 20, 30);
        ColorArgb64 converted = source;

        Assert.AreEqual((ushort)(128 << 8), converted.Alpha);
        Assert.AreEqual((ushort)(10 << 8), converted.Red);
        Assert.AreEqual((ushort)(20 << 8), converted.Green);
        Assert.AreEqual((ushort)(30 << 8), converted.Blue);
    }

    [TestMethod]
    public void AddWithEqualAlphaAveragesComponents()
    {
        ColorArgb64 first = new(ushort.MaxValue, 1000, 4000, 6000);
        ColorArgb64 second = new(ushort.MaxValue, 2000, 2000, 2000);

        ColorArgb64 result = (ColorArgb64)first.Add(second);

        Assert.AreEqual(ushort.MaxValue, result.Alpha);
        Assert.AreEqual((ushort)1500, result.Red);
        Assert.AreEqual((ushort)3000, result.Green);
        Assert.AreEqual((ushort)4000, result.Blue);
    }

    [TestMethod]
    public void OverWithOpaqueForegroundKeepsForegroundColor()
    {
        ColorArgb64 foreground = new(ushort.MaxValue, 3000, 4000, 5000);
        ColorArgb64 background = new(1000, 1000, 2000, 3000);

        ColorArgb64 result = (ColorArgb64)foreground.Over(background);

        Assert.AreEqual(foreground.Alpha, result.Alpha);
        Assert.AreEqual(foreground.Red, result.Red);
        Assert.AreEqual(foreground.Green, result.Green);
        Assert.AreEqual(foreground.Blue, result.Blue);
    }

    [TestMethod]
    public void SubtractReturnsComponentWiseDifference()
    {
        ColorArgb64 first = new(4000, 3000, 2000, 1000);
        ColorArgb64 second = new(1000, 4000, 1500, 6000);

        ColorArgb64 result = (ColorArgb64)first.Subtract(second);

        Assert.AreEqual((ushort)1000, result.Alpha);
        Assert.AreEqual((ushort)3000, result.Red);
        Assert.AreEqual((ushort)1500, result.Green);
        Assert.AreEqual((ushort)1000, result.Blue);
    }

    [TestMethod]
    public void ConstructorFromArrayReadsSequentialChannels()
    {
        ushort[] values =
        {
                        0, 1, 2, 3,
                        100,
                        500,
                        1000,
                        2000
                };

        ColorArgb64 color = new(values, 4);

        Assert.AreEqual((ushort)100, color.Alpha);
        Assert.AreEqual((ushort)500, color.Red);
        Assert.AreEqual((ushort)1000, color.Green);
        Assert.AreEqual((ushort)2000, color.Blue);
    }

    [TestMethod]
    public void ConstructorFromSystemDrawingColorExpandsChannels()
    {
        System.Drawing.Color source = System.Drawing.Color.FromArgb(25, 12, 34, 56);

        ColorArgb64 color = new(source);

        Assert.AreEqual((ushort)(25 << 8), color.Alpha);
        Assert.AreEqual((ushort)(12 << 8), color.Red);
        Assert.AreEqual((ushort)(34 << 8), color.Green);
        Assert.AreEqual((ushort)(56 << 8), color.Blue);
    }

    [TestMethod]
    public void ToStringProducesReadableOutput()
    {
        ColorArgb64 color = new(ushort.MaxValue, 1, 2, 3);
        string result = color.ToString();

        StringAssert.Contains(result, "a:");
        StringAssert.Contains(result, "R:");
        StringAssert.Contains(result, "G:");
        StringAssert.Contains(result, "B:");
    }

    [TestMethod]
    public void Value_IncludesAlphaChannel()
    {
        // Two colors with same RGB but different alpha must have different Value.
        ColorArgb64 c1 = new(1000, 500, 300, 200);
        ColorArgb64 c2 = new(2000, 500, 300, 200);
        Assert.AreNotEqual(c1.Value, c2.Value);
    }

    [TestMethod]
    public void UlongConstructor_SetsAllFourChannels()
    {
        // little-endian layout: blue[0-1] green[2-3] red[4-5] alpha[6-7]
        // ulong 0xAAAABBBBCCCCDDDD → alpha=AAAA red=BBBB green=CCCC blue=DDDD
        ulong packed = 0xAAAABBBBCCCCDDDDUL;
        ColorArgb64 color = new(packed);

        Assert.AreEqual((ushort)0xAAAA, color.Alpha);
        Assert.AreEqual((ushort)0xBBBB, color.Red);
        Assert.AreEqual((ushort)0xCCCC, color.Green);
        Assert.AreEqual((ushort)0xDDDD, color.Blue);
    }

    [TestMethod]
    public void Equality_DifferingOnlyInAlpha_AreNotEqual()
    {
        ColorArgb64 c1 = new(1000, 500, 300, 200);
        ColorArgb64 c2 = new(9999, 500, 300, 200);
        Assert.AreNotEqual(c1, c2);
        Assert.IsFalse(c1 == c2);
        Assert.AreNotEqual(c1.GetHashCode(), c2.GetHashCode());
    }
}
