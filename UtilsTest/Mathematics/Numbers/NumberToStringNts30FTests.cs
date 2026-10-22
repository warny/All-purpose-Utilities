using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Text.RegularExpressions;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30F: Spanish and Basque read a milliard as a thousand millions, the multiplier of <c>millón</c> / <c>milioi</c>
/// being one cardinal of up to six digits (DPD "cardinales": <c>tres mil quinientos millones</c>; Euskaltzaindia rule
/// 203: <c>berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi</c>). Both configurations pair their scales
/// from the million (<see cref="NumberScale.PairedScalesFrom"/> = 2) instead of naming 10^9 <c>mil millones</c> /
/// <c>mila milioi</c>, which composed <c>dos mil millones quinientos millones</c>. The generic engine contract is
/// owned by <see cref="NumberToStringNts37Tests"/>; these tests pin the production configurations (sources in
/// <c>docs/NTS-08-linguistic-sources.md</c>, NTS-30F section).
/// </summary>
[TestClass]
public class NumberToStringNts30FTests
{
    /// <summary>The Spanish converter.</summary>
    private static NumberToStringConverter ES => NumberToStringConverter.GetConverter("ES");

    /// <summary>The Basque converter.</summary>
    private static NumberToStringConverter EU => NumberToStringConverter.GetConverter("EU");

    /// <summary>Counts the whole-word occurrences of <paramref name="word"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The word.</param>
    /// <returns>The number of occurrences.</returns>
    private static int CountWord(string text, string word) => Regex.Matches(text, $@"\b{Regex.Escape(word)}\b").Count;

    // ===== configuration ============================================================================

    /// <summary>ES and EU pair their scales from the million; the upper half of the pair has no name of its own.</summary>
    [TestMethod]
    public void BuiltIn_SpanishAndBasquePairFromTheMillion()
    {
        foreach (string culture in new[] { "ES", "EU", "eu-ES" })
        {
            var scale = NumberToStringConverter.GetConverter(culture).Scale;
            Assert.AreEqual(2, scale.PairedScalesFrom, culture);
            Assert.IsTrue(scale.CanNameGroup(2), culture);
            Assert.IsFalse(scale.CanNameGroup(3), culture);
            Assert.ThrowsExactly<InvalidOperationException>(() => scale.GetScaleName(3), culture);
        }
        Assert.AreEqual("mil", ES.Scale.GetScaleName(1));
        Assert.AreEqual("mila", EU.Scale.GetScaleName(1));
        Assert.AreEqual("milioi", EU.Scale.GetScaleName(2));
    }

    /// <summary>The public domain is unchanged: no billón / bilioi is added.</summary>
    [TestMethod]
    public void Domain_StopsBelowTheBillion()
    {
        foreach (var converter in new[] { ES, EU })
        {
            Assert.IsNotNull(converter.Convert(999_999_999_999L));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.Convert(1_000_000_000_000L));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.Convert(-1_000_000_000_000L));
        }
    }

    // ===== Spanish ==================================================================================

    /// <summary>
    /// DPD "cardinales" (<c>mil millones</c>), "millardo" (<c>dos mil millones</c>) and "números" §3.3c
    /// (<c>13 000 millones</c>): the whole multiplier precedes one <c>millones</c>.
    /// </summary>
    [DataTestMethod]
    [DataRow(1_000_000L, "un millón")]
    [DataRow(2_000_000L, "dos millones")]
    [DataRow(21_000_000L, "veintiún millones")]
    [DataRow(999_000_000L, "novecientos noventa y nueve millones")]
    [DataRow(1_000_000_000L, "mil millones")]
    [DataRow(1_001_000_000L, "mil un millones")]
    [DataRow(1_500_000_000L, "mil quinientos millones")]
    [DataRow(2_000_000_000L, "dos mil millones")]
    [DataRow(2_500_000_000L, "dos mil quinientos millones")]
    [DataRow(3_500_000_000L, "tres mil quinientos millones")]
    [DataRow(21_000_000_000L, "veintiún mil millones")]
    [DataRow(21_001_000_000L, "veintiún mil un millones")]
    [DataRow(31_001_000_000L, "treinta y un mil un millones")]
    [DataRow(101_000_000_000L, "ciento un mil millones")]
    [DataRow(999_999_000_000L, "novecientos noventa y nueve mil novecientos noventa y nueve millones")]
    [DataRow(999_999_999_999L, "novecientos noventa y nueve mil novecientos noventa y nueve millones novecientos noventa y nueve mil novecientos noventa y nueve")]
    public void Spanish_MillionMultiplier(long number, string expected)
        => Assert.AreEqual(expected, ES.Convert(number));

    /// <summary>
    /// DPD "uno" §2.2-2.3 and "cardinales" §4: <c>uno</c> and its compounds are apocopated before <c>mil</c> and before
    /// <c>millón/millones</c>; inside a paired multiplier both boundaries apply (<c>veintiún mil un millones</c>).
    /// </summary>
    [DataTestMethod]
    [DataRow(1L, "un millón")]
    [DataRow(21L, "veintiún millones")]
    [DataRow(31L, "treinta y un millones")]
    [DataRow(101L, "ciento un millones")]
    [DataRow(121L, "ciento veintiún millones")]
    [DataRow(1_001L, "mil un millones")]
    [DataRow(1_021L, "mil veintiún millones")]
    [DataRow(1_031L, "mil treinta y un millones")]
    [DataRow(1_101L, "mil ciento un millones")]
    [DataRow(21_000L, "veintiún mil millones")]
    [DataRow(21_001L, "veintiún mil un millones")]
    [DataRow(31_000L, "treinta y un mil millones")]
    [DataRow(121_121L, "ciento veintiún mil ciento veintiún millones")]
    public void Spanish_ApocopeInsideTheMultiplier(long multiplier, string expected)
        => Assert.AreEqual(expected, ES.Convert(multiplier * 1_000_000L));

    /// <summary>The standalone cardinals keep their full forms.</summary>
    [TestMethod]
    public void Spanish_StandaloneCardinalsUnchanged()
    {
        Assert.AreEqual("uno", ES.Convert(1));
        Assert.AreEqual("veintiuno", ES.Convert(21));
        Assert.AreEqual("treinta y uno", ES.Convert(31));
        Assert.AreEqual("mil uno", ES.Convert(1_001));
        Assert.AreEqual("veintiún mil veintiuno", ES.Convert(21_021));
    }

    /// <summary>Every multiplier of a million is one phrase followed by exactly one scale noun.</summary>
    [TestMethod]
    public void Spanish_OneScaleNounPerMillionMultiplier()
    {
        foreach (long multiplier in new long[] { 1_000, 1_001, 1_500, 2_500, 21_001, 100_000, 123_456, 999_999 })
        {
            string text = ES.Convert(multiplier * 1_000_000L);
            Assert.AreEqual(1, CountWord(text, "millones") + CountWord(text, "millón"), text);
            StringAssert.EndsWith(text, " millones");
            Assert.IsFalse(text.Contains("mil millones ", StringComparison.Ordinal), text);
        }
    }

    /// <summary>Lower groups follow the million phrase as before.</summary>
    [DataTestMethod]
    [DataRow(2_500_001_000L, "dos mil quinientos millones mil")]
    [DataRow(2_500_000_001L, "dos mil quinientos millones uno")]
    [DataRow(2_500_001_001L, "dos mil quinientos millones mil uno")]
    [DataRow(1_200_065_785L, "mil doscientos millones sesenta y cinco mil setecientos ochenta y cinco")]
    [DataRow(21_001_021_021L, "veintiún mil un millones veintiún mil veintiuno")]
    public void Spanish_LowerGroups(long number, string expected)
        => Assert.AreEqual(expected, ES.Convert(number));

    // ===== Basque ===================================================================================

    /// <summary>
    /// Euskaltzaindia rule 7 (milioi bat, hamar milioi, mila milioi) and rule 203 §4 (long scale; a six-digit
    /// multiplier with its own <c>eta</c>); <c>mila eta bostehun</c> / <c>mila bostehun eta bat</c> follow the
    /// connector rule of both (1200 mila eta berrehun, 1201 mila berrehun eta bat).
    /// </summary>
    [DataTestMethod]
    [DataRow(1_000_000L, "milioi bat")]
    [DataRow(2_000_000L, "bi milioi")]
    [DataRow(10_000_000L, "hamar milioi")]
    [DataRow(999_000_000L, "bederatziehun eta laurogeita hemeretzi milioi")]
    [DataRow(1_000_000_000L, "mila milioi")]
    [DataRow(1_001_000_000L, "mila eta bat milioi")]
    [DataRow(1_500_000_000L, "mila eta bostehun milioi")]
    [DataRow(2_000_000_000L, "bi mila milioi")]
    [DataRow(2_001_000_000L, "bi mila eta bat milioi")]
    [DataRow(2_500_000_000L, "bi mila eta bostehun milioi")]
    [DataRow(2_501_000_000L, "bi mila bostehun eta bat milioi")]
    [DataRow(2_521_000_000L, "bi mila bostehun eta hogeita bat milioi")]
    [DataRow(3_500_000_000L, "hiru mila eta bostehun milioi")]
    [DataRow(206_795_000_000L, "berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi")]
    [DataRow(999_999_000_000L, "bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzi milioi")]
    [DataRow(999_999_999_999L, "bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzi milioi bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzi")]
    public void Basque_MillionMultiplier(long number, string expected)
    {
        Assert.AreEqual(expected, EU.Convert(number));
        Assert.AreEqual(expected, NumberToStringConverter.GetConverter("eu-ES").Convert(number));
    }

    /// <summary>
    /// The inversion <c>milioi bat</c> applies to exactly one million (onValue 1): a multiplier ending in one keeps
    /// the numeral before the noun, and no <c>eta ... eta</c> artifact appears inside a multiplier.
    /// </summary>
    [TestMethod]
    public void Basque_MilioiBatOnlyForOneMillion()
    {
        foreach (long multiplier in new long[] { 1_001, 2_001, 21_001, 101_001, 2_501, 2_521 })
        {
            string text = EU.Convert(multiplier * 1_000_000L);
            StringAssert.EndsWith(text, " bat milioi");
            Assert.IsFalse(text.Contains("milioi bat", StringComparison.Ordinal), text);
            Assert.AreEqual(1, CountWord(text, "milioi"), text);
        }
        foreach (long multiplier in new long[] { 1_101, 1_201, 2_501, 2_521, 121_121 })
            Assert.IsFalse(Regex.IsMatch(EU.Convert(multiplier * 1_000_000L), @"\beta \w*ehun eta\b"), multiplier.ToString());
    }

    /// <summary>
    /// The lower groups after a paired milioi: rule 203 attests <c>... milioi zortziehun eta berrogeita hiru mila</c>;
    /// a single final component takes <c>eta</c> (rule 203 <c>bi milioi eta zazpiehun mila</c>, <c>milioi bat eta bat</c>).
    /// </summary>
    [DataTestMethod]
    [DataRow(206_795_843_000L, "berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi zortziehun eta berrogeita hiru mila")]
    [DataRow(2_500_001_000L, "bi mila eta bostehun milioi eta mila")]
    [DataRow(2_500_000_001L, "bi mila eta bostehun milioi eta bat")]
    [DataRow(2_500_700_000L, "bi mila eta bostehun milioi eta zazpiehun mila")]
    public void Basque_LowerGroups(long number, string expected)
        => Assert.AreEqual(expected, EU.Convert(number));

    // ===== composite surfaces and unchanged outputs =================================================

    /// <summary>The composite surfaces reuse the corrected cardinal; none of them is widened.</summary>
    [TestMethod]
    public void CompositeSurfaces_UseTheCorrectedCardinal()
    {
        Assert.AreEqual("menos dos mil quinientos millones", ES.Convert(-2_500_000_000L));
        Assert.AreEqual("ken bi mila eta bostehun milioi", EU.Convert(-2_500_000_000L));
        Assert.AreEqual("dos mil quinientos millones coma cinco", ES.Convert(2_500_000_000.5m));
        Assert.AreEqual("mila eta bat milioi koma bi bost", EU.Convert(1_001_000_000.25m));
        Assert.AreEqual("uno sobre mil un millones", ES.ConvertFraction(1, 1_001_000_000));
        Assert.AreEqual("bi mila eta bostehun milioi zati hiru", EU.ConvertFraction(2_500_000_000, 3));
        Assert.AreEqual("veintiún mil un millones sobre siete", ES.Convert(new Utils.Numerics.Number(21_001_000_000, 7)));
        Assert.AreEqual("bat zati mila eta bat milioi", EU.Convert(new Utils.Numerics.Number(1, 1_001_000_000)));

        // The currency wording is caller-defined (the ES attributive apocope, NTS-30, does not touch this amount): only the cardinal is pinned.
        var euro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "céntimo", SubunitPlural = "céntimos", Connector = "con" };
        StringAssert.StartsWith(ES.ConvertCurrency(2_500_000_000.50m, euro), "dos mil quinientos millones euros");
        StringAssert.StartsWith(EU.ConvertCurrency(2_500_000_000.50m, euro), "bi mila eta bostehun milioi euros");
    }

    /// <summary>
    /// The supported ordinals are identical. NTS-30F kept the milliard closed; NTS-38 opened it on its own sources
    /// (<c>milmillonésimo</c>, <c>mila milioigarren</c>, see <c>NumberToStringNts30Nts38Tests</c>): only the Spanish
    /// multiplier written in several words stays closed.
    /// </summary>
    [TestMethod]
    public void Ordinals_Unchanged()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => ES.ConvertOrdinal(2_500_000_000L));
        Assert.AreEqual("millonésimo", ES.ConvertOrdinal(1_000_000));
        Assert.AreEqual("dosmillonésimo", ES.ConvertOrdinal(2_000_000));
        Assert.AreEqual("bi milioigarren", EU.ConvertOrdinal(2_000_000));
        Assert.AreEqual("mila eta batgarren", EU.ConvertOrdinal(1_001));
    }

    /// <summary>The clock does not use the scales and is unchanged.</summary>
    [TestMethod]
    public void Clock_Unchanged()
    {
        Assert.AreEqual("las dos y media", ES.ConvertClockTime(new TimeOnly(14, 30)));
        Assert.AreEqual("ordu biak eta laurden", EU.ConvertClockTime(new TimeOnly(2, 15)));
    }
}
