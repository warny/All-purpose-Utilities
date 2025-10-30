using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Numerics;
using System;
using System.Globalization;

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
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.AreEqual("0,3", (c + d).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
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
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.AreEqual("-1,5", (-value).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
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
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.AreEqual("10,5", value.ToString());
            Assert.AreEqual("21/2", value.ToString("Q", CultureInfo.InvariantCulture));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        Assert.IsFalse(Number.TryParse("not_a_number", null, out _));
    }

    [TestMethod]
    public void ToStringWithFormatOverloadUsesCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Number value = Number.Parse("2.75");
            Assert.AreEqual("2,75", value.ToString("G"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void PowIntegerTest()
    {
        Number baseValue = 2;
        Number exponent = 3;
        Assert.AreEqual("8", Number.Pow(baseValue, exponent).ToString());
    }

    [TestMethod]
    public void SqrtTest()
    {
        Number value = 9;
        Number result = Number.Sqrt(value);
        Assert.AreEqual("3", result.ToString());
    }

    [TestMethod]
    public void CosTest()
    {
        Number angle = Number.Parse("0.5");
        Number result = Number.Cos(angle);
        string expected = Number.Parse(Math.Cos(0.5).ToString("R", CultureInfo.InvariantCulture)).ToString();
        Assert.AreEqual(expected, result.ToString());
    }
}
