using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// NTS-04 — regression tests for the general constituent-local "ForcedVariants" mechanism.
/// A configured lexical constituent (a time unit, a currency unit/subunit, a fraction term) may
/// force grammatical variant dimensions (e.g. gender) on the numeric fragment it governs, without
/// requiring the caller to know the constituent's intrinsic grammar and without leaking into other
/// fragments or calls.
/// </summary>
[TestClass]
public class NumberToStringConverterForcedVariantsTests
{
    // ─── Red tests — reproduce the pre-fix defect (French time units default to masculine) ────

    // ─── Non-regression — ForcedVariants must not leak into the global cardinal default ────────

    // ─── Non-regression — EN/DE time output unchanged ───────────────────────────────────────────

    // ─── Anti-leak — sequential conversions on the same converter instance ─────────────────────

    // ─── Currency — unit and subunit force independent local variants ─────────────────────────

    // ─── Engine-level proof — synthetic converter, independent of French linguistic data ───────

    [TestMethod]
    public void Convert_Synthetic_ForcedDimensionDoesNotEraseUnrelatedCallerDimension()
    {
        // Two independent variant dimensions. The "hour" unit forces only "gender"; a caller-
        // supplied "case" value must remain visible to a rule that depends only on "case".
        var sourceConverter = NumberToStringConverter.GetConverter("EN");
        string baseToken = sourceConverter.Convert(1);
        var options = new NumberToStringConverterOptions(sourceConverter)
        {
            LanguageSpecifics = new DefaultNumberToStringLanguageSpecifics(),
            VariantDimensions =
            [
                new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"]),
                new NumberToStringConverter.VariantDimension("case", ["nominative", "dative"]),
            ],
            VariantRules =
            [
                // Distinct priorities: both constraints can be satisfied simultaneously (case=dative
                // AND gender=feminine), so the engine requires an explicit tie-break to stay deterministic.
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["case"] = "dative" },
                    [new NumberToStringConverter.ReplacementRule(baseToken, baseToken + "-DAT", ReplacementScope.Standalone)],
                    priority: 0),
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "feminine" },
                    [new NumberToStringConverter.ReplacementRule(baseToken, baseToken + "-FEM", ReplacementScope.Standalone)],
                    priority: 1),
            ],
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("unit", "units", null),
            },
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
            {
                ["hour"] = ForcedVariantSet.Create(("gender", "feminine")),
            },
        };
        var synthetic = new NumberToStringConverter(options);

        // The "hour" constituent forces gender=feminine only. The caller's case=dative survives
        // the overlay, so the case-only rule still fires — proving the forced overlay merges
        // dimension-by-dimension rather than replacing the whole query.
        Assert.AreEqual($"{baseToken}-DAT unit", synthetic.Convert(new TimeSpan(1, 0, 0), "case=dative", "gender=masculine"));

        // Independently, an ordinary cardinal with an explicit gender=feminine (no case) exercises
        // the gender-only rule, proving the forced value on the constituent is a real, working
        // dimension value and not a name collision with the rule above.
        Assert.AreEqual($"{baseToken}-FEM", synthetic.Convert(1, "gender=feminine"));
    }

    /// <summary>Verifies that a forced variant overrides a conflicting caller variant for its constituent.</summary>
    [TestMethod]
    public void Convert_Synthetic_ForcedVariantOverridesConflictingCallerVariant()
    {
        var sourceConverter = NumberToStringConverter.GetConverter("EN");
        string baseToken = sourceConverter.Convert(1);
        var options = new NumberToStringConverterOptions(sourceConverter)
        {
            LanguageSpecifics = new DefaultNumberToStringLanguageSpecifics(),
            VariantDimensions =
            [
                new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"]),
            ],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "masculine" },
                    [new NumberToStringConverter.ReplacementRule(baseToken, baseToken + "-MASC", ReplacementScope.Standalone)]),
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "feminine" },
                    [new NumberToStringConverter.ReplacementRule(baseToken, baseToken + "-FEM", ReplacementScope.Standalone)]),
            ],
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("unit", "units", null),
            },
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
            {
                ["hour"] = ForcedVariantSet.Create(("gender", "feminine")),
            },
        };
        var synthetic = new NumberToStringConverter(options);

        Assert.AreEqual(
            $"{baseToken}-FEM unit",
            synthetic.Convert(new TimeSpan(1, 0, 0), "gender=masculine"));
    }

    [TestMethod]
    public void Convert_Synthetic_NoStateLeakAcrossDifferentlyConstrainedConstituents()
    {
        var sourceConverter = NumberToStringConverter.GetConverter("EN");
        string baseToken = sourceConverter.Convert(1);
        var options = new NumberToStringConverterOptions(sourceConverter)
        {
            LanguageSpecifics = new DefaultNumberToStringLanguageSpecifics(),
            VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "feminine" },
                    [new NumberToStringConverter.ReplacementRule(baseToken, baseToken + "-FEM", ReplacementScope.Standalone)]),
            ],
        };
        var femToken = new CurrencyDefinition
        {
            UnitSingular = "token", UnitPlural = "tokens",
            SubunitSingular = "sub", SubunitPlural = "subs",
            UnitForcedVariants = ForcedVariantSet.Create(("gender", "feminine")),
        };
        var mascToken = new CurrencyDefinition
        {
            UnitSingular = "token", UnitPlural = "tokens",
            SubunitSingular = "sub", SubunitPlural = "subs",
        };
        var synthetic = new NumberToStringConverter(options);

        Assert.AreEqual($"{baseToken}-FEM token", synthetic.ConvertCurrency(1m, femToken));
        Assert.AreEqual($"{baseToken} token", synthetic.ConvertCurrency(1m, mascToken));
        Assert.AreEqual(baseToken, synthetic.Convert(1));
    }

    // ─── Immutability / snapshot ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void TimeUnitForcedVariants_MutatingSourceDictionaryAfterConstruction_DoesNotAffectConverter()
    {
        NumberToStringConverter sourceConverter = NumberToStringConverter.GetConverter("EN");
        string baseToken = sourceConverter.Convert(1);
        var source = new Dictionary<string, ForcedVariantSet>
        {
            ["hour"] = ForcedVariantSet.Create(("form", "alternate")),
        };
        var options = new NumberToStringConverterOptions(sourceConverter)
        {
            VariantDimensions = [new NumberToStringConverter.VariantDimension("form", ["base", "alternate"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["form"] = "alternate" },
                    [new NumberToStringConverter.ReplacementRule(baseToken, "ALT", ReplacementScope.Standalone)]),
            ],
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("UNIT", "UNITS", null),
            },
            TimeUnitForcedVariants = source,
        };
        var converter = new NumberToStringConverter(options);
        Assert.AreEqual("ALT UNIT", converter.Convert(new TimeSpan(1, 0, 0)));

        source["hour"] = ForcedVariantSet.Empty;

        Assert.AreEqual("ALT UNIT", converter.Convert(new TimeSpan(1, 0, 0)));
        Assert.AreEqual($"{baseToken} UNIT", new NumberToStringConverter(options).Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── Dimension alias canonicalization ──────────────────────────────────────────────────────

    /// <summary>Verifies duplicate canonical and local dimension names are rejected for forced time-unit variants.</summary>
    [TestMethod]
    public void TimeUnitForcedVariants_CanonicalAndAliasDuplicate_ThrowsUnts004()
    {
        var options = CreateAliasedVariantOptions();
        options.TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
        {
            ["hour"] = ForcedVariantSet.Parse("form=alternate,localForm=base"),
        };

        var exception = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => new NumberToStringConverter(options));
        Assert.AreEqual("UNTS004", exception.ErrorCode);
    }

    /// <summary>Verifies duplicate canonical and local dimension names are rejected for currency forced variants.</summary>
    [TestMethod]
    public void CurrencyForcedVariants_CanonicalAndAliasDuplicate_ThrowsUnts004()
    {
        var converter = new NumberToStringConverter(CreateAliasedVariantOptions());
        var currency = new CurrencyDefinition
        {
            UnitSingular = "UNIT",
            UnitPlural = "UNITS",
            SubunitSingular = "SUBUNIT",
            SubunitPlural = "SUBUNITS",
            UnitForcedVariants = ForcedVariantSet.Parse("form=alternate,localForm=base"),
        };

        var exception = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => converter.ConvertCurrency(1m, currency));
        Assert.AreEqual("UNTS004", exception.ErrorCode);
    }

    // ─── FromCulture round-trip ─────────────────────────────────────────────────────────────────

    /// <summary>Verifies that loading options from a registered culture preserves forced-variant metadata.</summary>
    [TestMethod]
    public void FromCulture_PreservesTimeUnitForcedVariants()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("FR");
        NumberToStringConverterOptions options = NumberToStringConverterOptions.FromCulture("FR");

        CollectionAssert.AreEquivalent(
            source.TimeUnitForcedVariants.Keys.ToArray(),
            options.TimeUnitForcedVariants.Keys.ToArray());
    }

    // ─── Caller validation is unaffected by ForcedVariants ─────────────────────────────────────

    /// <summary>Verifies caller variants are validated before a constituent's forced overlay is applied.</summary>
    [TestMethod]
    public void Convert_TimeSpan_InvalidCallerVariant_ThrowsBeforeForcedOverlay()
    {
        var options = CreateAliasedVariantOptions();
        options.TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
        {
            ["hour"] = ForcedVariantSet.Create(("form", "alternate")),
        };
        var converter = new NumberToStringConverter(options);

        Assert.ThrowsExactly<ArgumentException>(() => converter.Convert(TimeSpan.FromHours(1), "form=invalid"));
    }

    // ─── ForcedVariantSet.Create — programmatic construction edge cases ───────────────────────

    [TestMethod]
    public void ForcedVariantSet_Create_NullSequence_ReturnsEmpty()
    {
        Assert.AreSame(ForcedVariantSet.Empty, ForcedVariantSet.Create(null!));
    }

    [TestMethod]
    public void ForcedVariantSet_Create_EmptySequence_ReturnsEmpty()
    {
        Assert.AreSame(ForcedVariantSet.Empty, ForcedVariantSet.Create());
    }

    [TestMethod]
    public void ForcedVariantSet_Create_EnumerableInput_IsAcceptedOnceNotJustArrayLiteral()
    {
        // Proves the public factory accepts a genuine IEnumerable<T>, not only an array/params
        // literal — the repository-preferred `params IEnumerable<T>` shape (AGENTS.md).
        IEnumerable<(string Dimension, string Value)> source = new List<(string, string)> { ("gender", "feminin") };
        var forced = ForcedVariantSet.Create(source);
        Assert.IsFalse(forced.IsEmpty);
    }

    [TestMethod]
    public void ForcedVariantSet_Create_EmptyDimension_ThrowsMalformedSyntax()
    {
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(
            () => ForcedVariantSet.Create(("", "feminin")));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Create_EmptyValue_ThrowsMalformedSyntax()
    {
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(
            () => ForcedVariantSet.Create(("gender", "")));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Create_DuplicateRawDimension_ThrowsDuplicateConstraint()
    {
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(
            () => ForcedVariantSet.Create(("gender", "feminin"), ("gender", "masculin")));
        Assert.AreEqual("UNTS004", ex.ErrorCode);
    }

    // ─── Invalid ForcedVariants configuration ──────────────────────────────────────────────────

    [TestMethod]
    public void ForcedVariantSet_Parse_UnknownValue_ThrowsAtConverterConstruction()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"])],
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)> { ["hour"] = ("hour", "hours", null) },
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet> { ["hour"] = ForcedVariantSet.Parse("gender=banana") },
        };
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => new NumberToStringConverter(options));
        Assert.AreEqual("UNTS006", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Parse_UnknownDimension_ThrowsAtConverterConstruction()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"])],
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)> { ["hour"] = ("hour", "hours", null) },
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet> { ["hour"] = ForcedVariantSet.Parse("unknown=value") },
        };
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => new NumberToStringConverter(options));
        Assert.AreEqual("UNTS006", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Parse_MissingValue_ThrowsMalformedSyntax()
    {
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => ForcedVariantSet.Parse("gender="));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Parse_MissingDimension_ThrowsMalformedSyntax()
    {
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => ForcedVariantSet.Parse("=feminin"));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Parse_DuplicateDimension_ThrowsDuplicateConstraint()
    {
        var ex = Assert.ThrowsExactly<NumberToStringConfigurationException>(
            () => ForcedVariantSet.Parse("gender=feminin,gender=masculin"));
        Assert.AreEqual("UNTS004", ex.ErrorCode);
    }

    [TestMethod]
    public void TimeUnitForcedVariants_KeyWithoutMatchingTimeUnitsEntry_ThrowsArgumentException()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
            {
                ["nonexistent"] = ForcedVariantSet.Empty,
            },
        };
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(options));
    }

    [TestMethod]
    public void ConvertCurrency_InvalidUnitForcedVariant_ThrowsBeforeRenderingAnyFragment()
    {
        int finalizeCallCount = 0;
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = new CountingLanguageSpecifics(() => finalizeCallCount++),
            VariantDimensions = [new NumberToStringConverter.VariantDimension("form", ["base", "alternate"])],
        };
        var converter = new NumberToStringConverter(options);
        var invalid = new CurrencyDefinition
        {
            UnitSingular = "UNIT",
            UnitPlural = "UNITS",
            SubunitSingular = "SUBUNIT",
            SubunitPlural = "SUBUNITS",
            UnitForcedVariants = ForcedVariantSet.Create(("form", "invalid")),
        };

        Assert.ThrowsExactly<NumberToStringConfigurationException>(() => converter.ConvertCurrency(21m, invalid));
        Assert.AreEqual(0, finalizeCallCount);
    }

    /// <summary>Records how many times finalization is invoked, without altering the text.</summary>
    private sealed class CountingLanguageSpecifics(Action onFinalize) : INumberToStringLanguageSpecifics
    {
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            onFinalize();
            return text;
        }
    }

    /// <summary>Creates synthetic options with one canonical dimension, one local alias, and one time unit.</summary>
    private static NumberToStringConverterOptions CreateAliasedVariantOptions()
        => new(NumberToStringConverter.GetConverter("EN"))
        {
            VariantDimensions =
            [
                new NumberToStringConverter.VariantDimension("form", ["base", "alternate"], "localForm"),
            ],
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("UNIT", "UNITS", null),
            },
        };
}
