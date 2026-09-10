using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Verifies language-neutral default-interface conversion contracts.</summary>
[TestClass]
public class NumberToStringConverterEngineImprovementsTests
{
    /// <summary>Verifies that the default double overload delegates to decimal conversion.</summary>
    [TestMethod]
    public void Convert_Double_DelegatesToDecimal()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(converter.Convert(3.14m), converter.Convert(3.14));
        Assert.AreEqual(converter.Convert(2.5m), converter.Convert(2.5));
    }

    /// <summary>Verifies that the default float overload delegates consistently with double.</summary>
    [TestMethod]
    public void Convert_Float_DelegatesToDouble()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(converter.Convert(2.5), converter.Convert((float)2.5));
    }

    /// <summary>Verifies that variants survive float-overload delegation.</summary>
    [TestMethod]
    public void Convert_Float_WithVariants_DelegatesToDouble()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            VariantDimensions = [new NumberToStringConverter.VariantDimension("form", ["base", "alternate"])],
        };
        INumberToStringConverter converter = new NumberToStringConverter(options);

        Assert.AreEqual(converter.Convert(2.5, "form=alternate"), converter.Convert((float)2.5, "form=alternate"));
    }

    /// <summary>Verifies the language-neutral fraction fallback supplied by the interface.</summary>
    [TestMethod]
    public void ConvertFraction_Interface_DefaultFallback()
    {
        INumberToStringConverter converter = new MinimalConverter();
        Assert.AreEqual("2 / 4", converter.ConvertFraction(2, 4));
    }

    /// <summary>Verifies that multiplicative conversion is unsupported by default.</summary>
    [TestMethod]
    public void SupportsMultiplicative_Interface_DefaultFalse()
    {
        INumberToStringConverter converter = new MinimalConverter();
        Assert.IsFalse(converter.SupportsMultiplicative);
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertMultiplicative(1));
    }

    /// <summary>Minimal language-neutral implementation used to exercise default interface members.</summary>
    private sealed class MinimalConverter : INumberToStringConverter
    {
        /// <summary>Gets the unrestricted maximum value.</summary>
        public BigInteger? MaxNumber => null;

        /// <summary>Formats a big integer as invariant digits.</summary>
        public string Convert(BigInteger number) => number.ToString();

        /// <summary>Formats an integer as invariant digits.</summary>
        public string Convert(int number) => number.ToString();

        /// <summary>Formats a long integer as invariant digits.</summary>
        public string Convert(long number) => number.ToString();

        /// <summary>Formats a decimal as invariant digits.</summary>
        public string Convert(decimal number) => number.ToString();
    }
}
