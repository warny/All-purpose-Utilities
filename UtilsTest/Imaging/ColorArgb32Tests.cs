using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ColorArgb32Tests
{
    [TestMethod]
    public void UintConstructor_ExtractsAllChannels()
    {
        // 0xAABBCCDD → alpha=AA red=BB green=CC blue=DD
        ColorArgb32 color = new(0xAABBCCDDu);

        Assert.AreEqual((byte)0xAA, color.Alpha);
        Assert.AreEqual((byte)0xBB, color.Red);
        Assert.AreEqual((byte)0xCC, color.Green);
        Assert.AreEqual((byte)0xDD, color.Blue);
    }

    [TestMethod]
    public void UintConstructor_RoundTripsViaValue()
    {
        ColorArgb32 color = new(0x01020304u);
        Assert.AreEqual(0x01020304u, color.Value);
    }

    [TestMethod]
    public void UintConstructor_EqualsComponentConstructor()
    {
        ColorArgb32 fromUint = new(0x7F102030u);
        ColorArgb32 fromComponents = new(0x7F, 0x10, 0x20, 0x30);
        Assert.AreEqual(fromComponents, fromUint);
    }
}
