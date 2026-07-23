using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for audit findings 47–71 from the Utils.NumberToString TODO files (pass 1 and pass 2).
/// </summary>
[TestClass]
public class NumberToStringConverterAuditFixesTests
{
    private static NumberToStringConverter EN => NumberToStringConverter.GetConverter("EN");
    private static NumberToStringConverter FR => NumberToStringConverter.GetConverter("FR");

    // ── Item 50 — Minimum signed values overflow ──────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_Long_MinValue_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertOrdinal(long.MinValue),
            "ConvertOrdinal(long.MinValue) must throw because abs value exceeds long.MaxValue");
    }

    [TestMethod]
    public void ConvertOrdinal_Long_MinValuePlusOne_Succeeds()
    {
        var c = EN;
        // long.MinValue + 1 has abs = long.MaxValue which fits in long
        string result = c.ConvertOrdinal(long.MinValue + 1);
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
    }

    [TestMethod]
    public void ConvertOrdinal_Long_MaxValue_Succeeds()
    {
        var c = EN;
        string result = c.ConvertOrdinal(long.MaxValue);
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
    }

    [TestMethod]
    public void ConvertYear_Int_MinValue_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertYear(int.MinValue),
            "ConvertYear(int.MinValue) must throw because abs value exceeds int.MaxValue");
    }

    [TestMethod]
    public void ConvertYear_Int_MinValuePlusOne_Succeeds()
    {
        var c = EN;
        // int.MinValue + 1 = -2147483647, abs = 2147483647 = int.MaxValue
        string result = c.ConvertYear(int.MinValue + 1);
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
    }

    [TestMethod]
    public void Convert_TimeSpan_MinValue_ThrowsArgumentOutOfRange()
    {
        var fr = FR;
        // FR has TimeUnits configured
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => fr.Convert(TimeSpan.MinValue),
            "Convert(TimeSpan.MinValue) must throw because negation overflows");
    }

    [TestMethod]
    public void Convert_TimeSpan_AdjacentToMinValue_Succeeds()
    {
        var fr = FR;
        // One tick after MinValue is safe
        var duration = TimeSpan.FromTicks(long.MinValue + 1);
        // The negation gives a very large positive duration; just check it doesn't throw
        string result = fr.Convert(duration);
        Assert.IsNotNull(result);
    }

    // ── Item 47 — decimal.MinValue overflow in decimal conversion ────────────

    [TestMethod]
    public void Convert_Decimal_MinValue_DoesNotThrowOverflow()
    {
        var c = EN;
        // decimal.MinValue should not throw OverflowException — it is a valid decimal value
        string result = c.Convert(decimal.MinValue);
        Assert.IsNotNull(result);
        StringAssert.StartsWith(result, "minus ");
    }

    [TestMethod]
    public void Convert_Decimal_MinValue_NegativeSymmetricWithMaxValue()
    {
        var c = EN;
        string maxResult = c.Convert(decimal.MaxValue);
        string minResult = c.Convert(decimal.MinValue);
        // MinValue text should be "minus " + MaxValue text
        Assert.AreEqual("minus " + maxResult, minResult);
    }

    [TestMethod]
    public void Convert_Decimal_NearMinValue_Succeeds()
    {
        var c = EN;
        decimal nearMin = decimal.MinValue + 0.1m;
        string result = c.Convert(nearMin);
        Assert.IsNotNull(result);
        StringAssert.StartsWith(result, "minus ");
    }

    // ── Item 48 — Currency restricted to long ────────────────────────────────

    [TestMethod]
    public void ConvertCurrency_LargeAmount_BeyondLongMax_Succeeds()
    {
        var c = EN;
        var eur = new CurrencyDefinition
        {
            UnitSingular = "euro", UnitPlural = "euros",
            SubunitSingular = "cent", SubunitPlural = "cents",
            SubunitDigits = 2
        };
        // long.MaxValue = 9223372036854775807; use a value just above it as decimal
        decimal largeAmount = (decimal)long.MaxValue + 1000m;
        string result = c.ConvertCurrency(largeAmount, eur);
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "euro");
    }

    [TestMethod]
    public void ConvertCurrency_Decimal_MinValue_DoesNotThrowOverflow()
    {
        var c = EN;
        var eur = new CurrencyDefinition
        {
            UnitSingular = "euro", UnitPlural = "euros",
            SubunitSingular = "cent", SubunitPlural = "cents",
            SubunitDigits = 2
        };
        // Should produce a valid "minus N euros..." result, not throw OverflowException
        string result = c.ConvertCurrency(decimal.MinValue, eur);
        Assert.IsNotNull(result);
        StringAssert.StartsWith(result, "minus ");
    }

    // ── Item 49 — SubunitDigits validation ───────────────────────────────────

    [TestMethod]
    public void ConvertCurrency_SubunitDigits_Negative_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        var badCurrency = new CurrencyDefinition
        {
            UnitSingular = "unit", UnitPlural = "units",
            SubunitSingular = "sub", SubunitPlural = "subs",
            SubunitDigits = -1
        };
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertCurrency(10.5m, badCurrency),
            "Negative SubunitDigits must be rejected");
    }

    [TestMethod]
    public void ConvertCurrency_SubunitDigits_Zero_OnlyUnitsOutput()
    {
        var c = EN;
        var noSubunit = new CurrencyDefinition
        {
            UnitSingular = "dollar", UnitPlural = "dollars",
            SubunitSingular = "cent", SubunitPlural = "cents",
            SubunitDigits = 0
        };
        string result = c.ConvertCurrency(5m, noSubunit);
        StringAssert.Contains(result, "dollar");
        Assert.IsFalse(result.Contains("cent"), $"SubunitDigits=0 should produce no subunit part; got: {result}");
    }

    [TestMethod]
    public void ConvertCurrency_SubunitDigits_TooLarge_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        var badCurrency = new CurrencyDefinition
        {
            UnitSingular = "unit", UnitPlural = "units",
            SubunitSingular = "sub", SubunitPlural = "subs",
            SubunitDigits = 29
        };
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertCurrency(10.5m, badCurrency),
            "SubunitDigits > 28 must be rejected (exceeds decimal precision)");
    }

    // ── Item 52 — Large finite double/float silently loses fractional part ───

    [TestMethod]
    public void Convert_Double_BeyondDecimalRange_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        // double.MaxValue is well beyond decimal range and has a fractional part
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.Convert(double.MaxValue),
            "A double beyond decimal range should throw rather than silently truncate");
    }

    [TestMethod]
    public void Convert_Double_WithinDecimalRange_Succeeds()
    {
        var c = EN;
        // 1.5 is well within decimal range
        string result = c.Convert(1.5);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void Convert_Float_BeyondDecimalRange_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        // float.MaxValue ~ 3.4e38, beyond decimal.MaxValue ~ 7.9e28
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.Convert(float.MaxValue),
            "A float beyond decimal range should throw rather than silently truncate");
    }

    // ── Item 55 — Fraction zero/negative denominator ─────────────────────────

    [TestMethod]
    public void ConvertFraction_ZeroDenominator_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertFraction(1, 0),
            "Zero denominator must be rejected");
    }

    [TestMethod]
    public void ConvertFraction_NegativeDenominator_NormalizesToPositive()
    {
        var c = EN;
        // 1/-2 should be treated as -1/2 (sign normalized to numerator)
        string positive = c.ConvertFraction(-1, 2);
        string withNegDen = c.ConvertFraction(1, -2);
        Assert.AreEqual(positive, withNegDen,
            "1/-2 and -1/2 should produce identical output after sign normalization");
    }

    [TestMethod]
    public void ConvertFraction_BothNegative_NormalizesToPositive()
    {
        var c = EN;
        // -1/-2 = 1/2
        string positive = c.ConvertFraction(1, 2);
        string bothNeg = c.ConvertFraction(-1, -2);
        Assert.AreEqual(positive, bothNeg,
            "-1/-2 should normalize to positive 1/2");
    }

    // ── Item 67 — mandatoryDecimalDigits not validated ───────────────────────

    [TestMethod]
    public void Convert_Decimal_MandatoryDigits_Above28_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.Convert(1.5m, 29),
            "mandatoryDecimalDigits > 28 must be rejected (exceeds decimal precision)");
    }

    [TestMethod]
    public void Convert_Decimal_MandatoryDigits_Exactly28_Succeeds()
    {
        var c = EN;
        // 28 is the maximum valid precision for decimal
        string result = c.Convert(1.5m, 28);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void Convert_Decimal_MandatoryDigits_Zero_SuppressesDecimalPart()
    {
        var c = EN;
        string result = c.Convert(3.7m, 0);
        // Should round to 4 and suppress decimals
        Assert.AreEqual(c.Convert(4m), result);
    }

    // ── Item 62 — Cardinal zero bypasses variants, triggers, finalization ─────

    [TestMethod]
    public void Convert_Zero_WithVariants_VariantRulesApplied_DE()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        // In German, zero with gender=feminin should still go through the pipeline.
        // Even if the output is the same "null", the pipeline must run without error.
        string result = de.Convert(0, "gender=feminin");
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
    }

    [TestMethod]
    public void Convert_Zero_FinalizeWritingApplied()
    {
        // Use a converter with a custom AdjustFunction to verify it runs for zero too
        var options = new NumberToStringConverterOptions(EN)
        {
            AdjustFunction = s => "<<" + s + ">>",
            Zero = "zero"
        };
        var converter = new NumberToStringConverter(options);
        string result = converter.Convert(BigInteger.Zero);
        // AdjustFunction should be applied to zero
        Assert.AreEqual("<<zero>>", result,
            "AdjustFunction must be applied to zero just like any non-zero value");
    }

    [TestMethod]
    public void Convert_Zero_EndTriggerApplied()
    {
        // Use a converter with a trigger on zero to verify triggers run for zero
        var options = new NumberToStringConverterOptions(EN)
        {
            Zero = "zero",
            Triggers =
            [
                new NumberToStringConverter.TriggerRule(
                    NumberToStringConverter.TriggerAt.End,
                    null,
                    [new NumberToStringConverter.TriggerReplace("zero", false, [], "nought")])
            ]
        };
        var converter = new NumberToStringConverter(options);
        string result = converter.Convert(BigInteger.Zero);
        Assert.AreEqual("nought", result,
            "End trigger must fire for zero just like for non-zero values");
    }

    // ── Item 66 — Decimal suffix pluralization narrows to long ───────────────

    [TestMethod]
    public void Convert_Decimal_LargeFractionalPart_DoesNotOverflow()
    {
        // Build a decimal with a large fractional part (many digits) where Fractions
        // is configured for that digit count — pluralization must not cast to long
        var options = new NumberToStringConverterOptions(EN)
        {
            Zero = "zero",
            Fractions = new Dictionary<int, string> { [10] = "ten-billionth(s)" }
        };
        var converter = new NumberToStringConverter(options);
        // 0.0000000001 has 10 fractional digits; the fractional numerator = 1, fits in long
        string result = converter.Convert(0.0000000001m);
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "ten-billionth");
    }
}
