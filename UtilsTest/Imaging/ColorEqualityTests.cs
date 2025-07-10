using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Imaging;

namespace UtilsTest.Imaging;

[TestClass]
public class ColorEqualityTests
{
    [TestMethod]
    public void Argb32Equality()
    {
        ColorArgb32 c1 = new(byte.MaxValue, 10, 20, 30);
        ColorArgb32 c2 = new(byte.MaxValue, 10, 20, 30);
        ColorArgb32 c3 = new(byte.MaxValue, 20, 20, 30);

        Assert.IsTrue(c1 == c2);
        Assert.IsFalse(c1 != c2);
        Assert.IsTrue(c1.Equals(c2));
        Assert.IsFalse(c1 == c3);
        Assert.IsTrue(c1 != c3);
    }

    [TestMethod]
    public void Argb64Equality()
    {
        ColorArgb64 c1 = new(ushort.MaxValue, 1000, 2000, 3000);
        ColorArgb64 c2 = new(ushort.MaxValue, 1000, 2000, 3000);
        ColorArgb64 c3 = new(ushort.MaxValue, 2000, 2000, 3000);

        Assert.IsTrue(c1 == c2);
        Assert.IsFalse(c1 != c2);
        Assert.IsTrue(c1.Equals(c2));
        Assert.IsFalse(c1 == c3);
        Assert.IsTrue(c1 != c3);
    }

    [TestMethod]
    public void ArgbEquality()
    {
        ColorArgb c1 = new(1.0, 0.1, 0.2, 0.3);
        ColorArgb c2 = new(1.0, 0.1, 0.2, 0.3);
        ColorArgb c3 = new(1.0, 0.2, 0.2, 0.3);

        Assert.IsTrue(c1 == c2);
        Assert.IsFalse(c1 != c2);
        Assert.IsTrue(c1.Equals(c2));
        Assert.IsFalse(c1 == c3);
        Assert.IsTrue(c1 != c3);
    }

    [TestMethod]
    public void Ahsv32Equality()
    {
        ColorAhsv32 c1 = new(byte.MaxValue, 10, 20, 30);
        ColorAhsv32 c2 = new(byte.MaxValue, 10, 20, 30);
        ColorAhsv32 c3 = new(byte.MaxValue, 20, 20, 30);

        Assert.IsTrue(c1 == c2);
        Assert.IsFalse(c1 != c2);
        Assert.IsTrue(c1.Equals(c2));
        Assert.IsFalse(c1 == c3);
        Assert.IsTrue(c1 != c3);
    }

    [TestMethod]
    public void Ahsv64Equality()
    {
        ColorAhsv64 c1 = new(ushort.MaxValue, 1000, 2000, 3000);
        ColorAhsv64 c2 = new(ushort.MaxValue, 1000, 2000, 3000);
        ColorAhsv64 c3 = new(ushort.MaxValue, 2000, 2000, 3000);

        Assert.IsTrue(c1 == c2);
        Assert.IsFalse(c1 != c2);
        Assert.IsTrue(c1.Equals(c2));
        Assert.IsFalse(c1 == c3);
        Assert.IsTrue(c1 != c3);
    }

    [TestMethod]
    public void AhsvEquality()
    {
        ColorAhsv c1 = new(1.0, 90.0, 0.5, 0.6);
        ColorAhsv c2 = new(1.0, 90.0, 0.5, 0.6);
        ColorAhsv c3 = new(1.0, 120.0, 0.5, 0.6);

        Assert.IsTrue(c1 == c2);
        Assert.IsFalse(c1 != c2);
        Assert.IsTrue(c1.Equals(c2));
        Assert.IsFalse(c1 == c3);
        Assert.IsTrue(c1 != c3);
    }
}

