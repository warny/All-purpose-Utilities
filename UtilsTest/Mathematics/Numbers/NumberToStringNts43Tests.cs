using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-43: the caller's <c>AdjustFunction</c> runs after the cardinal morphology. A cardinal and a cardinal fragment
/// now run <c>ConvertRaw</c> → whole-number variant rules → <c>AdjustFunction</c> → <c>TriggerAt.End</c> (then, for a
/// top-level value only, the language finalization and the sign), the order the ordinal and multiplicative pipelines
/// already followed. Before, the adjustment ran first and hid the input of the textual variant replacements: with an
/// uppercasing adjustment, the UK feminine <c>два → дві</c> gave <c>ДВА</c>.
/// </summary>
[TestClass]
public class NumberToStringNts43Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Clones a converter with an uppercasing <c>AdjustFunction</c>.</summary>
    /// <param name="source">The converter to clone.</param>
    /// <returns>The uppercasing clone.</returns>
    private static NumberToStringConverter Upper(NumberToStringConverter source)
        => new(new NumberToStringConverterOptions(source) { AdjustFunction = text => text.ToUpperInvariant() });

    /// <summary>Clones a built-in converter with an uppercasing <c>AdjustFunction</c>.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The uppercasing clone.</returns>
    private static NumberToStringConverter Upper(string language) => Upper(Get(language));

    // ===== The revealing cases ======================================================================

    /// <summary>
    /// The feminine numeral two is a textual variant replacement in Ukrainian, Czech and Slovak: it is formed first,
    /// then uppercased, through <c>Convert</c>.
    /// </summary>
    [TestMethod]
    [DataRow("UK", "gender=жіночий", "дві", "ДВІ")]
    [DataRow("CS", "gender=ženský", "dvě", "DVĚ")]
    [DataRow("SK", "gender=ženský", "dve", "DVE")]
    public void FeminineTwo_IsInflectedThenAdjusted(string culture, string variant, string plain, string adjusted)
    {
        Assert.AreEqual(plain, Get(culture).Convert(new BigInteger(2), variant));
        Assert.AreEqual(adjusted, Upper(culture).Convert(new BigInteger(2), variant));
    }

    /// <summary>
    /// The fraction that exposed the defect: the numerator is a cardinal fragment whose feminine form comes from the
    /// numerator forced variants of the fraction plugin (<c>IFractionLanguageSpecifics.NumeratorForcedVariants</c>),
    /// with or without a caller gender. The denominator is not a numeric fragment and is not adjusted.
    /// </summary>
    [TestMethod]
    [DataRow("UK", "gender=жіночий", "дві третіх", "ДВІ третіх")]
    [DataRow("UK", "gender=чоловічий", "дві третіх", "ДВІ третіх")]
    [DataRow("CS", "gender=ženský", "dvě třetiny", "DVĚ třetiny")]
    [DataRow("CS", "gender=mužský", "dvě třetiny", "DVĚ třetiny")]
    [DataRow("SK", "gender=ženský", "dve tretiny", "DVE tretiny")]
    [DataRow("SK", "gender=mužský", "dve tretiny", "DVE tretiny")]
    public void FractionTwoThirds_NumeratorIsInflectedThenAdjusted(string culture, string variant, string plain, string adjusted)
    {
        Assert.AreEqual(plain, Get(culture).ConvertFraction(2, 3, variant));
        Assert.AreEqual(adjusted, Upper(culture).ConvertFraction(2, 3, variant));
        Assert.AreEqual(adjusted, Upper(culture).ConvertFraction(2, 3));
    }

    /// <summary>
    /// The oblique numerator a plugin spells (NTS-40 <c>FractionDenominatorReading.NumeratorText</c>) is already
    /// declined: only the adjustment applies to it.
    /// </summary>
    [TestMethod]
    [DataRow("UK", "case=родовий", "двох третіх", "ДВОХ третіх")]
    [DataRow("CS", "case=genitiv", "dvou třetin", "DVOU třetin")]
    [DataRow("SK", "case=genitív", "dvoch tretín", "DVOCH tretín")]
    public void ObliqueTwoThirds_PluginNumeratorIsAdjusted(string culture, string variant, string plain, string adjusted)
    {
        Assert.AreEqual(plain, Get(culture).ConvertFraction(2, 3, variant));
        Assert.AreEqual(adjusted, Upper(culture).ConvertFraction(2, 3, variant));
    }

    // ===== Generic morphology =======================================================================

    /// <summary>
    /// Gender and case morphology of other languages, each a textual variant replacement the adjustment used to hide.
    /// </summary>
    [TestMethod]
    [DataRow("FR", 1L, "gender=feminin", "UNE")]
    [DataRow("FR", 21L, "gender=feminin", "VINGT-ET-UNE")]
    [DataRow("ES", 21L, "gender=femenino", "VEINTIUNA")]
    [DataRow("ES", 200L, "gender=femenino", "DOSCIENTAS")]
    [DataRow("ES", 21L, "form=attributive", "VEINTIÚN")]
    [DataRow("DE", 1L, "gender=feminin", "EINE")]
    [DataRow("EL", 1L, "gender=θηλυκό", "ΜΊΑ")]
    [DataRow("EL", 3L, "gender=θηλυκό", "ΤΡΕΙΣ")]
    [DataRow("BG", 2L, "gender=feminine", "ДВЕ")]
    [DataRow("FI", 2L, "case=partitiivi", "KAHTA")]
    [DataRow("FI", 2000L, "case=partitiivi", "KAHTATUHATTA")]
    public void BuiltInMorphology_IsInflectedThenAdjusted(string culture, long value, string variant, string expected)
        => Assert.AreEqual(expected, Upper(culture).Convert(new BigInteger(value), variant));

    /// <summary>
    /// For the languages whose language finalization and end triggers do not depend on case, an uppercasing
    /// adjustment now gives exactly the uppercased plain cardinal and multiplicative, for every variant combination.
    /// (German finalization turns a final <c>ein</c> into <c>eins</c> and a Swedish end trigger joins <c>tusen</c>:
    /// both run after the adjustment by contract, and are not part of this matrix.)
    /// </summary>
    [TestMethod]
    [DataRow("UK")]
    [DataRow("CS")]
    [DataRow("SK")]
    [DataRow("BG")]
    [DataRow("FR")]
    [DataRow("ES")]
    [DataRow("FI")]
    [DataRow("EL")]
    public void UppercaseAdjustment_MatchesUppercasedPlainOutput(string culture)
    {
        NumberToStringConverter plain = Get(culture);
        NumberToStringConverter upper = Upper(plain);
        BigInteger[] values = [.. Enumerable.Range(0, 131).Select(i => (BigInteger)i),
            200, 1000, 2000, 21000, 1000000, 2000000, 21000000, 500000000, 2000000000,
            2 * BigInteger.Pow(10, 12), 2 * BigInteger.Pow(10, 15)];
        foreach (string[] variants in Combinations(plain.VariantDimensions))
        {
            foreach (BigInteger value in values.Where(v => plain.MaxNumber is not { } max || v <= max))
                Assert.AreEqual(plain.Convert(value, variants).ToUpperInvariant(), upper.Convert(value, variants),
                    $"{culture} {value} [{string.Join(",", variants)}]");
            if (plain.SupportsMultiplicative)
                foreach (int value in new[] { 1, 2, 3, 21, 100 })
                    Assert.AreEqual(plain.ConvertMultiplicative(value, variants).ToUpperInvariant(), upper.ConvertMultiplicative(value, variants),
                        $"{culture} ×{value} [{string.Join(",", variants)}]");
        }
    }

    // ===== Scale forced variants (shielding) ========================================================

    /// <summary>
    /// A scale form forcing a variant on its multiplier keeps its rendering against a different caller variant: the
    /// multiplier is rendered under the forced variant, the caller's whole-number variant pass leaves it shielded,
    /// then the adjustment sees the assembled, correctly inflected cardinal. Exact scale forms: ES <c>millones</c>
    /// forces the masculine (NTS-30G), CS <c>tisíc</c> the masculine; generated scale form: CS <c>biliarda</c>
    /// forces the feminine (NTS-30C).
    /// </summary>
    [TestMethod]
    [DataRow("ES", "500000000", "gender=femenino", "quinientos millones", "QUINIENTOS MILLONES")]
    [DataRow("CS", "2000", "gender=ženský", "dva tisíce", "DVA TISÍCE")]
    [DataRow("CS", "2000000000000000", "gender=mužský", "dvě biliardy", "DVĚ BILIARDY")]
    public void ScaleForcedVariants_AreShieldedFromCallerVariantsAndAdjusted(string culture, string value, string variant, string plain, string adjusted)
    {
        BigInteger number = BigInteger.Parse(value);
        Assert.AreEqual(plain, Get(culture).Convert(number, variant));
        Assert.AreEqual(adjusted, Upper(culture).Convert(number, variant));
    }

    /// <summary>
    /// The adjustment still sees a shielded group as ordinary text, and the end triggers keep their established
    /// shielding: while the adjusted text still contains the forced group, a synthetic end trigger matching its
    /// multiplier leaves it untouched and rewrites an unforced occurrence; an adjustment that rewrites the group itself
    /// (here, uppercasing) makes it unlocatable, so the end triggers process it as ordinary text, as documented on
    /// <c>ApplyShielded</c> before NTS-43.
    /// </summary>
    [TestMethod]
    public void ScaleForcedVariants_EndTriggerSeesAdjustedTextWithEstablishedShielding()
    {
        NumberToStringConverter source = Get("ES");
        NumberToStringConverter With(Func<string, string> adjust, string from) => new(new NumberToStringConverterOptions(source)
        {
            AdjustFunction = adjust,
            Triggers = [.. source.Triggers, new NumberToStringConverter.TriggerRule(
                NumberToStringConverter.TriggerAt.End, null,
                [new NumberToStringConverter.TriggerReplace(from, false, [], "TRIGGERED")])],
        });

        NumberToStringConverter wrapping = With(text => "[" + text + "]", "quinientos");
        Assert.AreEqual("[quinientos millones]", wrapping.Convert(new BigInteger(500000000), "gender=femenino"));
        Assert.AreEqual("[TRIGGERED]", wrapping.Convert(new BigInteger(500), "gender=masculino"));

        NumberToStringConverter uppercasing = With(text => text.ToUpperInvariant(), "QUINIENTOS");
        Assert.AreEqual("TRIGGERED MILLONES", uppercasing.Convert(new BigInteger(500000000), "gender=femenino"));
    }

    // ===== Composite surfaces and constituent forced variants =======================================

    /// <summary>
    /// The time units force the feminine on their numeral (FR <c>heure</c>, <c>minute</c>, <c>seconde</c>: NTS-04
    /// <c>TimeUnitForcedVariants</c>), for a duration and for a clock time: forced morphology first, then adjustment.
    /// </summary>
    [TestMethod]
    public void TimeUnitForcedVariants_AreInflectedThenAdjusted()
    {
        NumberToStringConverter converter = Upper("FR");
        Assert.AreEqual("UNE heure UNE minute DEUX secondes", converter.Convert(new TimeSpan(1, 1, 2)));
        Assert.AreEqual("DEUX heures VINGT-ET-UNE minutes UNE seconde", converter.Convert(new TimeSpan(2, 21, 1)));
        Assert.AreEqual("UNE heure", converter.Convert(new TimeOnly(1, 0)));
        Assert.AreEqual("DEUX heures VINGT-ET-UNE minutes", converter.Convert(new TimeOnly(2, 21)));
        // Spanish hora is feminine too, its minuto masculine (apocope before the noun).
        Assert.AreEqual("UNA hora UN minuto DOS segundos", Upper("ES").Convert(new TimeSpan(1, 1, 2)));
    }

    /// <summary>
    /// A currency subunit forcing a variant (<c>SubunitForcedVariants</c>) inflects its numeral before the adjustment;
    /// the unforced unit keeps the caller's default.
    /// </summary>
    [TestMethod]
    public void CurrencySubunitForcedVariants_AreInflectedThenAdjusted()
    {
        var currency = new CurrencyDefinition
        {
            UnitSingular = "unit",
            UnitPlural = "units",
            SubunitSingular = "cent",
            SubunitPlural = "cents",
            SubunitDigits = 2,
            Connector = "and",
            SubunitForcedVariants = ForcedVariantSet.Create(("gender", "feminin")),
        };

        Assert.AreEqual("vingt-et-un units and vingt-et-une cents", Get("FR").ConvertCurrency(21.21m, currency));
        Assert.AreEqual("VINGT-ET-UN units and VINGT-ET-UNE cents", Upper("FR").ConvertCurrency(21.21m, currency));
    }

    /// <summary>
    /// A configured fraction forcing a variant on its numerator (<c>FractionForcedVariants</c>) inflects the numerator
    /// before the adjustment, through <c>ConvertFraction</c> and through the language-default decimal reading.
    /// </summary>
    [TestMethod]
    public void FractionForcedVariants_AreInflectedThenAdjusted()
    {
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(Get("FR"))
        {
            AdjustFunction = text => text.ToUpperInvariant(),
            FractionForcedVariants = new Dictionary<int, ForcedVariantSet> { [3] = ForcedVariantSet.Create(("gender", "feminin")) },
        });

        StringAssert.StartsWith(converter.ConvertFraction(21, 1000), "VINGT-ET-UNE ");
        StringAssert.EndsWith(converter.Convert(21.021m), "VINGT-ET-UNE MILLIÈMES");
        StringAssert.StartsWith(converter.Convert(21.021m), "VINGT-ET-UN ");
    }

    /// <summary>
    /// The decimal readings reuse the cardinal fragment: the inflected integer part and digits are adjusted.
    /// </summary>
    [TestMethod]
    public void Decimals_AreInflectedThenAdjusted()
    {
        Assert.AreEqual("ДВІ КОМА П'ЯТЬ", Upper("UK").Convert(2.5m, -1, new DecimalFormatOptions { ReadingMode = DecimalReadingMode.DigitByDigit }, "gender=жіночий"));
        Assert.AreEqual("DVĚ CELÁ PĚT", Upper("CS").Convert(2.5m, -1, new DecimalFormatOptions { ReadingMode = DecimalReadingMode.DigitByDigit }, "gender=ženský"));
        Assert.AreEqual("DVE CELÁ PÄŤ", Upper("SK").Convert(2.5m, -1, new DecimalFormatOptions { ReadingMode = DecimalReadingMode.DigitByDigit }, "gender=ženský"));
        Assert.AreEqual("VEINTIÚN COMA CERO CINCO", Upper("ES").Convert(21.05m, "form=attributive"));
        var digits = new DecimalFormatOptions { ReadingMode = DecimalReadingMode.DigitByDigit };
        Assert.AreEqual("VEINTIÚN COMA CERO CINCO", Upper("ES").Convert(21.05m, -1, digits, "form=attributive"));
    }

    /// <summary>The minus template stays outside the inflected, adjusted magnitude.</summary>
    [TestMethod]
    public void NegativeValues_KeepTheMinusTemplateUnadjusted()
    {
        Assert.AreEqual("мінус ДВІ", Upper("UK").Convert(new BigInteger(-2), "gender=жіночий"));
        Assert.AreEqual("мінус ДВІ третіх", Upper("UK").ConvertFraction(-2, 3, "gender=жіночий"));
        Assert.AreEqual("minus DVĚ", Upper("CS").Convert(new BigInteger(-2), "gender=ženský"));
        Assert.AreEqual("mínus DVE tretiny", Upper("SK").ConvertFraction(-2, 3));
    }

    // ===== Ordinals ==================================================================================

    /// <summary>
    /// The ordinal pipeline was already right and is unchanged: the word rule forms the ordinal on the plain cardinal,
    /// then the adjustment runs (never <c>TWENTY-ONEth</c>).
    /// </summary>
    [TestMethod]
    public void Ordinal_WordRuleRunsBeforeAdjustFunction()
    {
        Assert.AreEqual("twenty-first", Get("EN").ConvertOrdinal(21));
        Assert.AreEqual("TWENTY-FIRST", Upper("EN").ConvertOrdinal(21));
    }

    /// <summary>Enumerates every combination of the declared variant values.</summary>
    /// <param name="dimensions">The variant dimensions.</param>
    /// <returns>The combinations, as <c>dimension=value</c> arrays.</returns>
    private static IEnumerable<string[]> Combinations(IReadOnlyList<NumberToStringConverter.VariantDimension> dimensions)
    {
        IEnumerable<string[]> combinations = [[]];
        foreach (var dimension in dimensions)
        {
            var current = dimension;
            combinations = combinations.SelectMany(c => current.Values.Select(v => c.Append($"{current.Name}={v}").ToArray())).ToList();
        }
        return combinations;
    }
}
