using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for the 6 engine improvements:
/// A1 — Convert(double/float), A2 — ConvertFraction, A3 — ConvertMultiplicative,
/// B1 — GroupConnector, B2 — StartsWith/EndsWith scopes, B3 — variant dimension validation.
/// </summary>
[TestClass]
public class NumberToStringConverterEngineImprovementsTests
{
    // ─── A1 — Convert(double/float) ────────────────────────────────────────
    // These are DEFAULT INTERFACE METHODS on INumberToStringConverter.
    // They must be called via the interface type (not the concrete type)
    // because C# default interface methods are not inherited by concrete classes.
    // The concrete NumberToStringConverter has Convert(Number) which handles double
    // via rational conversion (e.g. 2.5 → "five over two"); the interface default
    // instead parses via decimal.TryParse("R") and delegates to Convert(decimal).

    [TestMethod]
    public void Convert_Double_WholeNumbers()
    {
        INumberToStringConverter en = NumberToStringConverter.GetConverter("EN");
        // 3.0 → round-trip "3" → decimal 3 → Convert(decimal 3) → "three"
        Assert.AreEqual("three", en.Convert(3.0));
        Assert.AreEqual(en.Convert(3), en.Convert(3.0));
        Assert.AreEqual("zero", en.Convert(0.0));
    }

    [TestMethod]
    public void Convert_Double_Negative()
    {
        INumberToStringConverter en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual("minus five", en.Convert(-5.0));
    }

    [TestMethod]
    public void Convert_Double_WithDecimalPart()
    {
        INumberToStringConverter en = NumberToStringConverter.GetConverter("EN");
        // 3.14 → round-trip "3.14" → decimal 3.14 → "three point fourteen hundredths"
        var result = en.Convert(3.14);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.StartsWith("three", StringComparison.Ordinal), $"Unexpected: '{result}'");
        Assert.IsTrue(result.Contains("fourteen"), $"Decimal part not found in: '{result}'");
    }

    [TestMethod]
    public void Convert_Double_HalfInteger()
    {
        INumberToStringConverter en = NumberToStringConverter.GetConverter("EN");
        // 2.5 → round-trip "2.5" → decimal 2.5 → "two point five tenths"
        var result = en.Convert(2.5);
        Assert.IsTrue(result.StartsWith("two", StringComparison.Ordinal), $"Unexpected: '{result}'");
        Assert.IsTrue(result.Contains("five"), $"Decimal part not found in: '{result}'");
    }

    [TestMethod]
    public void Convert_Float_DelegatesToDouble()
    {
        // float and double 2.5 both parse identically via "R" format → same Convert(decimal) result
        INumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual(fr.Convert(2.5), fr.Convert((float)2.5));
    }

    [TestMethod]
    public void Convert_Double_WithVariants()
    {
        // double/float are default interface methods — access via INumberToStringConverter
        INumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");
        // Convert(1.0, "gender=feminin") → "une" (via decimal 1 → "un" → variant "une")
        var masculine = fr.Convert(1.0);
        var feminine  = fr.Convert(1.0, "gender=feminin");
        Assert.AreEqual("un",  masculine);
        Assert.AreEqual("une", feminine);
    }

    [TestMethod]
    public void Convert_Float_WithVariants()
    {
        // double/float are default interface methods — access via INumberToStringConverter
        INumberToStringConverter fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual(fr.Convert(2.5, "gender=feminin"), fr.Convert((float)2.5, "gender=feminin"));
    }

    // ─── A2 — ConvertFraction ───────────────────────────────────────────────

    [TestMethod]
    public void ConvertFraction_EN_NonDecimalDenominator_UsesOverConnector()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        // 3 is not a power of 10, falls back to "numerator over denominator"
        var result = en.ConvertFraction(1, 3);
        Assert.AreEqual("one over three", result);
    }

    [TestMethod]
    public void ConvertFraction_EN_PowerOfTenDenominator_UsesFractionSuffix()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        // denominator 10 → 1 digit → "tenth(s)" suffix is configured
        var result = en.ConvertFraction(1, 10);
        Assert.IsTrue(result.Contains("tenth"), $"Expected fraction suffix, got: '{result}'");
    }

    [TestMethod]
    public void ConvertFraction_EN_TwoOver_Four()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var result = en.ConvertFraction(2, 4);
        // 4 is not a power of 10 → "two over four"
        Assert.AreEqual("two over four", result);
        // check via interface too
        INumberToStringConverter iface = en;
        // Interface default delegates to the converter override
        Assert.AreEqual(result, iface.ConvertFraction(2, 4));
    }

    [TestMethod]
    public void ConvertFraction_FR_UsesOverConnector()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var result = fr.ConvertFraction(1, 3);
        // FR fractionSeparator is "sur"
        Assert.IsTrue(result.Contains("sur"), $"Expected 'sur', got: '{result}'");
        Assert.IsTrue(result.Contains("un"),  $"Expected 'un', got: '{result}'");
        Assert.IsTrue(result.Contains("trois"), $"Expected 'trois', got: '{result}'");
    }

    [TestMethod]
    public void ConvertFraction_Interface_DefaultFallback()
    {
        // A minimal converter uses the interface default which concatenates with " / "
        INumberToStringConverter minimal = new MinimalConverterForFractionTest();
        var result = minimal.ConvertFraction(2, 4);
        // Default: "2 / 4"
        Assert.AreEqual("2 / 4", result);
    }

    // ─── A3 — ConvertMultiplicative ─────────────────────────────────────────

    [TestMethod]
    public void ConvertMultiplicative_EN_NamedForms()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.IsTrue(en.SupportsMultiplicative, "EN should support multiplicative");
        Assert.AreEqual("once",   en.ConvertMultiplicative(1));
        Assert.AreEqual("twice",  en.ConvertMultiplicative(2));
        Assert.AreEqual("thrice", en.ConvertMultiplicative(3));
    }

    [TestMethod]
    public void ConvertMultiplicative_EN_FallbackSuffix()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual("four times", en.ConvertMultiplicative(4));
        Assert.AreEqual("ten times",  en.ConvertMultiplicative(10));
        Assert.AreEqual("one hundred times", en.ConvertMultiplicative(100));
    }

    [TestMethod]
    public void ConvertMultiplicative_FR_NamedForms()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.IsTrue(fr.SupportsMultiplicative, "FR should support multiplicative");
        Assert.AreEqual("une fois",  fr.ConvertMultiplicative(1));
        Assert.AreEqual("deux fois", fr.ConvertMultiplicative(2));
        Assert.AreEqual("trois fois", fr.ConvertMultiplicative(3));
    }

    [TestMethod]
    public void ConvertMultiplicative_FR_FallbackSuffix()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // 4 is not named → Convert(4) + " fois" = "quatre fois"
        Assert.AreEqual("quatre fois", fr.ConvertMultiplicative(4));
    }

    [TestMethod]
    public void ConvertMultiplicative_Unsupported_Throws()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        Assert.IsFalse(de.SupportsMultiplicative, "DE should not support multiplicative");
        Assert.ThrowsException<NotSupportedException>(() => de.ConvertMultiplicative(2));
    }

    [TestMethod]
    public void SupportsMultiplicative_Interface_DefaultFalse()
    {
        INumberToStringConverter minimal = new MinimalConverterForFractionTest();
        Assert.IsFalse(minimal.SupportsMultiplicative);
        Assert.ThrowsException<NotSupportedException>(() => minimal.ConvertMultiplicative(1));
    }

    // ─── B1 — Group connector ───────────────────────────────────────────────

    [TestMethod]
    public void GroupConnector_InjectedBelowThreshold()
    {
        var enBase = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(enBase)
        {
            GroupConnector = "and",
            GroupConnectorThreshold = 100,
        };
        var en = new NumberToStringConverter(opts);

        // lower group < 100 → connector
        Assert.AreEqual("one thousand and one",          en.Convert(1001));
        Assert.AreEqual("one thousand and ninety-nine",  en.Convert(1099));
    }

    [TestMethod]
    public void GroupConnector_NotInjectedAtOrAboveThreshold()
    {
        var enBase = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(enBase)
        {
            GroupConnector = "and",
            GroupConnectorThreshold = 100,
        };
        var en = new NumberToStringConverter(opts);

        // lower group = 100, not < 100 → use GroupSeparator
        Assert.AreEqual("one thousand, one hundred",          en.Convert(1100));
        // lower group = 101, not < 100 → use GroupSeparator ("," from EN groupSeparator)
        Assert.AreEqual("one thousand, one hundred and one",  en.Convert(1101));
    }

    [TestMethod]
    public void GroupConnector_DisabledWhenNull()
    {
        // Standard EN (no connector) should produce "one thousand, one"
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual("one thousand, one",              en.Convert(1001));
        Assert.AreEqual("one thousand, one hundred",       en.Convert(1100));
    }

    [TestMethod]
    public void GroupConnector_RoundTrip_ViaOptions()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(en);
        // Default GroupConnector should be null (not configured for EN)
        Assert.IsNull(opts.GroupConnector);
        Assert.AreEqual(100, opts.GroupConnectorThreshold);  // default value
    }

    // ─── B2 — StartsWith / EndsWith replacement scopes ─────────────────────

    [TestMethod]
    public void ReplacementScope_StartsWith_AppliedAtStart()
    {
        var enBase = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(enBase)
        {
            Replacements = enBase.Replacements.Append(
                new NumberToStringConverter.ReplacementRule("one ", "a ", ReplacementScope.StartsWith)
            ).ToList(),
        };
        var en = new NumberToStringConverter(opts);

        // "one hundred" starts with "one " → "a hundred"
        Assert.AreEqual("a hundred", en.Convert(100));
        // "twenty-one" does not start with "one " → unchanged
        Assert.AreEqual("twenty-one", en.Convert(21));
    }

    [TestMethod]
    public void ReplacementScope_StartsWith_DoesNotAffectMiddle()
    {
        var enBase = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(enBase)
        {
            Replacements = enBase.Replacements.Append(
                new NumberToStringConverter.ReplacementRule("one ", "a ", ReplacementScope.StartsWith)
            ).ToList(),
        };
        var en = new NumberToStringConverter(opts);

        // "one thousand, one" — starts with "one " so gets "a " prefix
        // The group connector is null, so:  "one thousand, one"
        // Does it start with "one "? Yes → "a thousand, one"
        var result = en.Convert(1001);
        Assert.IsTrue(result.StartsWith("a ", StringComparison.Ordinal), $"Expected 'a ', got: '{result}'");
    }

    [TestMethod]
    public void ReplacementScope_EndsWith_AppliedAtEnd()
    {
        var enBase = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(enBase)
        {
            Replacements = enBase.Replacements.Append(
                new NumberToStringConverter.ReplacementRule("one", "1", ReplacementScope.EndsWith)
            ).ToList(),
        };
        var en = new NumberToStringConverter(opts);

        // "twenty-one" ends with "one" → "twenty-1"
        Assert.AreEqual("twenty-1", en.Convert(21));
        // "one hundred" ends with "hundred", not "one" → unchanged
        Assert.AreEqual("one hundred", en.Convert(100));
    }

    [TestMethod]
    public void ReplacementScope_StartsWith_ViaVariantRules()
    {
        // StartsWith in ApplyVariantReplacement (variant rules path)
        var enBase = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(enBase)
        {
            VariantRules = new List<NumberToStringConverter.VariantRule>
            {
                new(
                    new Dictionary<string, string> { ["marker"] = "test" },
                    new List<NumberToStringConverter.ReplacementRule>
                    {
                        new("one ", "a ", ReplacementScope.StartsWith)
                    }
                )
            },
            VariantDimensions = new List<NumberToStringConverter.VariantDimension>
            {
                new("marker", ["test", "no"])
            },
        };
        var en = new NumberToStringConverter(opts);

        // With variant marker=test, StartsWith fires
        Assert.AreEqual("a hundred", en.Convert(100, "marker=test"));
        // Without variant (default=test), StartsWith still fires
        Assert.AreEqual("a hundred", en.Convert(100));
        // twenty-one → doesn't start with "one " → unchanged
        Assert.AreEqual("twenty-one", en.Convert(21, "marker=test"));
    }

    [TestMethod]
    public void ReplacementScope_EndsWith_ViaVariantRules()
    {
        var enBase = NumberToStringConverter.GetConverter("EN");
        var opts = new NumberToStringConverterOptions(enBase)
        {
            VariantRules = new List<NumberToStringConverter.VariantRule>
            {
                new(
                    new Dictionary<string, string> { ["marker"] = "test" },
                    new List<NumberToStringConverter.ReplacementRule>
                    {
                        new("one", "1", ReplacementScope.EndsWith)
                    }
                )
            },
            VariantDimensions = new List<NumberToStringConverter.VariantDimension>
            {
                new("marker", ["test"])
            },
        };
        var en = new NumberToStringConverter(opts);

        // "twenty-one" ends with "one" → "twenty-1"
        Assert.AreEqual("twenty-1", en.Convert(21, "marker=test"));
        // "one hundred" ends with "hundred" → unchanged
        Assert.AreEqual("one hundred", en.Convert(100, "marker=test"));
    }

    // ─── B3 — Variant dimension validation ──────────────────────────────────

    [TestMethod]
    public void VariantValidation_ExistingConverters_LoadWithoutError()
    {
        // Verifies that the validation does not throw for any standard language
        foreach (var culture in new[] { "EN", "FR", "DE", "ES", "IT", "PT", "NL", "CA", "GL",
                                         "FI", "RU", "PL", "AR", "HE", "ZH", "JA", "KO",
                                         "EU", "HI", "EL", "WO", "ZU", "EE" })
        {
            var conv = NumberToStringConverter.GetConverter(culture);
            Assert.IsNotNull(conv, $"Converter for {culture} must not be null");
        }
    }

    [TestMethod]
    public void VariantValidation_NoFalsePositives_ForFR()
    {
        // FR has a Variants section → validation must not throw
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.IsNotNull(fr);
        Assert.AreEqual("une", fr.Convert(1, "gender=feminin"));
    }

    [TestMethod]
    public void VariantValidation_NoFalsePositives_ForDE()
    {
        // DE has Variants with multiple dimensions → validation must not throw
        var de = NumberToStringConverter.GetConverter("DE");
        Assert.IsNotNull(de);
        Assert.AreEqual("eine", de.Convert(1, "genus=feminin"));
    }

    // ─── Helper types ───────────────────────────────────────────────────────

    private sealed class MinimalConverterForFractionTest : INumberToStringConverter
    {
        public BigInteger? MaxNumber => null;
        public string Convert(BigInteger number) => number.ToString();
        public string Convert(int     number)    => number.ToString();
        public string Convert(long    number)    => number.ToString();
        public string Convert(decimal number)    => number.ToString();
    }
}
