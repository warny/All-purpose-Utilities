using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Numerics;

namespace UtilsTest.Numerics;

[TestClass]
public class NumberTests
{
    [TestMethod]
    public void IntegerAdditionTest()
    {
        Number a = Number.Parse("123456789012345678901234567890");
        Number b = 1;
        Assert.AreEqual("123456789012345678901234567891", (a + b).ToString());
    }

    [TestMethod]
    public void DecimalAdditionTest()
    {
        Number c = Number.Parse("0.1");
        Number d = Number.Parse("0.2");
        Assert.AreEqual("0.3", (c + d).ToString());
    }

    [TestMethod]
    public void MultiplicationTest()
    {
        Number x = 10;
        Number y = Number.Parse("0.5");
        Assert.AreEqual("5", (x * y).ToString());
    }

    [TestMethod]
    public void UnaryNegationTest()
    {
        Number value = Number.Parse("1.5");
        Assert.AreEqual("-1.5", (-value).ToString());
    }

    [TestMethod]
    public void ComparisonOperatorsTest()
    {
        Number a = Number.Parse("2");
        Number b = Number.Parse("3");
        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
        Assert.IsTrue(a <= b);
        Assert.IsTrue(b >= a);
    }

    [TestMethod]
    public void TryParseTest()
    {
        bool ok = Number.TryParse("10.5", null, out Number value);
        Assert.IsTrue(ok);
        Assert.AreEqual("10.5", value.ToString());

        Assert.IsFalse(Number.TryParse("not_a_number", null, out _));
    }

    [TestMethod]
    public void PowIntegerTest()
    {
        Number baseValue = 2;
        Number exponent = 3;
        Assert.AreEqual("8", Number.Pow(baseValue, exponent).ToString());
    }
}
