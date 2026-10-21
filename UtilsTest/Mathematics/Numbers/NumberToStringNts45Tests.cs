using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-45 — the Czech and Slovak NTS-42 families A, B, D and E.
/// <list type="bullet">
/// <item><b>A, B (CS, opened)</b>: a <c>jeden/jedna</c> ending a compound numeral (21, 101, 1001, 10^12 + 1) or a
/// multiplier (21 000, 101 000, 21 × 10^12) declines in the oblique cases and agrees with what it counts: the feminine
/// fraction noun, or the scale noun (<c>tisíc</c>, <c>milion</c>, <c>bilion</c> masculine; <c>miliarda</c>,
/// <c>biliarda</c> feminine), which then stands in the singular of the case. The IJP declines every part of a multi-word
/// numeral (<i>před třemi sty šedesáti pěti lety</i>) and makes the counted noun agree with the last member of a
/// compound when the noun is not in the genitive (<i>dvacet jeden žák</i>); corpus examples attest the singular
/// agreement in the oblique cases (<i>dožil se devadesáti jednoho roku</i>, <i>po sto jednom roce</i>, <i>sto padesát
/// jedním hlasem</i>). The nominative and accusative keep their counting form (<i>dvacet jedna třetin</i>).</item>
/// <item><b>D (CS, SK, still closed)</b>: no source reads a whole <c>lomeno</c>/<c>lomené</c> phrase under an external
/// case.</item>
/// <item><b>E</b>: SK opened — <c>nula</c> stays indeclinable before a counted noun, which takes the case (Š. Peciar,
/// "Nula celých, či nula celá?", Slovenská reč 23, 1958, 117–118: <i>na nula stupňov</i>, <i>z nula percent</i>, <i>s
/// nula celými</i>); CS still closed (no normative source).</item>
/// </list>
/// The expected strings apply these rules to the fraction nouns; they are not quotations.
/// </summary>
[TestClass]
public class NumberToStringNts45Tests
{
    private static readonly string[] s_czechGenders = ["gender=mužský", "gender=ženský", "gender=střední"];
    private static readonly string[] s_czechObliqueCases = ["case=genitiv", "case=dativ", "case=lokál", "case=instrumentál"];
    private static readonly string[] s_slovakGenders = ["gender=mužský", "gender=ženský", "gender=stredný"];
    private static readonly string[] s_slovakObliqueCases = ["case=genitív", "case=datív", "case=lokál", "case=inštrumentál"];

    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Reads a fraction, under a variant when one is given.</summary>
    /// <param name="language">The culture.</param>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="variant">The variant, or an empty string for none.</param>
    /// <returns>The reading.</returns>
    private static string Read(string language, BigInteger numerator, BigInteger denominator, string variant)
        => variant.Length == 0
            ? Get(language).ConvertFraction(numerator, denominator)
            : Get(language).ConvertFraction(numerator, denominator, variant);

    /// <summary>Asserts that a fraction fails closed under each of the given case variants.</summary>
    /// <param name="language">The culture.</param>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="cases">The case variants.</param>
    private static void AssertClosed(string language, BigInteger numerator, BigInteger denominator, IEnumerable<string> cases)
    {
        foreach (string variant in cases)
            Assert.ThrowsExactly<NotSupportedException>(() => Get(language).ConvertFraction(numerator, denominator, variant),
                $"{language} {numerator}/{denominator} {variant}");
    }

    // ===== A — CS compound numerator ending in jedna ================================================

    /// <summary>
    /// A: the final <c>jedna</c> agrees with the feminine fraction noun in the case (<c>jedné</c>, <c>jednou</c>), the
    /// words before it declined as before (<c>dvaceti</c>, <c>sto</c> undeclined before a numeral, <c>tisíce</c>), and
    /// the noun is in the singular of the case.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(21L, 100L, "case=genitiv", "dvaceti jedné setiny")]
    [DataRow(21L, 100L, "case=dativ", "dvaceti jedné setině")]
    [DataRow(21L, 100L, "case=lokál", "dvaceti jedné setině")]
    [DataRow(21L, 100L, "case=instrumentál", "dvaceti jednou setinou")]
    [DataRow(31L, 3L, "case=genitiv", "třiceti jedné třetiny")]
    [DataRow(91L, 2L, "case=dativ", "devadesáti jedné polovině")]
    [DataRow(101L, 3L, "case=dativ", "sto jedné třetině")]
    [DataRow(101L, 3L, "case=instrumentál", "sto jednou třetinou")]
    [DataRow(121L, 1000L, "case=lokál", "sto dvaceti jedné tisícině")]
    [DataRow(201L, 3L, "case=genitiv", "dvou set jedné třetiny")]
    [DataRow(1001L, 3L, "case=genitiv", "tisíce jedné třetiny")]
    [DataRow(1001L, 3L, "case=instrumentál", "tisícem jednou třetinou")]
    [DataRow(1021L, 100L, "case=dativ", "tisíci dvaceti jedné setině")]
    [DataRow(2001L, 3L, "case=lokál", "dvou tisících jedné třetině")]
    [DataRow(1_000_001L, 3L, "case=genitiv", "jednoho milionu jedné třetiny")]
    [DataRow(1_000_000_001L, 3L, "case=dativ", "jedné miliardě jedné třetině")]
    public void A_CompoundEndingInJedna_ObliqueCases(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Read("CS", numerator, denominator, variant));

    /// <summary>A from 10^12: the generated scale nouns of the converter decline as before NTS-45 (NTS-42 F).</summary>
    /// <param name="power">The power of ten of the scale.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading of 10^power + 1 over three.</param>
    [TestMethod]
    [DataRow(12, "case=genitiv", "jednoho bilionu jedné třetiny")]
    [DataRow(12, "case=dativ", "jednomu bilionu jedné třetině")]
    [DataRow(12, "case=instrumentál", "jedním bilionem jednou třetinou")]
    [DataRow(15, "case=lokál", "jedné biliardě jedné třetině")]
    [DataRow(15, "case=instrumentál", "jednou biliardou jednou třetinou")]
    [DataRow(18, "case=dativ", "jednomu trilionu jedné třetině")]
    public void A_CompoundEndingInJedna_FromTenToTheTwelve(int power, string variant, string expected)
        => Assert.AreEqual(expected, Read("CS", BigInteger.Pow(10, power) + 1, 3, variant));

    /// <summary>
    /// A: the caller's gender does not change a fraction numerator, which agrees with the feminine fraction noun.
    /// </summary>
    [TestMethod]
    public void A_GenderOfTheCallerIsIgnored()
    {
        foreach (string gender in s_czechGenders)
            Assert.AreEqual("dvaceti jedné třetině", Get("CS").ConvertFraction(21, 3, "case=dativ", gender), gender);
    }

    /// <summary>
    /// A, unchanged neighbours: the nominative and accusative keep the counting form and the genitive plural (IJP: the
    /// genitive "běžnější a přirozenější"), <c>jedenáct</c> is not a compound ending in one, and a simple one declines.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="variant">The variant, or an empty string for the nominative.</param>
    /// <param name="expected">The expected reading over three.</param>
    [TestMethod]
    [DataRow(21L, "", "dvacet jedna třetin")]
    [DataRow(21L, "case=akuzativ", "dvacet jedna třetin")]
    [DataRow(1001L, "case=akuzativ", "tisíc jedna třetin")]
    [DataRow(11L, "case=dativ", "jedenácti třetinám")]
    [DataRow(111L, "case=dativ", "sto jedenácti třetinám")]
    [DataRow(1L, "case=dativ", "jedné třetině")]
    [DataRow(22L, "case=dativ", "dvaceti dvěma třetinám")]
    public void A_NeighboursUnchanged(long numerator, string variant, string expected)
        => Assert.AreEqual(expected, Read("CS", numerator, 3, variant));

    // ===== B — CS multiplier ending in one ==========================================================

    /// <summary>
    /// B: the multiplier's final <c>jeden/jedna</c> agrees with the scale noun (masculine <c>tisíc</c>, <c>milion</c>,
    /// <c>bilion</c>; feminine <c>miliarda</c>, <c>biliarda</c>), which stands in the singular of the case — the rule of
    /// A applied to the noun the multiplier counts. A round number keeps the fraction noun in the genitive plural.
    /// </summary>
    /// <param name="numerator">The numerator, as a string (BigInteger).</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading over three.</param>
    [TestMethod]
    [DataRow("21000", "case=genitiv", "dvaceti jednoho tisíce třetin")]
    [DataRow("21000", "case=dativ", "dvaceti jednomu tisíci třetin")]
    [DataRow("31000", "case=instrumentál", "třiceti jedním tisícem třetin")]
    [DataRow("101000", "case=dativ", "sto jednomu tisíci třetin")]
    [DataRow("121000", "case=lokál", "sto dvaceti jednom tisíci třetin")]
    [DataRow("21000000", "case=instrumentál", "dvaceti jedním milionem třetin")]
    [DataRow("31000000", "case=genitiv", "třiceti jednoho milionu třetin")]
    [DataRow("101000000", "case=lokál", "sto jednom milionu třetin")]
    [DataRow("21000000000", "case=dativ", "dvaceti jedné miliardě třetin")]
    [DataRow("21000000000", "case=instrumentál", "dvaceti jednou miliardou třetin")]
    [DataRow("21000000000000", "case=lokál", "dvaceti jednom bilionu třetin")]
    [DataRow("101000000000000", "case=dativ", "sto jednomu bilionu třetin")]
    [DataRow("21000000000000000", "case=instrumentál", "dvaceti jednou biliardou třetin")]
    public void B_MultiplierEndingInOne_ObliqueNumerators(string numerator, string variant, string expected)
        => Assert.AreEqual(expected, Read("CS", BigInteger.Parse(numerator), 3, variant));

    /// <summary>
    /// B combined with A and with the lower groups: the final one of each multiplier agrees with its scale noun, the
    /// final one of the numeral with the fraction noun.
    /// </summary>
    /// <param name="numerator">The numerator, as a string (BigInteger).</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading over three.</param>
    [TestMethod]
    [DataRow("21021", "case=dativ", "dvaceti jednomu tisíci dvaceti jedné třetině")]
    [DataRow("21001", "case=instrumentál", "dvaceti jedním tisícem jednou třetinou")]
    [DataRow("21022", "case=dativ", "dvaceti jednomu tisíci dvaceti dvěma třetinám")]
    [DataRow("21000021000", "case=genitiv", "dvaceti jedné miliardy dvaceti jednoho tisíce třetin")]
    [DataRow("21000000000001", "case=dativ", "dvaceti jednomu bilionu jedné třetině")]
    public void B_CombinedWithA(string numerator, string variant, string expected)
        => Assert.AreEqual(expected, Read("CS", BigInteger.Parse(numerator), 3, variant));

    /// <summary>
    /// B in the <c>lomeno</c> reading: the denominator is an instrumental, so the multiplier's final one is the
    /// instrumental of the scale noun's gender (<c>jedním tisícem</c>, <c>jednou miliardou</c>); a final <c>jedna</c> of
    /// the whole denominator stays the feminine <c>jednou</c> of the arithmetic term (NTS-41).
    /// </summary>
    /// <param name="denominator">The denominator, as a string (BigInteger).</param>
    /// <param name="expected">The expected reading of one over it.</param>
    [TestMethod]
    [DataRow("21000", "jedna lomeno dvaceti jedním tisícem")]
    [DataRow("101000", "jedna lomeno sto jedním tisícem")]
    [DataRow("121000", "jedna lomeno sto dvaceti jedním tisícem")]
    [DataRow("21000000", "jedna lomeno dvaceti jedním milionem")]
    [DataRow("21000001", "jedna lomeno dvaceti jedním milionem jednou")]
    [DataRow("21000000000", "jedna lomeno dvaceti jednou miliardou")]
    [DataRow("21000000000000", "jedna lomeno dvaceti jedním bilionem")]
    [DataRow("101000000000000", "jedna lomeno sto jedním bilionem")]
    [DataRow("21000000000000000", "jedna lomeno dvaceti jednou biliardou")]
    public void B_MultiplierEndingInOne_Lomeno(string denominator, string expected)
        => Assert.AreEqual(expected, Read("CS", 1, BigInteger.Parse(denominator), ""));

    /// <summary>
    /// B, unchanged neighbours: the accusative keeps the counting form, a multiplier ending in two and a bare multiplier
    /// of one read as before, and the final <c>jednou</c> of a denominator is unchanged.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="variant">The variant, or an empty string for the nominative.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(21_000L, 3L, "case=akuzativ", "dvacet jedna tisíc třetin")]
    [DataRow(21_000L, 3L, "", "dvacet jedna tisíc třetin")]
    [DataRow(22_000L, 3L, "case=dativ", "dvaceti dvěma tisícům třetin")]
    [DataRow(11_000L, 3L, "case=dativ", "jedenácti tisícům třetin")]
    [DataRow(1L, 22_000L, "", "jedna lomeno dvaceti dvěma tisíci")]
    [DataRow(1L, 1_001_000L, "", "jedna lomeno jedním milionem tisícem")]
    [DataRow(1L, 21L, "", "jedna lomeno dvaceti jednou")]
    [DataRow(1L, 1001L, "", "jedna lomeno tisícem jednou")]
    public void B_NeighboursUnchanged(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Read("CS", numerator, denominator, variant));

    // ===== D — lomeno / lomené under an external case ===============================================

    /// <summary>
    /// D (characterization, still closed): a fraction read with <c>lomeno</c>/<c>lomené</c> — a denominator without a
    /// fraction noun — fails closed in every case but the nominative, with a simple and a scaled denominator, while its
    /// nominative reads. The same numerators over a fraction noun read in the oblique cases, which isolates the
    /// closure on the slash reading itself. No source reads the first operand under an external case (<i>s dvěma lomeno
    /// třemi</i> / <i>s dvě lomeno třemi</i>; <i>s dvoma lomené troma</i> / <i>s dva lomené troma</i>).
    /// </summary>
    /// <param name="language">The culture.</param>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="nominative">The nominative reading.</param>
    [TestMethod]
    [DataRow("CS", 2L, 23L, "dvě lomeno dvaceti třemi")]
    [DataRow("CS", 1L, 21L, "jedna lomeno dvaceti jednou")]
    [DataRow("CS", 2L, 21_000L, "dvě lomeno dvaceti jedním tisícem")]
    [DataRow("CS", 2L, 2_000_000L, "dvě lomeno dvěma miliony")]
    [DataRow("SK", 2L, 23L, "dva lomené dvadsiatimi troma")]
    [DataRow("SK", 1L, 101L, "jedna lomené číslom sto jeden")]
    [DataRow("SK", 2L, 2_000_000L, "dva lomené dvoma miliónmi")]
    public void D_SlashReading_StillClosedUnderAnExternalCase(string language, long numerator, long denominator, string nominative)
    {
        Assert.AreEqual(nominative, Read(language, numerator, denominator, ""));
        string[] cases = language == "CS"
            ? [.. s_czechObliqueCases, "case=akuzativ"]
            : [.. s_slovakObliqueCases, "case=akuzatív"];
        AssertClosed(language, numerator, denominator, cases);
        Assert.AreEqual(language == "CS" ? "dvou třetin" : "dvoch tretín", Read(language, 2, 3, language == "CS" ? "case=genitiv" : "case=genitív"));
    }

    // ===== E — zero numerator ======================================================================

    /// <summary>
    /// E/SK: <c>nula</c> stays in its absolute form and the fraction noun takes the case (Peciar 1958: <i>na nula
    /// stupňov</i>, <i>z nula percent</i>, <i>s nula celými</i>, "nesklonnosť výrazu nula v spojení s podst. menom dobre
    /// vidno z nepriamych pádov"; the noun "stojí za ním" and changes, as after <c>sto</c>: <i>pred sto rohmi</i>). The
    /// accusative keeps the genitive plural of the nominative (<i>na nula stupňov</i>).
    /// </summary>
    /// <param name="denominator">The denominator.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading of zero over it.</param>
    [TestMethod]
    [DataRow(3L, "case=genitív", "nula tretín")]
    [DataRow(3L, "case=datív", "nula tretinám")]
    [DataRow(3L, "case=akuzatív", "nula tretín")]
    [DataRow(3L, "case=lokál", "nula tretinách")]
    [DataRow(3L, "case=inštrumentál", "nula tretinami")]
    [DataRow(2L, "case=genitív", "nula polovíc")]
    [DataRow(2L, "case=datív", "nula poloviciam")]
    [DataRow(2L, "case=akuzatív", "nula polovíc")]
    [DataRow(2L, "case=lokál", "nula poloviciach")]
    [DataRow(2L, "case=inštrumentál", "nula polovicami")]
    [DataRow(100L, "case=datív", "nula stotinám")]
    [DataRow(1000L, "case=inštrumentál", "nula tisícinami")]
    [DataRow(1_000_000L, "case=lokál", "nula milióntinách")]
    public void E_SlovakZero_FractionNounInTheCase(long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Read("SK", 0, denominator, variant));

    /// <summary>E/SK: the caller's gender does not change <c>nula</c>, and the nominative is unchanged.</summary>
    [TestMethod]
    public void E_SlovakZero_GenderIgnoredAndNominativeUnchanged()
    {
        Assert.AreEqual("nula tretín", Read("SK", 0, 3, ""));
        foreach (string gender in s_slovakGenders)
            Assert.AreEqual("nula tretinám", Get("SK").ConvertFraction(0, 3, "case=datív", gender), gender);
    }

    /// <summary>
    /// E/SK: a zero over a denominator without a fraction noun is a <c>lomené</c> reading and stays closed under an
    /// external case with family D.
    /// </summary>
    [TestMethod]
    public void E_SlovakZero_SlashReadingStaysClosed()
    {
        Assert.AreEqual("nula lomené dvadsiatimi troma", Read("SK", 0, 23, ""));
        AssertClosed("SK", 0, 23, [.. s_slovakObliqueCases, "case=akuzatív"]);
        AssertClosed("SK", 0, 1001, [.. s_slovakObliqueCases, "case=akuzatív"]);
    }

    /// <summary>
    /// E/CS (characterization, still closed): usage attests an indeclinable <c>nula</c> before a counted noun (<i>při
    /// nula stupních</i>), but no Czech normative source states it as Peciar does for Slovak, and the IJP declines
    /// <c>nula</c> as a noun (<i>nuly, nule, nulou</i>); a zero numerator stays closed in every oblique case, with and
    /// without a fraction noun, the nominative <c>nula lomeno třemi</c> unchanged.
    /// </summary>
    [TestMethod]
    public void E_CzechZero_StillClosed()
    {
        long[] denominators = [2, 3, 23, 100, 1000];
        foreach (long denominator in denominators)
            AssertClosed("CS", 0, denominator, [.. s_czechObliqueCases, "case=akuzativ"]);
        Assert.AreEqual("nula lomeno třemi", Read("CS", 0, 3, ""));
    }
}
