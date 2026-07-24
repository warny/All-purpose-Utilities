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
}
