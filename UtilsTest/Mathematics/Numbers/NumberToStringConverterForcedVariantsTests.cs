using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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

    [TestMethod]
    public void Convert_TimeSpan_FR_OneHour_NoExplicitVariant_IsFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // "heure" is intrinsically feminine; the caller must not need to know that.
        Assert.AreEqual("une heure", fr.Convert(new TimeSpan(1, 0, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_TwentyOneHours_NoExplicitVariant_IsFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // A compound number: Count1Form alone (count==1 only) cannot fix this — the whole
        // cardinal fragment must be built with gender=feminin in its variant query.
        Assert.AreEqual("vingt et une heures", fr.Convert(TimeSpan.FromHours(21)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_OneMinute_NoExplicitVariant_IsFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual("une minute", fr.Convert(new TimeSpan(0, 1, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_TwentyOneMinutes_NoExplicitVariant_IsFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual("vingt et une minutes", fr.Convert(new TimeSpan(0, 21, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_OneSecond_NoExplicitVariant_IsFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual("une seconde", fr.Convert(new TimeSpan(0, 0, 1)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_TwentyOneSeconds_NoExplicitVariant_IsFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual("vingt et une secondes", fr.Convert(new TimeSpan(0, 0, 21)));
    }

    [TestMethod]
    public void Convert_TimeOnly_FR_OneHourTwentyOneMinutesTwentyOneSeconds_AllFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // Composite proof: each constituent (hour/minute/second) independently forces its own
        // gender on its own fragment — no leakage between them.
        Assert.AreEqual("une heure vingt et une minutes vingt et une secondes",
            fr.Convert(new TimeOnly(1, 21, 21)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_TwentyOneHours_ExplicitMasculineIsOverriddenByForcedFeminine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // "Forced means forced": a contradictory caller-supplied value does not win locally.
        Assert.AreEqual("vingt et une heures", fr.Convert(TimeSpan.FromHours(21), "gender=masculin"));
    }

    // ─── Non-regression — ForcedVariants must not leak into the global cardinal default ────────

    [TestMethod]
    public void Convert_FR_OrdinaryCardinal_RemainsMasculineByDefault()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual("un", fr.Convert(1));
        Assert.AreEqual("vingt et un", fr.Convert(21));
    }

    // ─── Non-regression — EN/DE time output unchanged ───────────────────────────────────────────

    [TestMethod]
    public void Convert_TimeSpan_EN_Unaffected()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual("one hour", en.Convert(new TimeSpan(1, 0, 0)));
        Assert.AreEqual("two hours thirty minutes five seconds", en.Convert(new TimeSpan(2, 30, 5)));
    }

    [TestMethod]
    public void Convert_TimeSpan_DE_Unaffected_Count1FormStillApplies()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        Assert.AreEqual("eine Stunde", de.Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── Anti-leak — sequential conversions on the same converter instance ─────────────────────

    [TestMethod]
    public void Convert_FR_SequentialCalls_DoNotLeakForcedVariantStateAcrossCalls()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual("une heure", fr.Convert(new TimeSpan(1, 0, 0)));
        Assert.AreEqual("un", fr.Convert(1));
        Assert.AreEqual("une", fr.Convert(1, "gender=feminin"));
        Assert.AreEqual("un", fr.Convert(1));
    }

    // ─── Currency — unit and subunit force independent local variants ─────────────────────────

    private static CurrencyDefinition EuroCurrency() => new()
    {
        UnitSingular = "euro",
        UnitPlural = "euros",
        SubunitSingular = "centime",
        SubunitPlural = "centimes",
        Connector = "et",
        // Masculine is already the FR default: no forcing needed (spec point 19).
    };

    private static CurrencyDefinition LivreCurrency() => new()
    {
        UnitSingular = "livre",
        UnitPlural = "livres",
        SubunitSingular = "sou",
        SubunitPlural = "sous",
        Connector = "et",
        UnitForcedVariants = ForcedVariantSet.Create(("gender", "feminin")),
        SubunitForcedVariants = ForcedVariantSet.Create(("gender", "feminin")),
    };

    [TestMethod]
    public void ConvertCurrency_FR_MasculineCurrency_NoExplicitVariant()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var euro = EuroCurrency();
        Assert.AreEqual("un euro", fr.ConvertCurrency(1m, euro));
        Assert.AreEqual("vingt et un euros", fr.ConvertCurrency(21m, euro));
    }

    [TestMethod]
    public void ConvertCurrency_FR_FeminineCurrency_ForcedByUnitAlone_NoExplicitVariant()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var livre = LivreCurrency();
        Assert.AreEqual("une livre", fr.ConvertCurrency(1m, livre));
        Assert.AreEqual("vingt et une livres", fr.ConvertCurrency(21m, livre));
    }

    [TestMethod]
    public void ConvertCurrency_FR_UnitAndSubunit_ForceIndependentVariants_InSamePhrase()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // Synthetic currency: masculine main unit ("franc"), feminine subunit ("centime" forced
        // feminine here purely to exercise the mechanism — not real French grammar).
        var mixed = new CurrencyDefinition
        {
            UnitSingular = "franc",
            UnitPlural = "francs",
            SubunitSingular = "centime",
            SubunitPlural = "centimes",
            Connector = "et",
            SubunitForcedVariants = ForcedVariantSet.Create(("gender", "feminin")),
        };

        string unitsPart = fr.Convert(21L);                     // masculine default: "vingt et un"
        string subunitsPart = fr.Convert(21L, "gender=feminin"); // forced feminine: "vingt et une"
        string expected = $"{unitsPart} francs et {subunitsPart} centimes";

        Assert.AreEqual(expected, fr.ConvertCurrency(21.21m, mixed));
    }

    [TestMethod]
    public void ConvertCurrency_FR_FeminineVariant_ExistingCallerOnlyBehaviorUnchanged()
    {
        // Regression: a CurrencyDefinition with no ForcedVariants still relies entirely on the
        // caller-supplied variant for both fragments, exactly as before NTS-04.
        var fr = NumberToStringConverter.GetConverter("FR");
        var livre = new CurrencyDefinition
        {
            UnitSingular = "livre",
            UnitPlural = "livres",
            SubunitSingular = "sou",
            SubunitPlural = "sous",
            Connector = "et",
        };
        Assert.AreEqual("vingt et un livres", fr.ConvertCurrency(21m, livre));
        Assert.AreEqual("vingt et une livres", fr.ConvertCurrency(21m, livre, "gender=feminin"));
    }

    // ─── Fractions — a configured fraction term forces the numerator's variant ─────────────────

    [TestMethod]
    public void ConvertFraction_FR_SyntheticFractionTerm_ForcesFeminineNumerator()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var options = new NumberToStringConverterOptions(fr)
        {
            Fractions = new Dictionary<int, string> { [1] = "dixième(s)" },
            FractionForcedVariants = new Dictionary<int, ForcedVariantSet> { [1] = ForcedVariantSet.Create(("gender", "feminin")) },
        };
        var synthetic = new NumberToStringConverter(options);

        Assert.AreEqual("vingt et une dixièmes", synthetic.ConvertFraction(21, 10));
        // Non-regression: ordinary cardinal on the same converter stays masculine by default —
        // the forced variant is local to the fraction numerator, not global.
        Assert.AreEqual("vingt et un", synthetic.Convert(21));
    }

    // ─── Engine-level proof — synthetic converter, independent of French linguistic data ───────

    [TestMethod]
    public void Convert_Synthetic_ForcedDimensionDoesNotEraseUnrelatedCallerDimension()
    {
        // Two independent variant dimensions. The "hour" unit forces only "gender"; a caller-
        // supplied "case" value must remain visible to a rule that depends only on "case".
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
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
                    [new NumberToStringConverter.ReplacementRule("one", "one-DAT", ReplacementScope.Standalone)],
                    priority: 0),
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "feminine" },
                    [new NumberToStringConverter.ReplacementRule("one", "one-FEM", ReplacementScope.Standalone)],
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
        Assert.AreEqual("one-DAT unit", synthetic.Convert(new TimeSpan(1, 0, 0), "case=dative", "gender=masculine"));

        // Independently, an ordinary cardinal with an explicit gender=feminine (no case) exercises
        // the gender-only rule, proving the forced value on the constituent is a real, working
        // dimension value and not a name collision with the rule above.
        Assert.AreEqual("one-FEM", synthetic.Convert(1, "gender=feminine"));
    }

    [TestMethod]
    public void Convert_Synthetic_NoStateLeakAcrossDifferentlyConstrainedConstituents()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = new DefaultNumberToStringLanguageSpecifics(),
            VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "feminine" },
                    [new NumberToStringConverter.ReplacementRule("one", "one-FEM", ReplacementScope.Standalone)]),
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

        Assert.AreEqual("one-FEM token", synthetic.ConvertCurrency(1m, femToken));
        Assert.AreEqual("one token", synthetic.ConvertCurrency(1m, mascToken));
        Assert.AreEqual("one", synthetic.Convert(1));
    }

    // ─── Immutability / snapshot ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void TimeUnitForcedVariants_MutatingSourceDictionaryAfterConstruction_DoesNotAffectConverter()
    {
        var source = new Dictionary<string, ForcedVariantSet>
        {
            ["hour"] = ForcedVariantSet.Create(("gender", "feminin")),
        };
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            TimeUnitForcedVariants = source,
        };
        var converter = new NumberToStringConverter(options);

        source["hour"] = ForcedVariantSet.Empty;
        source["minute"] = ForcedVariantSet.Create(("gender", "feminin"));

        Assert.AreEqual("une heure", converter.Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── Dimension alias canonicalization ──────────────────────────────────────────────────────

    [TestMethod]
    public void TimeUnitForcedVariants_FR_LocalNameAlias_BehavesIdenticallyToCanonicalName()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // French declares <Dimension name="gender" localName="genre" ...>: "genre=feminin" must
        // canonicalize to "gender=feminin" and actually override the base query's canonical
        // "gender=masculin" default — not sit alongside it as an inert, differently-keyed entry.
        var options = new NumberToStringConverterOptions(fr)
        {
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
            {
                ["hour"] = ForcedVariantSet.Parse("genre=feminin"),
            },
        };
        var aliased = new NumberToStringConverter(options);

        Assert.AreEqual("une heure", aliased.Convert(new TimeSpan(1, 0, 0)));
        Assert.AreEqual("vingt et une heures", aliased.Convert(TimeSpan.FromHours(21)));
        // Non-regression: identical to forcing the canonical name directly.
        Assert.AreEqual(fr.Convert(TimeSpan.FromHours(21)), aliased.Convert(TimeSpan.FromHours(21)));
    }

    [TestMethod]
    public void TimeUnitForcedVariants_FR_CanonicalAndAliasForSameDimension_ThrowsDuplicateDimension()
    {
        // "gender" (canonical) and "genre" (its declared localName) both resolve to the same
        // declared VariantDimension: forcing both must fail deterministically rather than let one
        // value silently win.
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
            {
                ["hour"] = ForcedVariantSet.Parse("gender=feminin,genre=masculin"),
            },
        };

        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(() => new NumberToStringConverter(options));
        Assert.AreEqual("UNTS004", ex.ErrorCode);
    }

    [TestMethod]
    public void ConvertCurrency_FR_CanonicalAndAliasForSameDimension_ThrowsDuplicateDimension()
    {
        // Same duplicate-alias proof through the CurrencyDefinition validation path, which
        // canonicalizes per call rather than once at construction.
        var fr = NumberToStringConverter.GetConverter("FR");
        var invalid = new CurrencyDefinition
        {
            UnitSingular = "livre",
            UnitPlural = "livres",
            SubunitSingular = "sou",
            SubunitPlural = "sous",
            UnitForcedVariants = ForcedVariantSet.Parse("gender=feminin,genre=masculin"),
        };

        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(() => fr.ConvertCurrency(21m, invalid));
        Assert.AreEqual("UNTS004", ex.ErrorCode);
    }

    // ─── FromCulture round-trip ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void FromCulture_FR_PreservesTimeUnitForcedVariants()
    {
        var options = NumberToStringConverterOptions.FromCulture("FR");
        var rebuilt = new NumberToStringConverter(options);

        Assert.AreEqual("vingt et une heures", rebuilt.Convert(TimeSpan.FromHours(21)));
    }

    // ─── Caller validation is unaffected by ForcedVariants ─────────────────────────────────────

    [TestMethod]
    public void Convert_TimeSpan_FR_InvalidCallerVariant_StillThrowsBeforeForcedOverlay()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.ThrowsException<ArgumentException>(() => fr.Convert(new TimeSpan(1, 0, 0), "gender=banana"));
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
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => ForcedVariantSet.Create(("", "feminin")));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Create_EmptyValue_ThrowsMalformedSyntax()
    {
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => ForcedVariantSet.Create(("gender", "")));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Create_DuplicateRawDimension_ThrowsDuplicateConstraint()
    {
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
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
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(() => new NumberToStringConverter(options));
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
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(() => new NumberToStringConverter(options));
        Assert.AreEqual("UNTS006", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Parse_MissingValue_ThrowsMalformedSyntax()
    {
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(() => ForcedVariantSet.Parse("gender="));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Parse_MissingDimension_ThrowsMalformedSyntax()
    {
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(() => ForcedVariantSet.Parse("=feminin"));
        Assert.AreEqual("UNTS005", ex.ErrorCode);
    }

    [TestMethod]
    public void ForcedVariantSet_Parse_DuplicateDimension_ThrowsDuplicateConstraint()
    {
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => ForcedVariantSet.Parse("gender=feminin,gender=masculin"));
        Assert.AreEqual("UNTS004", ex.ErrorCode);
    }

    [TestMethod]
    public void TimeUnitForcedVariants_KeyWithoutMatchingTimeUnitsEntry_ThrowsArgumentException()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>
            {
                ["nonexistent"] = ForcedVariantSet.Create(("gender", "feminin")),
            },
        };
        Assert.ThrowsException<ArgumentException>(() => new NumberToStringConverter(options));
    }

    [TestMethod]
    public void ConvertCurrency_FR_InvalidUnitForcedVariant_ThrowsBeforeRenderingAnyFragment()
    {
        int finalizeCallCount = 0;
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            LanguageSpecifics = new CountingLanguageSpecifics(() => finalizeCallCount++),
        };
        var fr = new NumberToStringConverter(options);
        var invalid = new CurrencyDefinition
        {
            UnitSingular = "euro",
            UnitPlural = "euros",
            SubunitSingular = "centime",
            SubunitPlural = "centimes",
            UnitForcedVariants = ForcedVariantSet.Create(("gender", "banana")),
        };

        Assert.ThrowsException<NumberToStringConfigurationException>(() => fr.ConvertCurrency(21m, invalid));
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
}
