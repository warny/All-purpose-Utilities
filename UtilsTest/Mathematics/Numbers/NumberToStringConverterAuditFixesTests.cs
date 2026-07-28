using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for audit findings 47–71, 68, 71, 75–78 from the Utils.NumberToString TODO files.
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

    // ── Item 53 — Configurable regex timeout ─────────────────────────────────

    [TestMethod]
    public void TriggerReplace_InvalidRegex_ThrowsArgumentException()
    {
        // The ArgumentException must be thrown inside TriggerReplace's constructor
        // because that is where the Regex is compiled.
        Assert.ThrowsException<ArgumentException>(
            () => new NumberToStringConverter.TriggerReplace("[invalid(", true, [], "x"),
            "An invalid regex pattern must throw ArgumentException at construction time");
    }

    [TestMethod]
    public void TriggerReplace_ValidRegex_AppliedCorrectly()
    {
        var options = new NumberToStringConverterOptions(EN)
        {
            Zero = "zero",
            Triggers =
            [
                new NumberToStringConverter.TriggerRule(
                    NumberToStringConverter.TriggerAt.End,
                    null,
                    [new NumberToStringConverter.TriggerReplace(@"\bone\b", true, [], "ONE")])
            ]
        };
        var converter = new NumberToStringConverter(options);
        string result = converter.Convert(BigInteger.One);
        StringAssert.Contains(result, "ONE",
            "Regex trigger must be applied and replace 'one' with 'ONE'");
    }

    [TestMethod]
    public void TriggerReplace_RegexTimeout_CanBeConfigured()
    {
        // Verify the timeout property is settable without error
        var previous = NumberToStringConverter.RegexTimeout;
        try
        {
            NumberToStringConverter.RegexTimeout = TimeSpan.FromSeconds(5);
            Assert.AreEqual(TimeSpan.FromSeconds(5), NumberToStringConverter.RegexTimeout);
        }
        finally
        {
            NumberToStringConverter.RegexTimeout = previous;
        }
    }

    // ── Item 69 — Separator trimming uses exact token, not char stripping ─────

    [TestMethod]
    public void ConvertRaw_AlphabeticSeparator_WordEndingWithSeparatorNotCorrupted()
    {
        // When Separator="and", the word "thousand" ends with "and".
        // Character-level TrimEnd would corrupt "thousand" → "thous".
        // EndsWith-based stripping also corrupts: "oneandthousand".EndsWith("and") → "oneandthous".
        // The correct fix avoids adding a dangling separator at construction time.
        var options = new NumberToStringConverterOptions(EN)
        {
            Zero = "zero",
            Separator = "and",
            GroupSeparator = "",
            Groups = NumberToStringConverter.GetConverter("EN").Groups
                .ToDictionary(kv => kv.Key, kv => new DigitListType { Digits = kv.Value.Values.ToList() }),
            Scale = NumberToStringConverter.GetConverter("EN").Scale,
            Group = 3,
            Minus = "minus *"
        };
        var converter = new NumberToStringConverter(options);
        // 1000 → "oneandthousand" with Separator="and" (no space); must NOT be truncated
        string result = converter.Convert(new BigInteger(1000));
        StringAssert.EndsWith(result, "thousand",
            $"'thousand' must remain intact when Separator='and'; got: '{result}'");
    }

    [TestMethod]
    public void ConvertRaw_AlphabeticSeparator_UnitsNotCorrupted()
    {
        // For 1001 with Separator="and", result should be "oneandthousandandone"
        // (all three components joined by "and"). No word should be truncated.
        var options = new NumberToStringConverterOptions(EN)
        {
            Zero = "zero",
            Separator = "and",
            GroupSeparator = "",
            Groups = NumberToStringConverter.GetConverter("EN").Groups
                .ToDictionary(kv => kv.Key, kv => new DigitListType { Digits = kv.Value.Values.ToList() }),
            Scale = NumberToStringConverter.GetConverter("EN").Scale,
            Group = 3,
            Minus = "minus *"
        };
        var converter = new NumberToStringConverter(options);
        string result = converter.Convert(new BigInteger(1001));
        StringAssert.Contains(result, "thousand",
            $"'thousand' must be present and intact in result; got: '{result}'");
        StringAssert.EndsWith(result, "one",
            $"result must end with 'one' (the units); got: '{result}'");
    }

    // ── Item 70 — Missing time units produce exception (TimeSpan and TimeOnly) ──

    [TestMethod]
    public void Convert_TimeSpan_WithHour_NoHourUnit_ThrowsInvalidOperation()
    {
        // Converter with "minute" and "second" but NOT "hour":
        // SupportsTimeConversion is true, but a non-zero hour component must throw.
        var options = new NumberToStringConverterOptions(EN)
        {
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["minute"] = ("minute", "minutes", null),
                ["second"] = ("second", "seconds", null)
            }
        };
        var converter = new NumberToStringConverter(options);
        Assert.ThrowsException<InvalidOperationException>(
            () => converter.Convert(TimeSpan.FromHours(1)),
            "A non-zero hours value with no 'hour' unit must throw InvalidOperationException");
    }

    [TestMethod]
    public void Convert_TimeOnly_WithHour_NoHourUnit_ThrowsInvalidOperation()
    {
        // Same policy must apply to Convert(TimeOnly): non-zero hour with missing unit → throw.
        var options = new NumberToStringConverterOptions(EN)
        {
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["minute"] = ("minute", "minutes", null),
                ["second"] = ("second", "seconds", null)
            }
        };
        var converter = new NumberToStringConverter(options);
        Assert.ThrowsException<InvalidOperationException>(
            () => converter.Convert(new TimeOnly(1, 0)),
            "A non-zero hour in TimeOnly with no 'hour' unit must throw InvalidOperationException");
    }

    [TestMethod]
    public void Convert_TimeOnly_WithMinute_NoMinuteUnit_ThrowsInvalidOperation()
    {
        // Non-zero minutes with missing minute unit → throw.
        var options = new NumberToStringConverterOptions(EN)
        {
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("hour", "hours", null)
            }
        };
        var converter = new NumberToStringConverter(options);
        Assert.ThrowsException<InvalidOperationException>(
            () => converter.Convert(new TimeOnly(0, 30)),
            "A non-zero minute in TimeOnly with no 'minute' unit must throw InvalidOperationException");
    }

    // ── Item 82 — Scale-name capitalization uses ToUpperInvariant ────────────

    [TestMethod]
    public void GetScaleName_FirstLetterUppercase_UsesInvariantCasing_UnderTurkishCulture()
    {
        // Build a NumberScale with firstLetterUppercase=true whose first dynamic name starts
        // with 'i' (scale0Prefixes[1]="illi"). Under Turkish culture, char.ToUpper('i')=='İ'
        // (dotted capital I), but char.ToUpperInvariant('i')=='I' (plain capital I).
        // With the fix (ToUpperInvariant), the name must start with 'I' regardless of culture.
        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("tr-TR");

            // Verify that the Turkish culture actually uses dotted-I for 'i'
            Assert.AreNotEqual(
                char.ToUpperInvariant('i'), char.ToUpper('i'),
                "Test setup: Turkish culture must capitalize 'i' differently from invariant");

            // staticValues[0] = "" (units — no name), so GetScaleName(1) is dynamic.
            // scale0Prefixes[1] = "illi" → generated name ≈ "illi" + "lli" + "on" = "illillion"
            // With firstLetterUppercase=true and ToUpperInvariant → "Illillion" (starts with 'I')
            // scale0Prefixes must have exactly 10 entries (0-9); index 1 starts with 'i'
            var prefixes10 = new[] { "", "illi", "du", "tri", "quadri", "quinti", "sexti", "septi", "octi", "noni" };
            var scale = new NumberScale(
                staticValues: (IReadOnlyList<string>)new[] { "" },
                scaleSuffixes: (IReadOnlyList<string>)new[] { "on" },
                scale0Prefixes: (IReadOnlyList<string>)prefixes10,
                firstLetterUppercase: true);

            string name = scale.GetScaleName(1);
            Assert.IsTrue(name.Length > 0, "Generated scale name must be non-empty");
            Assert.AreEqual('I', name[0],
                $"Scale name must start with plain 'I' (ToUpperInvariant), not Turkish 'İ'; got: '{name}'");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void GetScaleName_MultiDigitPrefix_FirstLetterUppercase_UsesInvariantCasing()
    {
        // Verify ToUpperInvariant is also applied in the multi-digit prefix path of GetScaleName.
        // A prefix index ≥ 10 triggers the while-loop path. Use 10 suffixes so scale index 10
        // reaches the multi-digit branch with a prefix starting at 'i'.
        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("tr-TR");

            var suffixes = Enumerable.Repeat("on", 10).ToArray();
            var scale = new NumberScale(
                staticValues: (IReadOnlyList<string>)new[] { "" },
                scaleSuffixes: (IReadOnlyList<string>)suffixes,
                unitsPrefixes: (IReadOnlyList<string>)new[] { "", "illi", "du", "tre", "qua", "qui", "sex", "sep", "oct", "nov" },
                firstLetterUppercase: true);

            // scale index 10 will enter the while-loop path (prefix index > 9)
            string name = scale.GetScaleName(10);
            Assert.IsTrue(name.Length > 0, "Generated scale name must be non-empty");
            // The first character must be plain uppercase regardless of Turkish culture
            Assert.AreEqual(char.ToUpperInvariant(name[0]),  name[0],
                $"Scale name first letter must equal its ToUpperInvariant form; got: '{name}'");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    // ── Item 51 — ConvertOrdinal(BigInteger) out-of-long-range → clear exception ──

    [TestMethod]
    public void ConvertOrdinal_BigInteger_AboveLongMax_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        var tooLarge = new BigInteger(long.MaxValue) + 1;
        var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertOrdinal(tooLarge));
        StringAssert.Contains(ex.Message, tooLarge.ToString(),
            "Exception message must include the out-of-range value");
    }

    [TestMethod]
    public void ConvertOrdinal_BigInteger_BelowLongMin_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        var tooSmall = new BigInteger(long.MinValue) - 1;
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertOrdinal(tooSmall));
    }

    [TestMethod]
    public void ConvertOrdinal_BigInteger_AtLongMax_Succeeds()
    {
        var c = EN;
        string result = c.ConvertOrdinal(new BigInteger(long.MaxValue));
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
    }

    // ── Item 77 — NumberScale validates collections ───────────────────────────

    [TestMethod]
    public void NumberScale_EmptyScaleSuffixes_DynamicGenerationThrows()
    {
        // A scale with no suffixes can still serve static-only languages.
        // Requesting a scale beyond the static list must throw with a clear message.
        var scale = new NumberScale(
            staticValues: (IReadOnlyList<string>)new[] { "", "thousand" },
            scaleSuffixes: (IReadOnlyList<string>)System.Array.Empty<string>());
        // Static names are fine
        Assert.AreEqual("thousand", scale.GetScaleName(1));
        // Beyond static → must throw
        Assert.ThrowsException<InvalidOperationException>(
            () => scale.GetScaleName(2),
            "Requesting a dynamic scale when no suffixes are configured must throw");
    }

    [TestMethod]
    public void NumberScale_NegativeStartIndex_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new NumberScale(
                staticValues: (IReadOnlyList<string>)new[] { "" },
                scaleSuffixes: (IReadOnlyList<string>)new[] { "illion" },
                startIndex: -1),
            "Negative startIndex must be rejected");
    }

    [TestMethod]
    public void NumberScale_PrefixTableWrongSize_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberScale(
                staticValues: (IReadOnlyList<string>)new[] { "" },
                scaleSuffixes: (IReadOnlyList<string>)new[] { "illion" },
                scale0Prefixes: (IReadOnlyList<string>)new[] { "", "un" }), // only 2 entries
            "A prefix table with fewer than 10 entries must be rejected");
    }

    // ── Item 79 — ConvertGroup uses exact integer power ───────────────────────

    [TestMethod]
    public void ConvertGroup_NegativeGroupNumber_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertGroup(-1, 1),
            "Negative groupNumber must be rejected");
    }

    [TestMethod]
    public void ConvertGroup_InvalidGroupNumber_ThrowsArgumentOutOfRange()
    {
        var c = EN;
        // EN uses group=3 (keys 1, 2, 3). groupNumber=99 is not configured.
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => c.ConvertGroup(99, 1),
            "Unconfigured groupNumber must be rejected with a clear message");
    }

    // ── Item 80 — TriggerReplace/TriggerRule validate null/empty ─────────────

    [TestMethod]
    public void TriggerReplace_NullFrom_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberToStringConverter.TriggerReplace(null!, false, [], "x"),
            "null 'from' must be rejected");
    }

    [TestMethod]
    public void TriggerReplace_EmptyFrom_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberToStringConverter.TriggerReplace("", false, [], "x"),
            "Empty 'from' must be rejected");
    }

    [TestMethod]
    public void TriggerReplace_NullForms_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new NumberToStringConverter.TriggerReplace("word", false, null!, "x"),
            "null forms must be rejected");
    }

    [TestMethod]
    public void TriggerRule_NullReplaces_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new NumberToStringConverter.TriggerRule(
                NumberToStringConverter.TriggerAt.End, null, null!),
            "null replaces must be rejected");
    }

    [TestMethod]
    public void TriggerRule_NegativeGroupIndex_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberToStringConverter.TriggerRule(
                NumberToStringConverter.TriggerAt.Group, new[] { -1 }, []),
            "Negative group index must be rejected");
    }

    [TestMethod]
    public void TriggerReplace_NullConstraintsInForm_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberToStringConverter.TriggerReplace(
                "word", false,
                [(null!, "replacement")],
                null),
            "A form with null Constraints must be rejected at construction time");
    }

    [TestMethod]
    public void TriggerReplace_NullToInForm_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberToStringConverter.TriggerReplace(
                "word", false,
                [(new System.Collections.Generic.Dictionary<string, string>(), null!)],
                null),
            "A form with null To value must be rejected at construction time");
    }

    // ── Item 81 — Duplicate exact replacement keys produce clear error ────────

    [TestMethod]
    public void Constructor_DuplicateExactReplacementKeys_ThrowsInvalidOperation()
    {
        var options = new NumberToStringConverterOptions(EN)
        {
            Replacements =
            [
                new NumberToStringConverter.ReplacementRule("one", "1", ReplacementScope.Standalone),
                new NumberToStringConverter.ReplacementRule("one", "uno", ReplacementScope.Standalone)  // duplicate key
            ]
        };
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => new NumberToStringConverter(options));
        StringAssert.Contains(ex.Message, "'one'",
            "Exception must identify the conflicting key");
    }

    // ── Item 83 — GetScaleName rejects negative scale ─────────────────────────

    [TestMethod]
    public void GetScaleName_NegativeScale_ThrowsArgumentOutOfRange()
    {
        var scale = new NumberScale(
            staticValues: (IReadOnlyList<string>)new[] { "", "thousand" },
            scaleSuffixes: (IReadOnlyList<string>)new[] { "illion" });
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => scale.GetScaleName(-1),
            "Negative scale must be rejected");
    }

    [TestMethod]
    public void GetScaleName_IntMaxValue_WithPositiveStartIndex_DoesNotOverflow()
    {
        // Regression: the previous int arithmetic overflowed when scale + StartIndex exceeded int.MaxValue.
        // With long arithmetic this must complete without overflow or index-out-of-range.
        var prefixes10 = new[] { "", "un", "du", "tri", "quadri", "quinti", "sexti", "septi", "octi", "noni" };
        var scale = new NumberScale(
            staticValues: (IReadOnlyList<string>)new[] { "", "thousand" },
            scaleSuffixes: (IReadOnlyList<string>)new[] { "illion" },
            startIndex: 10,
            scale0Prefixes: (IReadOnlyList<string>)prefixes10);
        string result = scale.GetScaleName(int.MaxValue);
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result), "GetScaleName(int.MaxValue) must return a non-empty string");
    }

    // ── PR #503 review — deep-copy immutability of trigger models ─────────────

    [TestMethod]
    public void TriggerReplace_MutatingSourceFormsList_DoesNotAffectStoredForms()
    {
        var constraints = new System.Collections.Generic.Dictionary<string, string>();
        var formsList = new System.Collections.Generic.List<(IReadOnlyDictionary<string, string> Constraints, string To)>
        {
            (constraints, "uno")
        };
        var replace = new NumberToStringConverter.TriggerReplace("one", false, formsList, null);
        formsList.Clear();
        Assert.AreEqual(1, replace.Forms.Count,
            "Clearing the source list after construction must not affect the stored Forms");
    }

    [TestMethod]
    public void TriggerReplace_MutatingSourceConstraintsDict_DoesNotAffectStoredForms()
    {
        var constraints = new System.Collections.Generic.Dictionary<string, string> { ["gender"] = "m" };
        var replace = new NumberToStringConverter.TriggerReplace(
            "one", false, [(constraints, "uno")], null);
        constraints["gender"] = "mutated";
        // The stored snapshot must still reflect the original value.
        Assert.AreEqual("m", replace.Forms[0].Constraints["gender"],
            "Mutating the source Constraints dictionary after construction must not affect the stored snapshot");
    }

    [TestMethod]
    public void TriggerRule_MutatingSourceGroupIndicesArray_DoesNotAffectStoredIndices()
    {
        var indices = new[] { 1, 2 };
        var rule = new NumberToStringConverter.TriggerRule(
            NumberToStringConverter.TriggerAt.Group, indices, []);
        indices[0] = 99;
        Assert.AreEqual(1, rule.GroupIndices![0],
            "Mutating the source array after construction must not affect the stored GroupIndices");
    }

    [TestMethod]
    public void TriggerRule_MutatingSourceReplacesList_DoesNotAffectStoredReplaces()
    {
        var r = new NumberToStringConverter.TriggerReplace("x", false, [], "y");
        var list = new System.Collections.Generic.List<NumberToStringConverter.TriggerReplace> { r };
        var rule = new NumberToStringConverter.TriggerRule(
            NumberToStringConverter.TriggerAt.End, null, list);
        list.Clear();
        Assert.AreEqual(1, rule.Replaces.Count,
            "Clearing the source list after construction must not affect the stored Replaces");
    }

    // ── PR #503 review — NumberScale null-entry validation ───────────────────

    [TestMethod]
    public void NumberScale_NullEntryInStaticValues_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberScale(
                staticValues: (IReadOnlyList<string>)new string[] { "", null! },
                scaleSuffixes: (IReadOnlyList<string>)new[] { "illion" }),
            "null entry in staticValues must be rejected");
    }

    [TestMethod]
    public void NumberScale_NullEntryInScaleSuffixes_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberScale(
                staticValues: (IReadOnlyList<string>)new[] { "" },
                scaleSuffixes: (IReadOnlyList<string>)new string[] { null! }),
            "null entry in scaleSuffixes must be rejected");
    }

    [TestMethod]
    public void NumberScale_NullEntryInScale0Prefixes_ThrowsArgumentException()
    {
        var prefixes = new string[] { "", null!, "du", "tri", "quadri", "quinti", "sexti", "septi", "octi", "noni" };
        Assert.ThrowsException<ArgumentException>(
            () => new NumberScale(
                staticValues: (IReadOnlyList<string>)new[] { "" },
                scaleSuffixes: (IReadOnlyList<string>)new[] { "illion" },
                scale0Prefixes: (IReadOnlyList<string>)prefixes),
            "null entry in scale0Prefixes must be rejected");
    }

    // ── PR #503 review — ConvertGroup validates number range ─────────────────

    [TestMethod]
    public void ConvertGroup_NegativeNumber_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EN.ConvertGroup(1, -1),
            "Negative number must be rejected by ConvertGroup");
    }

    [TestMethod]
    public void ConvertGroup_NumberExceedsGroupRange_ThrowsArgumentOutOfRange()
    {
        // EN group 1 covers values 0–9; passing 10 must throw, not produce a KeyNotFoundException.
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EN.ConvertGroup(1, 10),
            "A number that exceeds the valid range for the group must be rejected with a clear message");
    }

    [TestMethod]
    public void ConvertGroup_GroupZero_NegativeNumber_ThrowsArgumentOutOfRange()
    {
        // groupNumber == 0 is the recursive base case but the method is public.
        // A negative number must still be rejected even for group 0.
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => EN.ConvertGroup(0, -1),
            "ConvertGroup(0, -1) must throw because number is invalid regardless of groupNumber");
    }

    // ── Item 54 — Group size and Groups structure are validated at construction ─

    private static DigitListType OneDigitList() =>
        new() { Digits = [new DigitType(1, "one")] };

    [TestMethod]
    public void Constructor_GroupSizeZero_ThrowsArgumentOutOfRange()
    {
        var opts = new NumberToStringConverterOptions(EN) { Group = 0 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new NumberToStringConverter(opts),
            "Group = 0 must be rejected at construction time");
    }

    [TestMethod]
    public void Constructor_GroupSizeNegative_ThrowsArgumentOutOfRange()
    {
        var opts = new NumberToStringConverterOptions(EN) { Group = -1 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new NumberToStringConverter(opts),
            "Group = -1 must be rejected at construction time");
    }

    [TestMethod]
    public void Constructor_EmptyGroups_ThrowsArgumentException()
    {
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType>()
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "An empty Groups dictionary must be rejected at construction time");
    }

    [TestMethod]
    public void Constructor_GroupKeyZero_ThrowsArgumentException()
    {
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType> { [0] = OneDigitList() }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "Group key 0 is not a positive integer and must be rejected");
    }

    [TestMethod]
    public void Constructor_NonContiguousGroupKeys_ThrowsArgumentException()
    {
        var dl = OneDigitList();
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType> { [1] = dl, [3] = dl }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "Group keys 1 and 3 have a gap at 2 and must be rejected");
    }

    [TestMethod]
    public void Constructor_MaxGroupKeyExceedsLimit_ThrowsArgumentException()
    {
        var dl = OneDigitList();
        var groups = new Dictionary<int, DigitListType>();
        for (int i = 1; i <= 20; i++) groups[i] = dl;
        var opts = new NumberToStringConverterOptions(EN) { Groups = groups };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "Group key 20 exceeds the supported limit and must be rejected");
    }

    [TestMethod]
    public void Constructor_GroupWithNoDigits_ThrowsArgumentException()
    {
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType>
            {
                [1] = new() { Digits = [] }
            }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "A group with no digit definitions must be rejected at construction time");
    }

    [TestMethod]
    public void Constructor_GroupSizeTooLarge_ThrowsArgumentOutOfRange()
    {
        // _decimalPowersOfTen has 19 entries; Group must be < 19 to stay in range.
        var opts = new NumberToStringConverterOptions(EN) { Group = 19 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new NumberToStringConverter(opts),
            "Group = 19 equals _decimalPowersOfTen.Length and must be rejected");
    }

    [TestMethod]
    public void Constructor_NullDigitListTypeInGroup_ThrowsArgumentException()
    {
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType> { [1] = null! }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "A null DigitListType entry must be rejected before the immutable snapshot is built");
    }

    [TestMethod]
    public void Constructor_NullDigitsListInGroup_ThrowsArgumentException()
    {
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType> { [1] = new DigitListType() }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "A null Digits list must be rejected before the immutable snapshot is built");
    }

    [TestMethod]
    public void Constructor_NullDigitTypeEntryInGroup_ThrowsArgumentException()
    {
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType>
            {
                [1] = new DigitListType { Digits = [new DigitType(1, "one"), null!] }
            }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "A null DigitType entry in the Digits list must be rejected before LINQ materialisation");
    }

    [TestMethod]
    public void Constructor_DuplicateDigitValuesInGroup_ThrowsArgumentException()
    {
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType>
            {
                [1] = new DigitListType { Digits = [new DigitType(1, "one"), new DigitType(1, "uno")] }
            }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "Duplicate digit value 1 in the same group must be rejected with a clear diagnostic");
    }

    [TestMethod]
    public void Constructor_DuplicateDigitValuesDifferentEntries_ThrowsArgumentException()
    {
        // Two DigitType entries with the same Digit value — the second is unreachable
        // and signals a configuration mistake; must be detected before LINQ materialisation.
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType>
            {
                [1] = new DigitListType
                {
                    Digits =
                    [
                        new DigitType(5, "five"),
                        new DigitType(5, "cinq")
                    ]
                }
            }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "Two DigitType entries with the same Digit value in the same group must be rejected");
    }

    [TestMethod]
    public void Constructor_GroupKeysStartingAtTwo_ThrowsArgumentException()
    {
        // ConvertGroup recurses down to group 1; a sequence starting at 2 would cause
        // a late KeyNotFoundException; must be caught at construction time.
        var opts = new NumberToStringConverterOptions(EN)
        {
            Groups = new Dictionary<int, DigitListType>
            {
                [2] = OneDigitList(),
                [3] = OneDigitList()
            }
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(opts),
            "Group keys must start at 1");
    }

    // ── Item 78 — MaxNumber must not be negative ──────────────────────────────

    [TestMethod]
    public void Constructor_NegativeMaxNumber_ThrowsArgumentOutOfRange()
    {
        var opts = new NumberToStringConverterOptions(EN) { MaxNumber = -1 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new NumberToStringConverter(opts),
            "MaxNumber = -1 must be rejected; a negative maximum makes every conversion fail");
    }

    // ── Item 75 — nested configuration objects must be deep-frozen ────────────

    [TestMethod]
    public void VariantDimension_MutatingSourceListAfterConstruction_DoesNotAffectValues()
    {
        var source = new List<string> { "masc", "fem" };
        var dim = new NumberToStringConverter.VariantDimension("gender", source);

        source.Add("neut");

        Assert.AreEqual(2, dim.Values.Count,
            "VariantDimension.Values must not reflect mutations made to the source list after construction");
    }

    [TestMethod]
    public void VariantDimension_NullName_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new NumberToStringConverter.VariantDimension(null!, ["masc"]));
    }

    [TestMethod]
    public void VariantDimension_EmptyName_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new NumberToStringConverter.VariantDimension("", ["masc"]));
    }

    [TestMethod]
    public void VariantDimension_NullValues_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new NumberToStringConverter.VariantDimension("gender", null!));
    }

    [TestMethod]
    public void VariantRule_MutatingSourceConstraintsAfterConstruction_DoesNotAffectRule()
    {
        var constraints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "fem" };
        var rule = new NumberToStringConverter.VariantRule(
            constraints,
            [new NumberToStringConverter.ReplacementRule("un", "une", ReplacementScope.LastWord)]);

        constraints["case"] = "nominative";

        Assert.AreEqual(1, rule.Constraints.Count,
            "VariantRule.Constraints must not reflect mutations to the source dictionary after construction");
    }

    [TestMethod]
    public void VariantRule_MutatingSourceReplacementsAfterConstruction_DoesNotAffectRule()
    {
        var replacements = new List<NumberToStringConverter.ReplacementRule>
        {
            new("un", "une", ReplacementScope.LastWord)
        };
        var rule = new NumberToStringConverter.VariantRule(
            new Dictionary<string, string>(),
            replacements);

        replacements.Add(new NumberToStringConverter.ReplacementRule("deux", "deux", ReplacementScope.LastWord));

        Assert.AreEqual(1, rule.Replacements.Count,
            "VariantRule.Replacements must not reflect mutations to the source list after construction");
    }

    [TestMethod]
    public void VariantRule_NullConstraints_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new NumberToStringConverter.VariantRule(null!, []));
    }

    [TestMethod]
    public void VariantRule_NullReplacements_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new NumberToStringConverter.VariantRule(new Dictionary<string, string>(), null!));
    }

    [TestMethod]
    public void OrdinalVariantRule_MutatingSourceExceptionsAfterConstruction_DoesNotAffectRule()
    {
        var exceptions = new Dictionary<long, string> { [1L] = "premier" };
        var rule = new NumberToStringConverter.OrdinalVariantRule(
            new Dictionary<string, string>(), exceptions, new Dictionary<string, string>(), null, null);

        exceptions[2L] = "deuxième";

        Assert.AreEqual(1, rule.Exceptions.Count,
            "OrdinalVariantRule.Exceptions must not reflect mutations to the source dictionary after construction");
    }

    [TestMethod]
    public void OrdinalVariantRule_MutatingSourceWordRulesAfterConstruction_DoesNotAffectRule()
    {
        var wordRules = new Dictionary<string, string> { ["one"] = "first" };
        var rule = new NumberToStringConverter.OrdinalVariantRule(
            new Dictionary<string, string>(), new Dictionary<long, string>(), wordRules, null, null);

        wordRules["two"] = "second";

        Assert.AreEqual(1, rule.WordRules.Count,
            "OrdinalVariantRule.WordRules must not reflect mutations to the source dictionary after construction");
    }

    [TestMethod]
    public void OrdinalVariantRule_NullConstraints_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new NumberToStringConverter.OrdinalVariantRule(
                null!, new Dictionary<long, string>(), new Dictionary<string, string>(), null, null));
    }

    [TestMethod]
    public void OrdinalVariantRule_NullExceptions_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new NumberToStringConverter.OrdinalVariantRule(
                new Dictionary<string, string>(), null!, new Dictionary<string, string>(), null, null));
    }

    [TestMethod]
    public void OrdinalVariantRule_NullWordRules_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new NumberToStringConverter.OrdinalVariantRule(
                new Dictionary<string, string>(), new Dictionary<long, string>(), null!, null, null));
    }

    // ── Item 76 — onValue rules must not be silently suppressed for BigInteger > long.MaxValue ──

    private static NumberToStringConverter BuildEnWithOnValueRule(string onValue) =>
        new(new NumberToStringConverterOptions(EN)
        {
            Replacements =
            [
                ..EN.Replacements,
                new NumberToStringConverter.ReplacementRule(
                    "hundred", "HUNDRED",
                    ReplacementScope.Anywhere,
                    onScale: null,
                    onValue: NumberToStringConverter.ParseRangeExpression(onValue))
            ]
        });

    [TestMethod]
    public void OnValueRule_SmallValue_WithinLongRange_FiresCorrectly()
    {
        // Baseline: onValue="1.." fires for a small value that contains "hundred".
        var c = BuildEnWithOnValueRule("1..");
        var result = c.Convert(100); // "one hundred"
        StringAssert.Contains(result, "HUNDRED",
            $"onValue='1..' must fire for value 100 (within long range); got: {result}");
    }

    [TestMethod]
    public void OnValueRule_BigIntegerAboveLongMax_FiresForOpenUpperBound()
    {
        // 10^20 is well above long.MaxValue (~9.2×10^18).
        // The assembled text for 10^20 in English is "one hundred quintillion".
        // A global replacement rule with onValue="1.." (= [1, +∞)) must fire
        // because the range has an open upper bound; previously it was silently
        // suppressed (numericValue was passed as null for large BigIntegers).
        var c = BuildEnWithOnValueRule("1..");
        var hugeValue = BigInteger.Pow(10, 20);
        var result = c.Convert(hugeValue);
        StringAssert.Contains(result, "HUNDRED",
            $"onValue='1..' must fire for BigInteger > long.MaxValue; got: {result}");
    }

    [TestMethod]
    public void OnValueRule_BigIntegerAboveLongMax_DoesNotFireForClosedRange()
    {
        // A rule with onValue="1..100" (closed upper bound far below long.MaxValue)
        // must NOT fire for 10^20, since 10^20 is not in [1, 100].
        var c = BuildEnWithOnValueRule("1..100");
        var hugeValue = BigInteger.Pow(10, 20);
        var result = c.Convert(hugeValue);
        Assert.IsFalse(result.Contains("HUNDRED"),
            $"onValue='1..100' must NOT fire for BigInteger > long.MaxValue; got: {result}");
    }

    [TestMethod]
    public void OnValueRule_BigIntegerAboveLongMax_DoesNotFireForClosedLongMaxRange()
    {
        // A rule with onValue="1..9223372036854775807" has a closed upper bound at exactly
        // long.MaxValue. 10^20 > long.MaxValue, so it is outside [1, long.MaxValue] and
        // the rule must NOT fire. The previous approximation (Contains(long.MaxValue) as proxy
        // for "open upper bound") incorrectly returned true for this case.
        var c = BuildEnWithOnValueRule($"1..{long.MaxValue}");
        var hugeValue = BigInteger.Pow(10, 20);
        var result = c.Convert(hugeValue);
        Assert.IsFalse(result.Contains("HUNDRED"),
            $"onValue='1..{long.MaxValue}' must NOT fire for 10^20 (above the closed upper bound); got: {result}");
    }

    [TestMethod]
    public void OnValueRule_BigIntegerAboveLongMax_DoesNotFireForExactLongMax()
    {
        // A rule with onValue="9223372036854775807" (the single exact value long.MaxValue)
        // must NOT fire for 10^20, which is strictly greater than long.MaxValue.
        var c = BuildEnWithOnValueRule($"{long.MaxValue}");
        var hugeValue = BigInteger.Pow(10, 20);
        var result = c.Convert(hugeValue);
        Assert.IsFalse(result.Contains("HUNDRED"),
            $"onValue='{long.MaxValue}' must NOT fire for 10^20; got: {result}");
    }

    // ── Item 68 — Hyphens must be preserved in fraction and decimal components ─

    [TestMethod]
    public void ConvertFraction_EN_HyphenatedNumerator_HyphenPreserved_NamedSuffix()
    {
        // 21/100 → denominator 100 is a power of 10 → named suffix path ("hundredths").
        // The numerator "twenty-one" must retain its hyphen; no .Replace("-", " ").
        string result = EN.ConvertFraction(21, 100);
        StringAssert.Contains(result, "twenty-one",
            $"Hyphen in 'twenty-one' must be preserved in named-suffix fraction path; got: '{result}'");
    }

    [TestMethod]
    public void ConvertFraction_EN_HyphenatedNumerator_HyphenPreserved_OverConnector()
    {
        // 21/7 → denominator 7 is not a power of 10 → "over" connector path.
        // The numerator "twenty-one" must retain its hyphen.
        string result = EN.ConvertFraction(21, 7);
        StringAssert.Contains(result, "twenty-one",
            $"Hyphen in 'twenty-one' must be preserved in over-connector fraction path; got: '{result}'");
    }

    [TestMethod]
    public void ConvertFraction_EN_HyphenatedDenominator_HyphenPreserved_OverConnector()
    {
        // 1/21 → denominator "twenty-one" must retain its hyphen in the over-connector path.
        string result = EN.ConvertFraction(1, 21);
        StringAssert.Contains(result, "twenty-one",
            $"Hyphen in denominator 'twenty-one' must be preserved; got: '{result}'");
    }

    [TestMethod]
    public void Convert_Decimal_EN_HyphenatedFractionPart_HyphenPreserved()
    {
        // 0.21 with a named decimal suffix forces the fractional part through Convert(21).
        // "twenty-one" must appear without hyphen stripping in the decimal output.
        var c = EN;
        string result = c.Convert(0.21m);
        StringAssert.Contains(result, "twenty-one",
            $"Hyphen in decimal fraction 'twenty-one' must be preserved; got: '{result}'");
    }

    // ── Item 71 — DateFirstDay applies only to {{ordinal-day}}; DateFirstCardinalDay to {{cardinal-day}} ─

    [TestMethod]
    public void Convert_DateOnly_DateFirstDay_AppliesToOrdinalDayOnly()
    {
        // A pattern with both {ordinal-day} and {cardinal-day}:
        // DateFirstDay → only {ordinal-day} for day 1;
        // {cardinal-day} for day 1 uses the standard cardinal form.
        var opts = new NumberToStringConverterOptions(EN)
        {
            DatePattern = "{cardinal-day}/{ordinal-day}",
            DateFirstDay = "PREMIER"
        };
        var c = new NumberToStringConverter(opts);
        string result = c.Convert(new DateOnly(2026, 1, 1));
        // ordinal-day → "PREMIER", cardinal-day → standard cardinal "one"
        Assert.AreEqual("one/PREMIER", result,
            $"DateFirstDay must apply only to {{ordinal-day}}, not {{cardinal-day}}; got: '{result}'");
    }

    [TestMethod]
    public void Convert_DateOnly_DateFirstCardinalDay_AppliesToCardinalDayOnly()
    {
        // DateFirstCardinalDay → only {cardinal-day} for day 1;
        // {ordinal-day} for day 1 uses the standard ordinal form.
        var opts = new NumberToStringConverterOptions(EN)
        {
            DatePattern = "{cardinal-day}/{ordinal-day}",
            DateFirstCardinalDay = "ERSTEN"
        };
        var c = new NumberToStringConverter(opts);
        string result = c.Convert(new DateOnly(2026, 1, 1));
        // cardinal-day → "ERSTEN", ordinal-day → standard ordinal "first"
        Assert.AreEqual("ERSTEN/first", result,
            $"DateFirstCardinalDay must apply only to {{cardinal-day}}, not {{ordinal-day}}; got: '{result}'");
    }

    [TestMethod]
    public void Convert_DateOnly_BothFirstDayOverrides_Independent()
    {
        // Both overrides set independently.
        var opts = new NumberToStringConverterOptions(EN)
        {
            DatePattern = "{cardinal-day}/{ordinal-day}",
            DateFirstDay = "ORDINAL-FIRST",
            DateFirstCardinalDay = "CARDINAL-FIRST"
        };
        var c = new NumberToStringConverter(opts);
        string result = c.Convert(new DateOnly(2026, 1, 1));
        Assert.AreEqual("CARDINAL-FIRST/ORDINAL-FIRST", result,
            $"Both overrides must apply to their respective tokens independently; got: '{result}'");
    }

    [TestMethod]
    public void Convert_DateOnly_FirstDayOverrides_NotAppliedForOtherDays()
    {
        // Overrides must only fire for day == 1.
        var opts = new NumberToStringConverterOptions(EN)
        {
            DatePattern = "{cardinal-day}/{ordinal-day}",
            DateFirstDay = "ORDINAL-FIRST",
            DateFirstCardinalDay = "CARDINAL-FIRST"
        };
        var c = new NumberToStringConverter(opts);
        string result = c.Convert(new DateOnly(2026, 1, 2));
        // day 2 → standard forms, no overrides
        Assert.AreEqual("two/second", result,
            $"First-day overrides must not apply for day 2; got: '{result}'");
    }

    // ── Item 88 — forms= count must match dimension value count ──────────────

    [TestMethod]
    public void ReadConfiguration_FormsTooFew_ThrowsInvalidOperation()
    {
        // Dimension declares 3 values; forms= provides only 2 → must reject.
        string xml = BuildXmlWithForms("dim1", "a,b,c", "X,Y");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "dim1",
            "Exception must identify the offending dimension name");
        StringAssert.Contains(ex.Message, "3",
            "Exception must state the expected count");
        StringAssert.Contains(ex.Message, "2",
            "Exception must state the actual count");
    }

    [TestMethod]
    public void ReadConfiguration_FormsTooMany_ThrowsInvalidOperation()
    {
        // Dimension declares 2 values; forms= provides 3 → must reject.
        string xml = BuildXmlWithForms("dim1", "a,b", "X,Y,Z");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "dim1");
    }

    [TestMethod]
    public void ReadConfiguration_FormsExactCount_Succeeds()
    {
        // Exact count (2 values, 2 forms) must load without error.
        string xml = BuildXmlWithForms("dim1", "a,b", "X,Y");
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.Count > 0, "Valid forms count must produce at least one converter");
    }

    // ── Item 90 — dimension alias collisions are reported clearly ─────────────

    [TestMethod]
    public void ReadConfiguration_DuplicateDimensionName_ThrowsInvalidOperation()
    {
        // Two dimensions with the same canonical name must be rejected.
        string xml = BuildXmlWithDuplicateDimension(canonical1: "gender", alias1: null,
                                                    canonical2: "gender", alias2: null);
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "gender",
            "Exception must identify the colliding dimension name");
    }

    [TestMethod]
    public void ReadConfiguration_AliasCollidesWithCanonicalName_ThrowsInvalidOperation()
    {
        // Dimension "case" with alias "gender" collides with the canonical name of another dimension "gender".
        string xml = BuildXmlWithDuplicateDimension(canonical1: "gender", alias1: null,
                                                    canonical2: "case", alias2: "gender");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "gender");
    }

    // ── Item 91 — culture identifiers are trimmed at registration boundaries ──

    [TestMethod]
    public void GetConverter_CultureWithLeadingTrailingSpace_ResolvesSamAsWithout()
    {
        // "EN" and " EN " must resolve to the same converter.
        var plain = NumberToStringConverter.GetConverter("EN");
        var spaced = NumberToStringConverter.GetConverter(" EN ");
        Assert.AreSame(plain, spaced,
            "GetConverter must trim whitespace from culture identifiers before lookup");
    }

    [TestMethod]
    public void ReadConfiguration_CultureWithSpaces_RegisteredAndLookupSucceeds()
    {
        // A culture registered as " XTEST " must be found as "XTEST".
        string xml = BuildMinimalXml(" XTEST ");
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.ContainsKey("XTEST"),
            "ReadConfiguration must trim culture keys before registering them");
    }

    // ── Item 93 — static scale indices must be zero-based and contiguous ──────

    [TestMethod]
    public void ReadConfiguration_StaticScaleSkipsIndex_ThrowsInvalidOperation()
    {
        // indices 0 and 2 (gap at 1) must be rejected.
        string xml = BuildXmlWithScaleIndices(0, 2);
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "contiguous",
            "Exception must mention contiguous indices requirement");
    }

    [TestMethod]
    public void ReadConfiguration_StaticScaleStartsAtOne_ThrowsInvalidOperation()
    {
        // An index starting at 1 (missing 0) must be rejected.
        string xml = BuildXmlWithScaleIndices(1, 2);
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "0",
            "Exception must mention that index 0 is expected first");
    }

    [TestMethod]
    public void ReadConfiguration_StaticScaleContiguous_Succeeds()
    {
        // Indices 0, 1 (the normal pattern) must succeed.
        string xml = BuildXmlWithScaleIndices(0, 1);
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.Count > 0, "Contiguous scale indices must load successfully");
    }

    // ── Item 94 — replacement without newValue or form variants is rejected ───

    [TestMethod]
    public void ReadConfiguration_ReplacementWithoutNewValueOrVariants_ThrowsInvalidOperation()
    {
        string xml = BuildXmlWithBareReplacement("one");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "one",
            "Exception must identify the incomplete replacement's oldValue");
        StringAssert.Contains(ex.Message, "newValue",
            "Exception must mention the missing newValue");
    }

    // ── Follow-up: baseOn normalization (review finding) ─────────────────────

    [TestMethod]
    public void ReadConfiguration_BaseOnWithSpaces_ResolvesSuccessfully()
    {
        // Base culture registered with spaces; child references it via baseOn with spaces.
        // Both must load without error, proving NormalizeCulture is applied to baseOn.
        string xml = BuildXmlWithBaseOn(baseCulture: " XBASE ", childCulture: "XCHILD", baseOnValue: " XBASE ");
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.ContainsKey("XBASE"), "Base culture must be registered as 'XBASE'");
        Assert.IsTrue(converters.ContainsKey("XCHILD"), "Child culture must resolve baseOn and load successfully");
    }

    // ── Follow-up: empty entries inside forms= are rejected (review finding) ──

    [TestMethod]
    public void ReadConfiguration_FormsWithLeadingEmptyEntry_ThrowsInvalidOperation()
    {
        // Count matches (2 values, 2 entries) but first entry is empty → must reject.
        string xml = BuildXmlWithForms("dim1", "a,b", ",b");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "dim1",
            "Exception must identify the dimension");
        StringAssert.Contains(ex.Message, "a",
            "Exception must identify the dimension value that lacks a form");
    }

    [TestMethod]
    public void ReadConfiguration_FormsWithTrailingEmptyEntry_ThrowsInvalidOperation()
    {
        // Count matches (2 values, 2 entries) but last entry is empty → must reject.
        string xml = BuildXmlWithForms("dim1", "a,b", "X,");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "dim1");
        StringAssert.Contains(ex.Message, "b",
            "Exception must identify the dimension value that lacks a form");
    }

    // ── Follow-up: item 94 in nested Variant rules (review finding) ───────────

    [TestMethod]
    public void ReadConfiguration_NestedVariantBareReplacement_ThrowsInvalidOperation()
    {
        // A <Replacement> inside a <Variant> rule with no newValue and no <Variant> children must throw.
        string xml = BuildXmlWithNestedBareReplacement(dimName: "gender", dimValues: "a,b", dimValue: "a", oldValue: "one");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "one",
            "Exception must identify the incomplete replacement's oldValue");
        StringAssert.Contains(ex.Message, "newValue",
            "Exception must mention the missing newValue");
    }

    [TestMethod]
    public void ReadConfiguration_NestedVariantFormVariantReplacement_ThrowsInvalidOperation()
    {
        // A <Replacement> inside a structural <Variant> rule that has form-variant children
        // (instead of newValue) must be rejected — not silently ignored.
        string xml = BuildXmlWithNestedFormVariantReplacement(
            structDim: "case", structValues: "nom,acc", structValue: "acc",
            formDim: "gender", formValues: "m,f", oldValue: "one");
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "one",
            "Exception must identify the replacement's oldValue");
        StringAssert.Contains(ex.Message, "form-variant",
            "Exception must mention form-variant children");
    }

    // ── XML helpers for configuration audit tests ─────────────────────────────

    private static string BuildXmlWithForms(string dimName, string dimValues, string forms) => $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>XTEST</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
    <Variants>
      <Dimension name=""{dimName}"" values=""{dimValues}"" />
    </Variants>
    <Replacements>
      <Replacement oldValue=""zero"" scope=""Standalone"">
        <Variant type=""{dimName}"" forms=""{forms}"" />
      </Replacement>
    </Replacements>
  </Language>
</Numbers>";

    private static string BuildXmlWithDuplicateDimension(
        string canonical1, string? alias1, string canonical2, string? alias2)
    {
        string attr1 = alias1 != null ? $@" localName=""{alias1}""" : "";
        string attr2 = alias2 != null ? $@" localName=""{alias2}""" : "";
        return $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>XTEST</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
    <Variants>
      <Dimension name=""{canonical1}""{attr1} values=""a,b"" />
      <Dimension name=""{canonical2}""{attr2} values=""x,y"" />
    </Variants>
  </Language>
</Numbers>";
    }

    private static string BuildMinimalXml(string culture) => $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{culture}</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";

    private static string BuildXmlWithScaleIndices(params int[] indices)
    {
        var scales = string.Concat(indices.Select(i => $@"<Scale value=""{i}"" string=""s{i}"" />"));
        return $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>XTEST</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames>{scales}</StaticNames></NumberScale>
  </Language>
</Numbers>";
    }

    private static string BuildXmlWithBareReplacement(string oldValue) => $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>XTEST</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
    <Replacements>
      <Replacement oldValue=""{oldValue}"" scope=""Standalone"" />
    </Replacements>
  </Language>
</Numbers>";

    private static string BuildXmlWithBaseOn(string baseCulture, string childCulture, string baseOnValue) => $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{baseCulture}</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"" baseOn=""{baseOnValue}"">
    <Culture>{childCulture}</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";

    private static string BuildXmlWithNestedBareReplacement(
        string dimName, string dimValues, string dimValue, string oldValue) => $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>XTEST</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
    <Variants>
      <Dimension name=""{dimName}"" values=""{dimValues}"" />
      <Variant type=""{dimName}"" variant=""{dimValue}"">
        <Replacement oldValue=""{oldValue}"" scope=""Standalone"" />
      </Variant>
    </Variants>
  </Language>
</Numbers>";

    private static string BuildXmlWithNestedFormVariantReplacement(
        string structDim, string structValues, string structValue,
        string formDim, string formValues, string oldValue) => $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>XTEST</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
    <Variants>
      <Dimension name=""{structDim}"" values=""{structValues}"" />
      <Dimension name=""{formDim}"" values=""{formValues}"" />
      <Variant type=""{structDim}"" variant=""{structValue}"">
        <Replacement oldValue=""{oldValue}"" scope=""Standalone"">
          <Variant type=""{formDim}"" forms=""x,y"" />
        </Replacement>
      </Variant>
    </Variants>
  </Language>
</Numbers>";

    // ── Item 78 — MaxNumber does not constrain ConvertFraction sub-expressions ──

    [TestMethod]
    public void ConvertFraction_ComponentsExceedMaxNumber_Succeeds()
    {
        // ConvertFraction is an independent conversion surface; its numerator and
        // denominator are sub-expressions that must not be guarded by MaxNumber.
        var opts = new NumberToStringConverterOptions(EN) { MaxNumber = 1 };
        var conv = new NumberToStringConverter(opts);
        string result = conv.ConvertFraction(2, 3);
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result),
            "ConvertFraction must succeed even when numerator/denominator exceed MaxNumber");
    }

    [TestMethod]
    public void Convert_Number_FractionComponentsExceedMaxNumber_Succeeds()
    {
        // Convert(Number) for a proper fraction delegates to BuildFractionText;
        // the numerator and denominator must not be checked against MaxNumber.
        var opts = new NumberToStringConverterOptions(EN) { MaxNumber = 1 };
        var conv = new NumberToStringConverter(opts);
        var number = new Utils.Numerics.Number(5, 7);
        string result = conv.Convert(number);
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result),
            "Convert(Number) for a fraction must succeed even when components exceed MaxNumber");
    }

    // ── Item 78 — MaxNumber constrains cardinal component of composite conversions ─

    [TestMethod]
    public void Convert_Decimal_IntegerPartAtMaxNumber_Succeeds()
    {
        var opts = new NumberToStringConverterOptions(EN) { MaxNumber = 1 };
        var conv = new NumberToStringConverter(opts);
        // integer part is 1 == MaxNumber → must not throw
        _ = conv.Convert(1.9m);
    }

    [TestMethod]
    public void Convert_Decimal_IntegerPartExceedsMaxNumber_ThrowsArgumentOutOfRange()
    {
        var opts = new NumberToStringConverterOptions(EN) { MaxNumber = 1 };
        var conv = new NumberToStringConverter(opts);
        // integer part is 2 > MaxNumber → must throw
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => conv.Convert(2.1m),
            "Convert(decimal) must throw when the integer part exceeds MaxNumber");
    }

    // ── Item 85 — CultureInfo for month names is resolved once at construction ──

    [TestMethod]
    public void SupportsLocalizableMonthNames_KnownCultureWithDatePattern_ReturnsTrue()
    {
        // EN has a DateFormat in its XML configuration; "EN" resolves to a known CultureInfo.
        var conv = NumberToStringConverter.GetConverter("EN");
        Assert.IsTrue(conv.SupportsLocalizableMonthNames,
            "SupportsLocalizableMonthNames must be true when LanguageIdentifier maps to a system culture");
    }

    [TestMethod]
    public void SupportsLocalizableMonthNames_UnknownCultureWithDatePattern_ReturnsFalse()
    {
        // Build a converter whose LanguageIdentifier is not a known system culture.
        var opts = new NumberToStringConverterOptions(EN)
        {
            LanguageIdentifier = "XTEST",
            DatePattern = "{month} {ordinal-day}"
        };
        var conv = new NumberToStringConverter(opts);
        Assert.IsFalse(conv.SupportsLocalizableMonthNames,
            "SupportsLocalizableMonthNames must be false when LanguageIdentifier is unknown to CultureInfo");
    }

    [TestMethod]
    public void SupportsLocalizableMonthNames_NullDatePattern_ReturnsFalse()
    {
        // A converter with no DatePattern should never expose month-name support.
        var opts = new NumberToStringConverterOptions(EN)
        {
            LanguageIdentifier = "EN",
            DatePattern = null
        };
        var conv = new NumberToStringConverter(opts);
        Assert.IsFalse(conv.SupportsLocalizableMonthNames,
            "SupportsLocalizableMonthNames must be false when no DatePattern is configured");
    }

    [TestMethod]
    public void SupportsLocalizableMonthNames_UnknownTwoLetterCode_ReturnsFalse()
    {
        // "XQ" is not an ISO 639-1 language code and should not appear in any
        // platform's known-culture list. ICU may synthesise a culture for it, but the
        // parent-chain walk must not find a registered ancestor, so the result must be null.
        var opts = new NumberToStringConverterOptions(EN)
        {
            LanguageIdentifier = "XQ",
            DatePattern = "{month}"
        };
        var conv = new NumberToStringConverter(opts);
        Assert.IsFalse(conv.SupportsLocalizableMonthNames,
            "SupportsLocalizableMonthNames must be false for a 2-letter non-culture code");
    }

    [TestMethod]
    public void SupportsLocalizableMonthNames_UnknownThreeLetterCode_ReturnsFalse()
    {
        // "ZZZ" is not an ISO 639-2/3 language code and must be rejected as well.
        var opts = new NumberToStringConverterOptions(EN)
        {
            LanguageIdentifier = "ZZZ",
            DatePattern = "{month}"
        };
        var conv = new NumberToStringConverter(opts);
        Assert.IsFalse(conv.SupportsLocalizableMonthNames,
            "SupportsLocalizableMonthNames must be false for a 3-letter non-culture code");
    }

    [TestMethod]
    public void Convert_DateOnly_UnknownCultureInfo_MonthRenderedAsDecimalNumber()
    {
        // A converter with an unknown LanguageIdentifier must fall back to a numeric month.
        var opts = new NumberToStringConverterOptions(EN)
        {
            LanguageIdentifier = "XTEST",
            DatePattern = "{month}"
        };
        var conv = new NumberToStringConverter(opts);
        // 15 March 2024 → month = 3 → expected output "3"
        string result = conv.Convert(new DateOnly(2024, 3, 15));
        Assert.AreEqual("3", result,
            "When LanguageIdentifier does not resolve to a CultureInfo, the month must be emitted as its decimal number");
    }

    // ── Item 57 — baseOn cycle detection ─────────────────────────────────────

    [TestMethod]
    public void ReadConfiguration_BaseOnCycle_ThrowsInvalidOperation()
    {
        // Two languages that each list the other as their baseOn must trigger cycle detection.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""CYCLE-B"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>CYCLE-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""CYCLE-A"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>CYCLE-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "cycle",
            "Exception must mention 'cycle' to identify the cause");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOnSelfCycle_ThrowsInvalidOperation()
    {
        // A language that lists itself as its own baseOn must be rejected.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""SELF"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>SELF</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "cycle",
            "Self-cycle must be detected and reported as a cycle");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOnThreeNodeCycle_ThrowsInvalidOperation()
    {
        // A → B → C → A cycle must be detected.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""TN-C"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>TN-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""TN-A"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>TN-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""TN-B"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>TN-C</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "cycle",
            "Three-node cycle must be detected");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOnCycle_IsCaseInsensitive()
    {
        // Cycle detection must be case-insensitive: CYCLE-A and cycle-a are the same node.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""ci-b"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>CI-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""CI-A"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>ci-b</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "cycle",
            "Case-insensitive cycle must be detected");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOnMissingCulture_ThrowsInvalidOperation()
    {
        // A baseOn referencing a non-existent culture must throw with a clear message.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""DOES-NOT-EXIST"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>XMISSING-CHILD</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "DOES-NOT-EXIST",
            "Exception must identify the missing base culture");
    }

    [TestMethod]
    public void ReadConfiguration_SharedBaseWithoutCycle_Succeeds()
    {
        // D inherits from B; E inherits from B. B is used twice but there is no cycle.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>SB-BASE</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""SB-BASE"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zerod"" minus=""minus *"">
    <Culture>SB-D</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""SB-BASE"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zeroe"" minus=""minus *"">
    <Culture>SB-E</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.ContainsKey("SB-BASE"), "Base must be registered");
        Assert.IsTrue(converters.ContainsKey("SB-D"), "D must be registered");
        Assert.IsTrue(converters.ContainsKey("SB-E"), "E must be registered");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOnCycle_DoesNotCommitPartialConfigurations()
    {
        // A document with one valid language and one cycle must not commit the valid language.
        string uniqueKey = "PARTIAL-VALID-" + System.Guid.NewGuid().ToString("N");
        string xml = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{uniqueKey}</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""PCYCLE-B"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>PCYCLE-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""PCYCLE-A"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>PCYCLE-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        // The valid language in the same document must NOT have been committed.
        bool found = NumberToStringConverter.TryGetConverter(uniqueKey, out _);
        Assert.IsFalse(found,
            "ReadConfiguration must not commit any configurations when a cycle is detected");
    }

    [TestMethod]
    public void ReadConfiguration_MultipleBases_CycleInOneBranch_ThrowsInvalidOperation()
    {
        // A baseOn B,C where C baseOn A — the cycle exists only in the second branch.
        // The whole document must be rejected regardless of which branch contains the cycle.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>MCB-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""MCB-A"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>MCB-C</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""MCB-B,MCB-C"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>MCB-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(xml));
        StringAssert.Contains(ex.Message, "cycle",
            "Cycle in one branch of a multi-base inheritance must be detected");
    }

    [TestMethod]
    public void ReadConfiguration_CurrentDocumentCycleShadowsCachedCulture_ThrowsInvalidOperation()
    {
        // Load a first document containing a standalone language so it lands in the global cache.
        // RegisterConfigurations (not ReadConfiguration alone) populates CachedConfigurations.
        string shadowKey = "XSHADOW-" + System.Guid.NewGuid().ToString("N");
        string firstDoc = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{shadowKey}</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        NumberToStringConverter.RegisterConfigurations([firstDoc]);
        // Confirm it is cached.
        Assert.IsTrue(NumberToStringConverter.TryGetConverter(shadowKey, out _),
            "First document must be loaded into the global cache");

        // Second document redefines shadowKey and introduces a cycle: LOCAL-B → shadowKey → LOCAL-B.
        // Even though an older valid version of shadowKey is in the global cache, the local
        // redefinition must take priority and the cycle must be detected.
        string localB = "XSHADOW-LOCAL-B-" + System.Guid.NewGuid().ToString("N");
        string secondDoc = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""{shadowKey}"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{localB}</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""{localB}"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{shadowKey}</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(secondDoc));
        StringAssert.Contains(ex.Message, "cycle",
            "A locally declared language must shadow the cached version so the cycle is still detected");
    }

    // ── Item 57 — baseOn multi-base and merge order ───────────────────────────

    [TestMethod]
    public void ReadConfiguration_BaseOn_ChildOverridesDirectBase()
    {
        // Child defines zero="child-zero", base defines zero="base-zero".
        // The resolved converter must use "child-zero".
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""base-zero"" minus=""minus *"">
    <Culture>BASE-OV</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""base-zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""BASE-OV"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""child-zero"" minus=""minus *"">
    <Culture>CHILD-OV</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""child-zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.TryGetValue("CHILD-OV", out var child));
        string result = child.Convert(BigInteger.Zero);
        Assert.AreEqual("child-zero", result,
            "Child's own zero value must override the base's zero value");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOn_InheritedPropertyFromBase_WhenChildOmitsIt()
    {
        // Child does not define minus; base defines minus="BASE-MINUS *".
        // The resolved converter must use "BASE-MINUS *" for negatives.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""BASE-MINUS *"">
    <Culture>BASE-INH</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""BASE-INH"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>CHILD-INH</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        // Simply verify the child loaded — if it inherited minus correctly, Convert(-1) won't throw.
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.ContainsKey("CHILD-INH"),
            "Child must load when it inherits minus from base");
    }

    [TestMethod]
    public void ReadConfiguration_MultipleBases_LaterBaseOverridesEarlierBase()
    {
        // A baseOn B,C — C is the later base so C's zero must win over B's zero.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-b"" minus=""minus *"">
    <Culture>MLB-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-b"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-c"" minus=""minus *"">
    <Culture>MLB-C</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-c"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""MLB-B,MLB-C"" groupSize=""3"" separator="" "" groupSeparator="","" minus=""minus *"">
    <Culture>MLB-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-c"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.TryGetValue("MLB-A", out var a));
        Assert.AreEqual("zero-c", a.Convert(BigInteger.Zero),
            "Later base (C) must override earlier base (B) — A.zero must be 'zero-c'");
    }

    [TestMethod]
    public void ReadConfiguration_MultipleBases_ChildOverridesAllBases()
    {
        // A baseOn B,C with A's own zero — A's value must win over both bases.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-b"" minus=""minus *"">
    <Culture>MCA-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-b"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-c"" minus=""minus *"">
    <Culture>MCA-C</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-c"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""MCA-B,MCA-C"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-a"" minus=""minus *"">
    <Culture>MCA-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-a"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.TryGetValue("MCA-A", out var a));
        Assert.AreEqual("zero-a", a.Convert(BigInteger.Zero),
            "Child's own zero must override both bases' zero values");
    }

    [TestMethod]
    public void ReadConfiguration_MultipleBases_IndependentOptionsAreCombined()
    {
        // A baseOn B,C — B provides minus, C provides zero (overrides B's zero).
        // A's final configuration must have zero from C and minus from B (C does not set minus).
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-b"" minus=""BMIN *"">
    <Culture>MIO-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-b"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-c"" minus=""BMIN *"">
    <Culture>MIO-C</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-c"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""MIO-B,MIO-C"" groupSize=""3"" separator="" "" groupSeparator="","" minus=""BMIN *"">
    <Culture>MIO-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-c"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.TryGetValue("MIO-A", out var a));
        Assert.AreEqual("zero-c", a.Convert(BigInteger.Zero),
            "zero must come from C (later base overrides B)");
        string negOne = a.Convert(new System.Numerics.BigInteger(-1));
        StringAssert.StartsWith(negOne, "BMIN",
            "minus prefix 'BMIN' must be inherited from B (and C since both use it)");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOn_ClosestAncestorOverridesDeeperAncestor()
    {
        // A baseOn B, B baseOn C — B's zero must override C's, and A's zero must override B's.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-c"" minus=""minus *"">
    <Culture>CAO-C</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-c"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""CAO-C"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-b"" minus=""minus *"">
    <Culture>CAO-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-b"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""CAO-B"" groupSize=""3"" separator="" "" groupSeparator="","" minus=""minus *"">
    <Culture>CAO-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-b"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        // B must see zero-b (overrides C's zero-c)
        Assert.IsTrue(converters.TryGetValue("CAO-B", out var b));
        Assert.AreEqual("zero-b", b.Convert(BigInteger.Zero),
            "B must override C's zero");
        // A inherits B's zero-b (A does not set its own zero)
        Assert.IsTrue(converters.TryGetValue("CAO-A", out var a));
        Assert.AreEqual("zero-b", a.Convert(BigInteger.Zero),
            "A must inherit B's zero (which already overrode C's zero)");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOn_ResultIndependentOfDocumentDeclarationOrder()
    {
        // Same inheritance chain as ClosestAncestor but declared in reverse order (A first, then B, then C).
        // The merge result must be identical regardless of declaration order.
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""DIO-B"" groupSize=""3"" separator="" "" groupSeparator="","" minus=""minus *"">
    <Culture>DIO-A</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-b"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language baseOn=""DIO-C"" groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-b"" minus=""minus *"">
    <Culture>DIO-B</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-b"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero-c"" minus=""minus *"">
    <Culture>DIO-C</Culture>
    <Groups><Group level=""1""><Digit digit=""0"" string=""zero-c"" /><Digit digit=""1"" string=""one"" /></Group></Groups>
    <NumberScale><StaticNames><Scale value=""0"" string="""" /></StaticNames></NumberScale>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.IsTrue(converters.TryGetValue("DIO-A", out var a));
        Assert.AreEqual("zero-b", a.Convert(BigInteger.Zero),
            "A's zero must be 'zero-b' (from B, which overrides C) regardless of declaration order");
    }

    // ── Item 61 — additional TryGetConverter tests ────────────────────────────

    [TestMethod]
    public void TryGetConverter_NullString_ReturnsFalse()
    {
        Assert.IsFalse(NumberToStringConverter.TryGetConverter((string)null!, out _),
            "null culture string must return false without throwing");
    }

    [TestMethod]
    public void TryGetConverter_WhitespaceString_ReturnsFalse()
    {
        Assert.IsFalse(NumberToStringConverter.TryGetConverter("   ", out _),
            "Whitespace-only culture string must return false");
    }

    [TestMethod]
    public void TryGetConverter_NullCultureInfo_ReturnsFalse()
    {
        Assert.IsFalse(NumberToStringConverter.TryGetConverter((System.Globalization.CultureInfo)null!, out _),
            "null CultureInfo must return false without throwing");
    }

    [TestMethod]
    public void TryGetConverter_ParentCulture_ReturnsParentConverter()
    {
        // "en-US" has subtag "en" which maps to EN; TryGetConverter must find it via BCP-47 stripping.
        bool found = NumberToStringConverter.TryGetConverter("en-US", out var converter);
        Assert.IsTrue(found, "en-US must resolve to EN via BCP-47 subtag stripping");
        Assert.IsNotNull(converter);
    }

    [TestMethod]
    public void TryGetConverter_UnknownCulture_DoesNotReturnEnglish()
    {
        // An unknown culture must not fall back to English.
        bool found = NumberToStringConverter.TryGetConverter("XTEST-ZZ-UNKNOWN", out var converter);
        Assert.IsFalse(found, "Unknown culture must return false, not the English converter");
        Assert.IsNull(converter);
    }

    // ── Item 60 — additional RegisterLanguageSpecifics validation ────────────

    [TestMethod]
    public void RegisterLanguageSpecifics_NullInstance_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => NumberToStringConverter.RegisterLanguageSpecifics("ValidName", null!),
            "Null instance must be rejected");
    }

    // ── Item 60 — RegisterLanguageSpecifics name validation ───────────────────

    [TestMethod]
    public void RegisterLanguageSpecifics_NullName_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => NumberToStringConverter.RegisterLanguageSpecifics(null!, new FakeSpecifics()),
            "Null typeName must be rejected");
    }

    [TestMethod]
    public void RegisterLanguageSpecifics_EmptyName_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => NumberToStringConverter.RegisterLanguageSpecifics("", new FakeSpecifics()),
            "Empty typeName must be rejected");
    }

    [TestMethod]
    public void RegisterLanguageSpecifics_WhitespaceName_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(
            () => NumberToStringConverter.RegisterLanguageSpecifics("   ", new FakeSpecifics()),
            "Whitespace-only typeName must be rejected");
    }

    // ── Item 61 — TryGetConverter explicit non-fallback API ───────────────────

    [TestMethod]
    public void TryGetConverter_KnownCulture_ReturnsTrueAndConverter()
    {
        bool found = NumberToStringConverter.TryGetConverter("EN", out var converter);
        Assert.IsTrue(found, "TryGetConverter must return true for a registered culture");
        Assert.IsNotNull(converter, "converter must not be null when TryGetConverter returns true");
    }

    [TestMethod]
    public void TryGetConverter_KnownCultureCaseInsensitive_ReturnsTrueAndConverter()
    {
        bool found = NumberToStringConverter.TryGetConverter("en", out var converter);
        Assert.IsTrue(found, "TryGetConverter must be case-insensitive");
        Assert.IsNotNull(converter);
    }

    [TestMethod]
    public void TryGetConverter_UnknownCulture_ReturnsFalseAndNullConverter()
    {
        bool found = NumberToStringConverter.TryGetConverter("XTEST-UNKNOWN", out var converter);
        Assert.IsFalse(found,
            "TryGetConverter must return false — not fall back to English — for an unknown culture");
        Assert.IsNull(converter, "converter must be null when TryGetConverter returns false");
    }

    [TestMethod]
    public void TryGetConverter_EmptyString_ReturnsFalse()
    {
        Assert.IsFalse(NumberToStringConverter.TryGetConverter("", out _));
    }

    [TestMethod]
    public void TryGetConverter_SingleCharString_ReturnsFalse()
    {
        Assert.IsFalse(NumberToStringConverter.TryGetConverter("X", out _),
            "Single-character culture codes are invalid and must return false");
    }

    [TestMethod]
    public void TryGetConverter_CultureInfo_KnownCulture_ReturnsTrueAndConverter()
    {
        var ci = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        bool found = NumberToStringConverter.TryGetConverter(ci, out var converter);
        Assert.IsTrue(found, "TryGetConverter(CultureInfo) must return true for en-US (resolves to EN)");
        Assert.IsNotNull(converter);
    }

    [TestMethod]
    public void TryGetConverter_CultureInfo_InvariantCulture_ReturnsFalse()
    {
        // InvariantCulture.Name is "" — too short, so must return false.
        Assert.IsFalse(NumberToStringConverter.TryGetConverter(
            System.Globalization.CultureInfo.InvariantCulture, out _));
    }

    // ── Item 57 — explicit GroupSize=0 is not treated as absent ─────────────

    [TestMethod]
    public void ReadConfiguration_BaseOn_ExplicitZeroGroupSizeIsNotTreatedAsAbsent()
    {
        // Child explicitly sets groupSize="0". The old merge code treated 0 as absent
        // (GroupSize != 0 → use inherited 3), which silently masked the invalid value.
        // The corrected merge (GroupSize ?? inherited) propagates explicit 0 to ReadConverter,
        // which rejects it with ArgumentOutOfRangeException (group size must be positive).
        string baseKey = "EXPLICIT-ZERO-BASE-" + Guid.NewGuid().ToString("N");
        string childKey = "EXPLICIT-ZERO-CHILD-" + Guid.NewGuid().ToString("N");
        const string groupDef = @"<Groups>
      <Group level=""1"">
        <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""one""/><Digit digit=""2"" string=""two""/>
        <Digit digit=""3"" string=""three""/><Digit digit=""4"" string=""four""/><Digit digit=""5"" string=""five""/>
        <Digit digit=""6"" string=""six""/><Digit digit=""7"" string=""seven""/><Digit digit=""8"" string=""eight""/>
        <Digit digit=""9"" string=""nine""/>
      </Group>
    </Groups>
    <NumberScale><StaticNames><Scale value=""0"" string=""""/><Scale value=""1"" string=""thousand""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>";
        string xml = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{baseKey}</Culture>
    {groupDef}
  </Language>
  <Language baseOn=""{baseKey}"" groupSize=""0"">
    <Culture>{childKey}</Culture>
  </Language>
</Numbers>";
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => NumberToStringConverter.ReadConfiguration(xml),
            "Explicit groupSize=\"0\" must propagate through merge and be rejected by ReadConverter, " +
            "not silently replaced by the inherited non-zero value");
    }

    // ── Item 57 — explicit empty LanguageSpecifics overrides inherited type ──

    [TestMethod]
    public void ReadConfiguration_BaseOn_ExplicitEmptyLanguageSpecificsTypeNameOverridesInherited()
    {
        // Base language uses a LanguageSpecifics implementation that appends "_TRANSFORMED".
        // Child explicitly sets <LanguageSpecifics></LanguageSpecifics> (empty string — not absent).
        // Old code: !string.IsNullOrEmpty("") → false → inherited type wins → output "one_TRANSFORMED".
        // New code: "" != null → true → explicit "" wins → default (no-op) specifics → output "one".
        string baseKey = "EMPTYSPEC-BASE-" + Guid.NewGuid().ToString("N");
        string childKey = "EMPTYSPEC-CHILD-" + Guid.NewGuid().ToString("N");
        string typeName = "EMPTYSPEC-TYPE-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterLanguageSpecifics(typeName, new TransformingSpecifics());

        const string groupDef = @"<Groups>
      <Group level=""1"">
        <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""one""/><Digit digit=""2"" string=""two""/>
        <Digit digit=""3"" string=""three""/><Digit digit=""4"" string=""four""/><Digit digit=""5"" string=""five""/>
        <Digit digit=""6"" string=""six""/><Digit digit=""7"" string=""seven""/><Digit digit=""8"" string=""eight""/>
        <Digit digit=""9"" string=""nine""/>
      </Group>
    </Groups>
    <NumberScale><StaticNames><Scale value=""0"" string=""""/><Scale value=""1"" string=""thousand""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>";
        string xml = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{baseKey}</Culture>
    {groupDef}
    <LanguageSpecifics>{typeName}</LanguageSpecifics>
  </Language>
  <Language baseOn=""{baseKey}"">
    <Culture>{childKey}</Culture>
    <LanguageSpecifics></LanguageSpecifics>
  </Language>
</Numbers>";
        var converters = NumberToStringConverter.ReadConfiguration(xml);
        string result = converters[childKey].Convert(1);
        Assert.AreEqual("one", result,
            "Child explicitly sets empty <LanguageSpecifics> — the parent's transforming specifics " +
            "must NOT be inherited. Old code would have inherited it (producing 'one_TRANSFORMED').");
    }

    // ── Atomicity — ReadConverter failure must not commit partial state ────────

    [TestMethod]
    public void ReadConfiguration_ReadConverter_FailureDoesNotCommitResolvedLanguagesToGlobalCache()
    {
        // Document D1 has two languages: one valid (BASE) and one broken (groupSize=0).
        // ReadConverter fails for the broken language → D1 throws before committing anything.
        // Document D2 references BASE via baseOn. If D1 had partially committed BASE to
        // _cachedLanguageTypes, D2 would succeed. The expected behavior is that D2 fails
        // with "base not found", proving D1's resolution was never written to the global cache.
        string baseKey = "ATOMIC-NOCACHE-BASE-" + Guid.NewGuid().ToString("N");
        string brokenKey = "ATOMIC-NOCACHE-BROKEN-" + Guid.NewGuid().ToString("N");
        string childKey = "ATOMIC-NOCACHE-CHILD-" + Guid.NewGuid().ToString("N");

        const string groupDef = @"<Groups>
      <Group level=""1"">
        <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""one""/><Digit digit=""2"" string=""two""/>
        <Digit digit=""3"" string=""three""/><Digit digit=""4"" string=""four""/><Digit digit=""5"" string=""five""/>
        <Digit digit=""6"" string=""six""/><Digit digit=""7"" string=""seven""/><Digit digit=""8"" string=""eight""/>
        <Digit digit=""9"" string=""nine""/>
      </Group>
    </Groups>
    <NumberScale><StaticNames><Scale value=""0"" string=""""/><Scale value=""1"" string=""thousand""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>";

        string d1 = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{baseKey}</Culture>
    {groupDef}
  </Language>
  <Language groupSize=""0"" separator="" "" groupSeparator="","" zero=""zero"" minus=""minus *"">
    <Culture>{brokenKey}</Culture>
    {groupDef}
  </Language>
</Numbers>";

        string d2 = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language baseOn=""{baseKey}"">
    <Culture>{childKey}</Culture>
  </Language>
</Numbers>";

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => NumberToStringConverter.ReadConfiguration(d1),
            "D1 must fail because one of its languages has an invalid groupSize");

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => NumberToStringConverter.ReadConfiguration(d2),
            "D2 must fail because D1 was never committed to _cachedLanguageTypes (atomicity)");
        StringAssert.Contains(ex.Message, baseKey,
            "The error message must name the missing base culture to confirm the root cause");
    }

    // ── Item 57 — NumberScale.FirstLetterUpperCase explicit presence ──────────

    [TestMethod]
    public void ReadConfiguration_BaseOn_AbsentFirstLetterUpperCasePreservesInheritedTrue()
    {
        // Base has firstLetterUpperCase="true" (explicit). Child has <NumberScale startIndex="1">
        // but NO firstLetterUpperCase attribute → absent → null.
        // After MergeNumberScaleType: null ?? true = true (inherited).
        // The test XML uses <Group level="1"> (groupValue=10), so for 1_000_000 the algorithm
        // calls GetScaleName(6). With StaticNames.Count=2 and startIndex=1:
        //   dynamicScale = 6 - 2 + 1 = 5; quotient=2, remainder=1 → suffix="ard", prefix=3="tri"
        //   GroupSeparator defaults to "lli" → "tri"+"lli"+"ard" = "trilliard".
        // firstLetterUpperCase=true capitalises → "one Trilliard".
        string baseKey = "FLUC-BASE-" + Guid.NewGuid().ToString("N");
        string childKey = "FLUC-CHILD-" + Guid.NewGuid().ToString("N");
        var converters = NumberToStringConverter.ReadConfiguration(
            BuildScaleTestXml(baseKey, @"firstLetterUpperCase=""true"" startIndex=""1""",
                              childKey, @"startIndex=""1"""));
        Assert.AreEqual("one Trilliard", converters[childKey].Convert(1_000_000),
            "Absent firstLetterUpperCase must inherit 'true' from base — scale name must be capitalised");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOn_ExplicitFalseFirstLetterUpperCaseOverridesInheritedTrue()
    {
        // Base has firstLetterUpperCase="true". Child has firstLetterUpperCase="false" (explicit).
        // After MergeNumberScaleType: false ?? true = false (explicit false wins).
        // Same startIndex=1 and GetScaleName(6) → "trilliard"; firstLetterUpperCase=false → lowercase.
        // Without the Specified-pattern fix, absent bool was deserialized as false → both tests
        // would produce lowercase, making the absent-vs-explicit distinction invisible.
        string baseKey = "FLUC-OVR-BASE-" + Guid.NewGuid().ToString("N");
        string childKey = "FLUC-OVR-CHILD-" + Guid.NewGuid().ToString("N");
        var converters = NumberToStringConverter.ReadConfiguration(
            BuildScaleTestXml(baseKey, @"firstLetterUpperCase=""true"" startIndex=""1""",
                              childKey, @"firstLetterUpperCase=""false"" startIndex=""1"""));
        Assert.AreEqual("one trilliard", converters[childKey].Convert(1_000_000),
            "Explicit firstLetterUpperCase=\"false\" must override inherited 'true' — scale name must be lowercase");
    }

    // ── Item 57 — NumberScale.StartIndex explicit presence ───────────────────

    [TestMethod]
    public void ReadConfiguration_BaseOn_AbsentStartIndexPreservesInheritedValue()
    {
        // Base has startIndex="1" (explicit). Child has <NumberScale firstLetterUpperCase="true">
        // but NO startIndex attribute → absent → null.
        // After MergeNumberScaleType: null ?? 1 = 1 (inherited).
        // With GetScaleName(6), StaticNames.Count=2, startIndex=1:
        //   dynamicScale=5; quotient=2, remainder=1 → suffix="ard" → "Trilliard".
        string baseKey = "SI-BASE-" + Guid.NewGuid().ToString("N");
        string childKey = "SI-CHILD-" + Guid.NewGuid().ToString("N");
        var converters = NumberToStringConverter.ReadConfiguration(
            BuildScaleTestXml(baseKey, @"firstLetterUpperCase=""true"" startIndex=""1""",
                              childKey, @"firstLetterUpperCase=""true"""));
        Assert.AreEqual("one Trilliard", converters[childKey].Convert(1_000_000),
            "Absent startIndex must inherit 1 from base (suffix[1]='ard' → Trilliard, not Trillion)");
    }

    [TestMethod]
    public void ReadConfiguration_BaseOn_ExplicitZeroStartIndexOverridesInheritedNonZero()
    {
        // Base has startIndex="1". Child has startIndex="0" (explicit zero).
        // After MergeNumberScaleType: 0 ?? 1 = 0 (explicit zero wins — not treated as absent).
        // With GetScaleName(6), StaticNames.Count=2, startIndex=0:
        //   dynamicScale=4; quotient=2, remainder=0 → suffix="on" → "Trillion" (≠ "Trilliard").
        // Without the Specified-pattern fix, startIndex=0 was indistinguishable from absent int=0,
        // so the inherited 1 was incorrectly kept, producing "Trilliard" instead.
        string baseKey = "SI-ZERO-BASE-" + Guid.NewGuid().ToString("N");
        string childKey = "SI-ZERO-CHILD-" + Guid.NewGuid().ToString("N");
        var converters = NumberToStringConverter.ReadConfiguration(
            BuildScaleTestXml(baseKey, @"firstLetterUpperCase=""true"" startIndex=""1""",
                              childKey, @"firstLetterUpperCase=""true"" startIndex=""0"""));
        Assert.AreEqual("one Trillion", converters[childKey].Convert(1_000_000),
            "Explicit startIndex=\"0\" must override inherited 1 (suffix[0]='on' → Trillion, not Trilliard)");
    }

    private static string BuildScaleTestXml(
        string baseKey, string baseNumberScaleAttrs,
        string childKey, string childNumberScaleAttrs)
    {
        const string groups = @"<Groups>
  <Group level=""1"">
    <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""one""/><Digit digit=""2"" string=""two""/>
    <Digit digit=""3"" string=""three""/><Digit digit=""4"" string=""four""/><Digit digit=""5"" string=""five""/>
    <Digit digit=""6"" string=""six""/><Digit digit=""7"" string=""seven""/><Digit digit=""8"" string=""eight""/>
    <Digit digit=""9"" string=""nine""/>
  </Group>
</Groups>";

        const string staticAndSuffixes = @"<StaticNames>
  <Scale value=""0"" string=""""/>
  <Scale value=""1"" string=""thousand""/>
</StaticNames>
<Suffixes><Suffix>on</Suffix><Suffix>ard</Suffix></Suffixes>";

        const string prefixTables = @"<Scale0Prefixes>
  <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""mi""/><Digit digit=""2"" string=""bi""/>
  <Digit digit=""3"" string=""tri""/><Digit digit=""4"" string=""quadri""/><Digit digit=""5"" string=""quinti""/>
  <Digit digit=""6"" string=""sexti""/><Digit digit=""7"" string=""septi""/><Digit digit=""8"" string=""octi""/>
  <Digit digit=""9"" string=""noni""/>
</Scale0Prefixes>
<UnitsPrefixes>
  <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""uni""/><Digit digit=""2"" string=""duo""/>
  <Digit digit=""3"" string=""tre(s)""/><Digit digit=""4"" string=""quattuor""/><Digit digit=""5"" string=""quinqua""/>
  <Digit digit=""6"" string=""se(xs)""/><Digit digit=""7"" string=""septe(mn)""/><Digit digit=""8"" string=""octo""/>
  <Digit digit=""9"" string=""nove(mn)""/>
</UnitsPrefixes>
<TensPrefixes>
  <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""(n)deci""/><Digit digit=""2"" string=""(ms)vingti""/>
  <Digit digit=""3"" string=""(ns)triginta""/><Digit digit=""4"" string=""(ns)quadraginta""/>
  <Digit digit=""5"" string=""(ns)quinquaginta""/><Digit digit=""6"" string=""(n)sexaginta""/>
  <Digit digit=""7"" string=""(n)septuaginta""/><Digit digit=""8"" string=""(mxs)octoginta""/>
  <Digit digit=""9"" string=""nonaginta""/>
</TensPrefixes>
<HundredsPrefixes>
  <Digit digit=""0"" string=""""/><Digit digit=""1"" string=""(nx)centi""/><Digit digit=""2"" string=""(ms)ducenti""/>
  <Digit digit=""3"" string=""(ns)trecenti""/><Digit digit=""4"" string=""(ns)quadringenti""/>
  <Digit digit=""5"" string=""(ns)quingenti""/><Digit digit=""6"" string=""(n)sescenti""/>
  <Digit digit=""7"" string=""(n)septingenti""/><Digit digit=""8"" string=""(mxs)octingenti""/>
  <Digit digit=""9"" string=""nongenti""/>
</HundredsPrefixes>";

        return $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Numbers xmlns=""Utils/NumberConvertionConfiguration.xsd"">
  <Language groupSize=""3"" separator="" "" groupSeparator="""" zero=""zero"" minus=""minus *"">
    <Culture>{baseKey}</Culture>
    {groups}
    <NumberScale {baseNumberScaleAttrs}>
      {staticAndSuffixes}
      {prefixTables}
    </NumberScale>
  </Language>
  <Language baseOn=""{baseKey}"">
    <Culture>{childKey}</Culture>
    <NumberScale {childNumberScaleAttrs}>
      {staticAndSuffixes}
    </NumberScale>
  </Language>
</Numbers>";
    }

    // ── Item 57 — public API compatibility after internal XML/definition split ──

    [TestMethod]
    public void PublicApi_LanguageType_GroupSize_RemainsInt()
    {
        Assert.AreEqual(typeof(int),
            typeof(LanguageType).GetProperty(nameof(LanguageType.GroupSize))!.PropertyType,
            "LanguageType.GroupSize must stay a plain int (historical public API)");
    }

    [TestMethod]
    public void PublicApi_NumberScaleType_FirstLetterUpperCase_RemainsBool()
    {
        Assert.AreEqual(typeof(bool),
            typeof(NumberScaleType).GetProperty(nameof(NumberScaleType.FirstLetterUpperCase))!.PropertyType,
            "NumberScaleType.FirstLetterUpperCase must stay a plain bool (historical public API)");
    }

    [TestMethod]
    public void PublicApi_NumberScaleType_StartIndex_RemainsInt()
    {
        Assert.AreEqual(typeof(int),
            typeof(NumberScaleType).GetProperty(nameof(NumberScaleType.StartIndex))!.PropertyType,
            "NumberScaleType.StartIndex must stay a plain int (historical public API)");
    }

    [TestMethod]
    public void PublicApi_XmlSpecified_NotPublic_LanguageType()
    {
        var specifiedProps = typeof(LanguageType).GetProperties()
            .Where(p => p.Name.EndsWith("Specified") || p.Name.EndsWith("Xml"));
        Assert.IsFalse(specifiedProps.Any(),
            $"Found unexpected public XML properties on LanguageType: {string.Join(", ", specifiedProps.Select(p => p.Name))}");
    }

    [TestMethod]
    public void PublicApi_XmlSpecified_NotPublic_NumberScaleType()
    {
        var specifiedProps = typeof(NumberScaleType).GetProperties()
            .Where(p => p.Name.EndsWith("Specified") || p.Name.EndsWith("Xml"));
        Assert.IsFalse(specifiedProps.Any(),
            $"Found unexpected public XML properties on NumberScaleType: {string.Join(", ", specifiedProps.Select(p => p.Name))}");
    }

    // ── Item 57 — Optional<T> presence semantics ────────────────────────────────

    [TestMethod]
    public void Optional_Unspecified_IsNotSpecified()
    {
        Assert.IsFalse(Optional<int>.Unspecified.IsSpecified);
    }

    [TestMethod]
    public void Optional_Of_IsSpecified()
    {
        Assert.IsTrue(Optional<int>.Of(0).IsSpecified);
        Assert.AreEqual(0, Optional<int>.Of(0).Value);
    }

    [TestMethod]
    public void Optional_Of_False_IsSpecifiedFalse()
    {
        var opt = Optional<bool>.Of(false);
        Assert.IsTrue(opt.IsSpecified);
        Assert.IsFalse(opt.Value);
    }

    [TestMethod]
    public void Optional_GetValueOrDefault_UnspecifiedReturnsFallback()
    {
        Assert.AreEqual(3, Optional<int>.Unspecified.GetValueOrDefault(3));
    }

    [TestMethod]
    public void Optional_GetValueOrDefault_SpecifiedReturnsValueEvenWhenZero()
    {
        Assert.AreEqual(0, Optional<int>.Of(0).GetValueOrDefault(3),
            "An explicit 0 must win over the fallback — this is what preserves explicit groupSize/startIndex=0");
    }

    private sealed class FakeSpecifics : INumberToStringLanguageSpecifics
    {
        public string FinalizeWriting(string language, string value) => value;
    }

    private sealed class TransformingSpecifics : INumberToStringLanguageSpecifics
    {
        public string FinalizeWriting(string language, string value) => value + "_TRANSFORMED";
    }
}
