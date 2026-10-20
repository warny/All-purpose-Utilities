using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-44 — Slovak oblique fractions whose numerator ends in a bare <c>jeden</c> (NTS-42 family C). The higher compound
/// numerals ending in <c>jeden</c> are indeclinable like <c>dvadsaťjeden</c> (Jarošová, Slovenská reč 86, 2021, 223:
/// <i>101 stojeden / sto jeden – nesklonné</i>; 226: <i>vyššie zložené číslovky končiace sa na číslovku jeden (podobne
/// ako typ dvadsaťjeden) sú nesklonné</i>; the millions and milliards belong to the higher compounds, note 18, 221, and
/// table 2.4.1, 223–224) and the counted noun alone takes the case (PSP 1953, 88–89, quoted there p. 211: <i>so
/// stojeden družstvami</i>; Morfológia slovenského jazyka, 1966, 326: <i>ku štyridsaťjeden družstvám, so štyridsaťjeden
/// družstvami</i>). The expected strings apply that rule to the fraction nouns; they are not quotations. The spelling
/// is the converter's separated groups (<c>sto jeden</c>, <c>tisíc jeden</c>), one of the two spellings Jarošová admits.
/// </summary>
[TestClass]
public class NumberToStringNts44Tests
{
    private static readonly string[] s_slovakGenders = ["gender=mužský", "gender=ženský", "gender=stredný"];
    private static readonly string[] s_slovakObliqueCases = ["case=genitív", "case=datív", "case=lokál", "case=inštrumentál"];

    /// <summary>Gets the Slovak converter.</summary>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Slovak() => NumberToStringConverter.GetConverter("SK");

    /// <summary>
    /// The required matrix: 101, 1001 and 2001 over three in the four oblique cases. The numeral keeps its absolute
    /// form and the noun <c>tretina</c> (pattern <c>žena</c>, Morfológia 101) takes the plural of the case.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(101L, "case=genitív", "sto jeden tretín")]
    [DataRow(101L, "case=datív", "sto jeden tretinám")]
    [DataRow(101L, "case=lokál", "sto jeden tretinách")]
    [DataRow(101L, "case=inštrumentál", "sto jeden tretinami")]
    [DataRow(1001L, "case=genitív", "tisíc jeden tretín")]
    [DataRow(1001L, "case=datív", "tisíc jeden tretinám")]
    [DataRow(1001L, "case=lokál", "tisíc jeden tretinách")]
    [DataRow(1001L, "case=inštrumentál", "tisíc jeden tretinami")]
    [DataRow(2001L, "case=genitív", "dvetisíc jeden tretín")]
    [DataRow(2001L, "case=datív", "dvetisíc jeden tretinám")]
    [DataRow(2001L, "case=lokál", "dvetisíc jeden tretinách")]
    [DataRow(2001L, "case=inštrumentál", "dvetisíc jeden tretinami")]
    public void BareJeden_ObliqueCases_OverThree(long numerator, string variant, string expected)
        => Assert.AreEqual(expected, Slovak().ConvertFraction(numerator, 3, variant));

    /// <summary>
    /// Other fraction nouns: <c>polovica</c> (pattern <c>ulica</c>, Morfológia 104: <i>poloviciam, poloviciach,
    /// polovicami</i>), <c>stotina</c>, <c>tisícina</c> and <c>milióntina</c> (pattern <c>žena</c>), and other
    /// hundreds and thousands before the bare <c>jeden</c>.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(101L, 2L, "case=genitív", "sto jeden polovíc")]
    [DataRow(101L, 2L, "case=datív", "sto jeden poloviciam")]
    [DataRow(101L, 2L, "case=lokál", "sto jeden poloviciach")]
    [DataRow(101L, 2L, "case=inštrumentál", "sto jeden polovicami")]
    [DataRow(101L, 100L, "case=datív", "sto jeden stotinám")]
    [DataRow(1001L, 1000L, "case=inštrumentál", "tisíc jeden tisícinami")]
    [DataRow(2001L, 1_000_000L, "case=lokál", "dvetisíc jeden milióntinách")]
    [DataRow(201L, 3L, "case=datív", "dvesto jeden tretinám")]
    [DataRow(901L, 7L, "case=inštrumentál", "deväťsto jeden sedminami")]
    [DataRow(5101L, 10L, "case=genitív", "päťtisíc sto jeden desatín")]
    public void BareJeden_OtherDenominators(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Slovak().ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// The direct cases are unchanged: the nominative and the accusative keep the counting form with the genitive
    /// plural (Morfológia 317: <i>v priamych pádoch má tvar G. pl.</i>).
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="expected">The expected reading in both direct cases.</param>
    [TestMethod]
    [DataRow(101L, "sto jeden tretín")]
    [DataRow(1001L, "tisíc jeden tretín")]
    [DataRow(2001L, "dvetisíc jeden tretín")]
    public void BareJeden_DirectCasesUnchanged(long numerator, string expected)
    {
        Assert.AreEqual(expected, Slovak().ConvertFraction(numerator, 3));
        Assert.AreEqual(expected, Slovak().ConvertFraction(numerator, 3, "case=nominatív"));
        Assert.AreEqual(expected, Slovak().ConvertFraction(numerator, 3, "case=akuzatív"));
    }

    /// <summary>A negative numerator takes <c>mínus</c> once, before the declined phrase.</summary>
    [TestMethod]
    public void BareJeden_Sign()
    {
        Assert.AreEqual("mínus sto jeden tretinám", Slovak().ConvertFraction(-101, 3, "case=datív"));
        Assert.AreEqual("mínus tisíc jeden tretinami", Slovak().ConvertFraction(1001, -3, "case=inštrumentál"));
    }

    /// <summary>
    /// The caller's gender does not change the reading: <c>jeden</c> in a compound has the masculine form for every
    /// noun (Morfológia 326) and the fraction noun fixes its own gender.
    /// </summary>
    [TestMethod]
    public void BareJeden_GenderVariantsAgree()
    {
        foreach (string gender in s_slovakGenders)
        {
            Assert.AreEqual("sto jeden tretinám", Slovak().ConvertFraction(101, 3, "case=datív", gender), gender);
            Assert.AreEqual("dvetisíc jeden tretinami", Slovak().ConvertFraction(2001, 3, "case=inštrumentál", gender), gender);
        }
    }

    /// <summary>
    /// The millions and milliards: Jarošová counts them among the higher compounds (note 18, 221; table 2.4.1,
    /// 223–224, where <i>milión dvestotridsaťštyritisíc päťsto šesťdesiatich siedmich</i> declines only the last
    /// component), so a final bare <c>jeden</c> after them is indeclinable as well, and the words before it keep the
    /// absolute form the plugin already reads before an indeclinable <c>dvadsaťjeden</c> (1 000 021).
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(1_000_001L, "case=datív", "jeden milión jeden tretinám")]
    [DataRow(1_000_001L, "case=inštrumentál", "jeden milión jeden tretinami")]
    [DataRow(1_000_101L, "case=lokál", "jeden milión sto jeden tretinách")]
    [DataRow(1_001_001L, "case=genitív", "jeden milión tisíc jeden tretín")]
    [DataRow(1_000_000_001L, "case=inštrumentál", "jedna miliarda jeden tretinami")]
    [DataRow(2_000_000_001L, "case=datív", "dve miliardy jeden tretinám")]
    [DataRow(1_000_021L, "case=datív", "jeden milión dvadsaťjeden tretinám")]
    [DataRow(1_000_121L, "case=lokál", "jeden milión sto dvadsaťjeden tretinách")]
    [DataRow(1_000_000_021L, "case=inštrumentál", "jedna miliarda dvadsaťjeden tretinami")]
    public void BareJeden_AfterTheScaleNouns(long numerator, string variant, string expected)
        => Assert.AreEqual(expected, Slovak().ConvertFraction(numerator, 3, variant));

    /// <summary>
    /// The opened class is the indeclinable one only: in every oblique case the numerator of a value ending in a bare
    /// <c>jeden</c> is its absolute cardinal, up to the last declined value below 10^12.
    /// </summary>
    [TestMethod]
    public void BareJeden_NumeratorIsTheAbsoluteCardinal()
    {
        long[] numerators = [101, 201, 1001, 2001, 10_001, 100_001, 999_001, 1_000_001, 999_999_999_001];
        foreach (long numerator in numerators)
        {
            string absolute = Slovak().ConvertFraction(numerator, 3);
            Assert.IsTrue(absolute.EndsWith(" jeden tretín", StringComparison.Ordinal), absolute);
            string head = absolute[..^" tretín".Length];
            Assert.AreEqual(head + " tretín", Slovak().ConvertFraction(numerator, 3, "case=genitív"), numerator.ToString());
            Assert.AreEqual(head + " tretinám", Slovak().ConvertFraction(numerator, 3, "case=datív"), numerator.ToString());
            Assert.AreEqual(head + " tretinách", Slovak().ConvertFraction(numerator, 3, "case=lokál"), numerator.ToString());
            Assert.AreEqual(head + " tretinami", Slovak().ConvertFraction(numerator, 3, "case=inštrumentál"), numerator.ToString());
        }
    }

    /// <summary>
    /// The neighbouring values keep their reading: 1 agrees in the singular, 2–4 and the other compounds decline
    /// (Morfológia 320–326), <c>jedenásť</c> follows <c>päť</c>, <c>sto</c> and <c>tisíc</c> stay indeclinable, the
    /// <c>-jeden</c> compounds keep their NTS-40 reading.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(1L, "case=datív", "jednej tretine")]
    [DataRow(2L, "case=datív", "dvom tretinám")]
    [DataRow(3L, "case=lokál", "troch tretinách")]
    [DataRow(4L, "case=inštrumentál", "štyrmi tretinami")]
    [DataRow(11L, "case=datív", "jedenástim tretinám")]
    [DataRow(21L, "case=datív", "dvadsaťjeden tretinám")]
    [DataRow(99L, "case=datív", "deväťdesiatim deviatim tretinám")]
    [DataRow(100L, "case=datív", "sto tretinám")]
    [DataRow(102L, "case=datív", "sto dvom tretinám")]
    [DataRow(111L, "case=datív", "sto jedenástim tretinám")]
    [DataRow(121L, "case=inštrumentál", "sto dvadsaťjeden tretinami")]
    [DataRow(199L, "case=inštrumentál", "sto deväťdesiatimi deviatimi tretinami")]
    [DataRow(999L, "case=genitív", "deväťsto deväťdesiatich deviatich tretín")]
    [DataRow(1000L, "case=datív", "tisíc tretinám")]
    [DataRow(1002L, "case=inštrumentál", "tisíc dvoma tretinami")]
    [DataRow(1_000_000L, "case=inštrumentál", "jedným miliónom tretín")]
    public void Neighbours_Unchanged(long numerator, string variant, string expected)
        => Assert.AreEqual(expected, Slovak().ConvertFraction(numerator, 3, variant));

    /// <summary>
    /// What NTS-44 leaves closed: zero (NTS-42 E) and a denominator without a fraction noun (NTS-42 D). From 10^12 a bare
    /// final <c>jeden</c>, closed with the whole of NTS-42 F until then, reads exactly as after <c>milión</c> since NTS-42 F
    /// (the scale noun before it is not the last component and keeps its absolute form).
    /// </summary>
    [TestMethod]
    public void StillClosed()
    {
        foreach (string variant in s_slovakObliqueCases)
            Assert.ThrowsExactly<NotSupportedException>(() => Slovak().ConvertFraction(BigInteger.Zero, 3, variant), variant);
        foreach (string variant in s_slovakObliqueCases)
            Assert.ThrowsExactly<NotSupportedException>(() => Slovak().ConvertFraction(101, 23, variant), variant);
        foreach (int tail in new[] { 1, 101 })
            foreach (string variant in s_slovakObliqueCases)
                Assert.AreEqual(
                    Slovak().ConvertFraction(1_000_000 + tail, 3, variant).Replace("milión", "bilión"),
                    Slovak().ConvertFraction(BigInteger.Pow(10, 12) + tail, 3, variant),
                    $"{tail} {variant}");
    }

    /// <summary>
    /// The <c>lomené</c> reading of a denominator ending in a bare <c>jeden</c> is unchanged: indeclinable, it takes
    /// <c>číslom</c> and the absolute cardinal (Ondrus 1968, §6).
    /// </summary>
    /// <param name="denominator">The denominator.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(101L, "jedna lomené číslom sto jeden")]
    [DataRow(1001L, "jedna lomené číslom tisíc jeden")]
    [DataRow(1_000_001L, "jedna lomené číslom jeden milión jeden")]
    public void SlashDenominator_Unchanged(long denominator, string expected)
        => Assert.AreEqual(expected, Slovak().ConvertFraction(1, denominator));
}
