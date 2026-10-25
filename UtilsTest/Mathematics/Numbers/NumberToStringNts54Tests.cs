using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-54, the Romanian scale multiplier agrees with its scale noun:
/// <list type="bullet">
/// <item><description>
/// <c>mie</c> / <c>mii</c> is feminine (DEX '09 "mie": num. card., s. f.): the whole multiplier takes the feminine forms
/// on its last constituent, <c>două mii</c> (DOOM 3, Studiu introductiv §4.1.5), <c>douăsprezece mii de lei</c> (DOOM 2,
/// Normele §6.4), <c>douăzeci și două de mii</c> (DOOM 3 "douăzeci și doi": num. m., f. douăzeci și două), and
/// <c>douăzeci și una de mii</c>, <c>o sută una mii</c>: a final <c>unu</c> agrees even after a hundred (Institute of
/// Linguistics of the Romanian Academy, CNA monitoring reports 2010: <c>o sută şaizeci şi una de mii de lei</c>,
/// <c>şase sute treizeci şi una de mii de oameni</c>, <c>o sută una zile</c>), the form the Romanian ordinals already
/// write (<c>al douăzeci și una de mii unulea</c>).
/// </description></item>
/// <item><description>
/// <c>milion</c> and <c>miliard</c> are neuter (DOOM 3 "milion": s. n., pl. milioane): a neuter plural counts with the
/// feminine <c>două</c> (DOOM 3 "două puncte": num. + s. n. pl.; GALR 2005: <c>două milioane trei sute opt lei</c>) but
/// keeps <c>unu</c> (<c>douăzeci și unu de milioane</c>): the new <c>gen=neutru</c> value, forced on the multiplier.
/// </description></item>
/// </list>
/// The NTS-53 connector (<c>de</c> from 20 except after a multiplier ending on 1–19) is decided on the number and is
/// independent of the form. Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-54 section.
/// </summary>
[TestClass]
public class NumberToStringNts54Tests
{
    /// <summary>Gets the built-in Romanian converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language = "RO") => NumberToStringConverter.GetConverter(language);

    /// <summary>Builds a caller-defined currency with the masculine/neuter <c>leu</c>.</summary>
    /// <returns>The currency.</returns>
    private static CurrencyDefinition Lei() => new()
    {
        UnitSingular = "leu",
        UnitPlural = "lei",
        SubunitSingular = "ban",
        SubunitPlural = "bani",
        Connector = "și",
    };

    /// <summary>The multiplier of <c>mii</c> is feminine on its last constituent, with or without <c>de</c>.</summary>
    [TestMethod]
    [DataRow(1_000L, "o mie")]
    [DataRow(2_000L, "două mii")]
    [DataRow(12_000L, "douăsprezece mii")]
    [DataRow(19_000L, "nouăsprezece mii")]
    [DataRow(20_000L, "douăzeci de mii")]
    [DataRow(21_000L, "douăzeci și una de mii")]
    [DataRow(22_000L, "douăzeci și două de mii")]
    [DataRow(32_000L, "treizeci și două de mii")]
    [DataRow(101_000L, "o sută una mii")]
    [DataRow(102_000L, "o sută două mii")]
    [DataRow(112_000L, "o sută douăsprezece mii")]
    [DataRow(119_000L, "o sută nouăsprezece mii")]
    [DataRow(120_000L, "o sută douăzeci de mii")]
    [DataRow(121_000L, "o sută douăzeci și una de mii")]
    [DataRow(122_000L, "o sută douăzeci și două de mii")]
    [DataRow(132_000L, "o sută treizeci și două de mii")]
    [DataRow(161_000L, "o sută șaizeci și una de mii")]
    [DataRow(202_000L, "două sute două mii")]
    [DataRow(631_000L, "șase sute treizeci și una de mii")]
    [DataRow(212_000L, "două sute douăsprezece mii")]
    public void Thousands_FeminineMultiplier(long number, string expected) => Assert.AreEqual(expected, Get().Convert(number));

    /// <summary>
    /// The multiplier of the neuter <c>milioane</c> / <c>miliarde</c> takes <c>două</c> but keeps <c>unu</c>, with or
    /// without <c>de</c>; one alone is <c>un milion</c>, <c>un miliard</c>.
    /// </summary>
    [TestMethod]
    [DataRow(1_000_000L, "un milion")]
    [DataRow(2_000_000L, "două milioane")]
    [DataRow(12_000_000L, "douăsprezece milioane")]
    [DataRow(21_000_000L, "douăzeci și unu de milioane")]
    [DataRow(22_000_000L, "douăzeci și două de milioane")]
    [DataRow(32_000_000L, "treizeci și două de milioane")]
    [DataRow(101_000_000L, "o sută unu milioane")]
    [DataRow(102_000_000L, "o sută două milioane")]
    [DataRow(112_000_000L, "o sută douăsprezece milioane")]
    [DataRow(119_000_000L, "o sută nouăsprezece milioane")]
    [DataRow(120_000_000L, "o sută douăzeci de milioane")]
    [DataRow(121_000_000L, "o sută douăzeci și unu de milioane")]
    [DataRow(122_000_000L, "o sută douăzeci și două de milioane")]
    [DataRow(1_000_000_000L, "un miliard")]
    [DataRow(2_000_000_000L, "două miliarde")]
    [DataRow(12_000_000_000L, "douăsprezece miliarde")]
    [DataRow(21_000_000_000L, "douăzeci și unu de miliarde")]
    [DataRow(22_000_000_000L, "douăzeci și două de miliarde")]
    [DataRow(32_000_000_000L, "treizeci și două de miliarde")]
    [DataRow(102_000_000_000L, "o sută două miliarde")]
    [DataRow(112_000_000_000L, "o sută douăsprezece miliarde")]
    [DataRow(122_000_000_000L, "o sută douăzeci și două de miliarde")]
    public void MillionsAndMilliards_NeuterMultiplier(long number, string expected) => Assert.AreEqual(expected, Get().Convert(number));

    /// <summary>Each group agrees with its own noun; the units group keeps the citation masculine.</summary>
    [TestMethod]
    [DataRow(2_002L, "două mii doi")]
    [DataRow(22_022L, "douăzeci și două de mii douăzeci și doi")]
    [DataRow(2_002_002_002L, "două miliarde două milioane două mii doi")]
    [DataRow(22_022_022_022L, "douăzeci și două de miliarde douăzeci și două de milioane douăzeci și două de mii douăzeci și doi")]
    [DataRow(21_021_021_021L, "douăzeci și unu de miliarde douăzeci și unu de milioane douăzeci și una de mii douăzeci și unu")]
    [DataRow(12_012_012_012L, "douăsprezece miliarde douăsprezece milioane douăsprezece mii doisprezece")]
    [DataRow(1_001_001_001L, "un miliard un milion o mie unu")]
    public void Compositions_EachGroupOnItsNoun(long number, string expected) => Assert.AreEqual(expected, Get().Convert(number));

    /// <summary>The forced multiplier is shielded from the caller's gender: only the lower groups follow it.</summary>
    [TestMethod]
    [DataRow("gen=feminin", 21_000_000L, "douăzeci și unu de milioane")]
    [DataRow("gen=feminin", 22_000_001L, "douăzeci și două de milioane una")]
    [DataRow("gen=feminin", 2_002L, "două mii două")]
    [DataRow("gen=masculin", 22_000L, "douăzeci și două de mii")]
    [DataRow("gen=masculin", 21_000_000L, "douăzeci și unu de milioane")]
    [DataRow("gen=neutru", 21_002L, "douăzeci și una de mii două")]
    [DataRow("gen=neutru", 2_021L, "două mii douăzeci și unu")]
    public void CallerGender_DoesNotReachTheForcedMultiplier(string variant, long number, string expected)
        => Assert.AreEqual(expected, Get().Convert(number, variant));

    /// <summary>
    /// <c>gen=neutru</c> is the neuter agreement of a counted noun: masculine one (<c>unu</c>), feminine two
    /// (<c>două</c>, <c>douăsprezece</c>, <c>douăzeci și două</c>); the default stays masculine.
    /// </summary>
    [TestMethod]
    [DataRow(1L, "unu")]
    [DataRow(2L, "două")]
    [DataRow(12L, "douăsprezece")]
    [DataRow(21L, "douăzeci și unu")]
    [DataRow(22L, "douăzeci și două")]
    [DataRow(102L, "o sută două")]
    public void Neuter_Standalone(long number, string expected) => Assert.AreEqual(expected, Get().Convert(number, "gen=neutru"));

    /// <summary>The standalone cardinals and the declared dimension.</summary>
    [TestMethod]
    public void Cardinals_And_Dimension()
    {
        var converter = Get();
        Assert.AreEqual("unu", converter.Convert(1));
        Assert.AreEqual("doi", converter.Convert(2));
        Assert.AreEqual("doisprezece", converter.Convert(12));
        Assert.AreEqual("una", converter.Convert(1, "gen=feminin"));
        Assert.AreEqual("douăzeci și una", converter.Convert(21, "gen=feminin"));
        var gender = converter.VariantDimensions.Single(d => d.Name == "gen");
        CollectionAssert.AreEqual(new[] { "masculin", "feminin", "neutru" }, gender.Values.ToArray());
    }

    /// <summary>RO-RO is the same converter.</summary>
    [TestMethod]
    public void RomanianRomania_SameBehaviour()
    {
        Assert.AreEqual("douăsprezece mii", Get("RO-RO").Convert(12_000));
        Assert.AreEqual("douăzeci și două de milioane", Get("RO-RO").Convert(22_000_000));
    }

    /// <summary>The NTS-53 connector is unchanged and independent of the form.</summary>
    [TestMethod]
    [DataRow(119_000L, "o sută nouăsprezece mii")]
    [DataRow(120_000L, "o sută douăzeci de mii")]
    [DataRow(19_000_000L, "nouăsprezece milioane")]
    [DataRow(20_000_000L, "douăzeci de milioane")]
    [DataRow(212_000_000L, "două sute douăsprezece milioane")]
    [DataRow(222_000_000L, "două sute douăzeci și două de milioane")]
    public void ScaleConnector_Unchanged(long number, string expected) => Assert.AreEqual(expected, Get().Convert(number));

    /// <summary>The currency numeral carries the corrected multiplier; <c>un</c> / <c>o</c> (NTS-53) are kept.</summary>
    [TestMethod]
    [DataRow("1", "un leu")]
    [DataRow("21", "douăzeci și unu de lei")]
    [DataRow("101", "o sută unu lei")]
    [DataRow("2000", "două mii de lei")]
    [DataRow("12000", "douăsprezece mii de lei")]
    [DataRow("22000", "douăzeci și două de mii de lei")]
    [DataRow("102000", "o sută două mii de lei")]
    [DataRow("2000000", "două milioane de lei")]
    [DataRow("22000000", "douăzeci și două de milioane de lei")]
    public void Currency_CarriesTheAgreement(string amount, string expected)
        => Assert.AreEqual(expected, Get().ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Lei()));

    /// <summary>The ordinals and the clock are unchanged (the ordinal plugin already wrote a feminine multiplier).</summary>
    [TestMethod]
    public void Ordinals_And_Clock_Unchanged()
    {
        var converter = Get();
        Assert.AreEqual("al doilea", converter.ConvertOrdinal(2));
        Assert.AreEqual("a doua", converter.ConvertOrdinal(2, "gen=feminin"));
        Assert.AreEqual("al douăzeci și una de mii unulea", converter.ConvertOrdinal(21_001));
        Assert.AreEqual("al douăsprezece mii doilea", converter.ConvertOrdinal(12_002));
        Assert.AreEqual("al doilea", converter.ConvertOrdinal(2, "gen=neutru"));
        Assert.AreEqual("ora două", converter.ConvertClockTime(new TimeOnly(2, 0)));
        Assert.AreEqual("ora unu", converter.ConvertClockTime(new TimeOnly(1, 0)));
    }
}
