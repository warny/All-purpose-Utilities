using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Tests numeric overload and formatting-option contracts without owning natural-language wording.</summary>
[TestClass]
public class NumberToStringConverterCoverageGapsTests
{
    /// <summary>Verifies that the double overload follows the equivalent decimal conversion path.</summary>
    [TestMethod]
    public void Convert_Double_MatchesEquivalentDecimal()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual(converter.Convert(1.5m), converter.Convert(1.5));
    }

    /// <summary>Verifies custom decimal separator and suffix markers independently of numeral wording.</summary>
    [TestMethod]
    public void DecimalFormatOptions_CustomMarkersAreApplied()
    {
        var converter = NumberToStringConverter.GetConverter("EN");
        var options = new DecimalFormatOptions
        {
            DecimalSeparator = "UNIT(s)",
            DecimalSuffix = "SUBUNIT(s)"
        };

        string result = converter.Convert(21.50m, 2, options);

        StringAssert.Contains(result, "UNITs");
        StringAssert.Contains(result, "SUBUNITs");
    }

    /// <summary>Verifies that significant-digit rounding is identical for BigInteger and the rounded value.</summary>
    [TestMethod]
    public void Convert_BigInteger_SignificantDigitsMatchesRoundedValue()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual(converter.Convert((BigInteger)100_000_000), converter.Convert((BigInteger)123_456_789, 1));
    }

    /// <summary>Verifies that a caller variant survives significant-digit BigInteger conversion.</summary>
    [TestMethod]
    public void Convert_BigInteger_SignificantDigitsPreservesVariants()
    {
        var converter = NumberToStringConverter.GetConverter("ES");

        Assert.AreEqual(
            converter.Convert((BigInteger)200_000_000, "gender=femenino"),
            converter.Convert((BigInteger)223_456_789, 1, "gender=femenino"));
    }

    /// <summary>Verifies that a configured era suffix composes with variants without fixing language wording.</summary>
    [TestMethod]
    public void ConvertYear_Negative_WithVariant_AppendsConfiguredSuffix()
    {
        var source = NumberToStringConverter.GetConverter("FR");
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            YearFormat = new YearFormatOptions(null, null, null, BeforeChristSuffix: "ERA")
        });

        Assert.AreEqual($"{source.Convert(1)} ERA", converter.ConvertYear(-1));
        Assert.AreEqual($"{source.Convert(1, "gender=feminin")} ERA", converter.ConvertYear(-1, "gender=feminin"));
        Assert.AreEqual(source.Convert(1), converter.ConvertYear(1));
    }
}
