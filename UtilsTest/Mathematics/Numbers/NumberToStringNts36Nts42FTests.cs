using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-36 — the Czech and Slovak spelling of the generated (Conway) scale names — and NTS-42 family F — the CS and SK
/// fractions from 10^12, which need those names in a case. The two are tested separately: the NTS-36 tests read
/// cardinals only, the NTS-42 F tests fractions only.
/// <para>
/// NTS-36 sources: Internetová jazyková příručka (ÚJČ AV ČR), entries <i>bilion</i>, <i>trilion</i>, <i>kvadrilion</i>,
/// <i>kvintilion</i>, <i>sextilion</i>, <i>septilion</i>, <i>biliarda</i>, <i>triliarda</i>; Slovník cudzích slov,
/// entries <i>bilión</i>, <i>trilión</i>, <i>kvadrilión</i>, <i>kvintilión</i>, <i>sextilión</i>, <i>septilión</i>,
/// <i>biliarda</i>; KSSJ <i>bilión</i>. The attested names fix the spelling of the Latin components (<c>qu</c> →
/// <c>kv</c>, <c>oct</c> → <c>okt</c>, <c>x</c> and <c>c</c> kept); the higher names are their productive continuation
/// (AGENTS.md, Conway scale names), not quotations.
/// </para>
/// <para>
/// NTS-42 F sources: the IJP paradigms of <i>bilion</i> … <i>septilion</i> (masculine inanimate, <c>-u</c>, <c>-u</c>,
/// <c>-em</c>; plural <c>-y</c>, <c>-ů</c>, <c>-ům</c>, <c>-ech</c>) and <i>biliarda</i>, <i>triliarda</i> (feminine,
/// <c>žena</c>); Slovak: the <c>dub</c> paradigm of <i>milión</i> (Morfológia slovenského jazyka, 1966, 330) for the
/// masculine family, whose genitive singular is lexical (<i>bilión -a</i>, KSSJ; <i>trilión -u</i>, <i>kvadrilión
/// -u</i>, Slovník cudzích slov) and therefore not generated, and the <c>žena</c> paradigm of <i>miliarda</i> for the
/// feminine family. The construction rules are those of NTS-40/41 (CS: every part declined; SK: only the last
/// component). The expected strings apply those rules; they are not quotations.
/// </para>
/// </summary>
[TestClass]
public class NumberToStringNts36Nts42FTests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Returns <paramref name="multiplier"/> × 10^<paramref name="exponent"/>.</summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <returns>The value.</returns>
    private static BigInteger E(long multiplier, int exponent) => multiplier * BigInteger.Pow(10, exponent);

    // ===== NTS-36 — cardinals ======================================================================

    /// <summary>
    /// The Czech generated names: the attested base series (IJP: <i>kvadrilion</i>, <i>kvintilion</i>,
    /// <i>sextilion</i>, <i>septilion</i>, <i>triliarda</i>) and their continuation through the units, tens and hundreds
    /// tables (<c>kv</c> for <c>qu</c>, <c>okt</c> for <c>oct</c>), up to a name past the 999th prefix.
    /// </summary>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="expected">The expected cardinal.</param>
    [TestMethod]
    [DataRow(12, "jeden bilion")]
    [DataRow(15, "jedna biliarda")]
    [DataRow(18, "jeden trilion")]
    [DataRow(21, "jedna triliarda")]
    [DataRow(24, "jeden kvadrilion")]
    [DataRow(27, "jedna kvadriliarda")]
    [DataRow(30, "jeden kvintilion")]
    [DataRow(33, "jedna kvintiliarda")]
    [DataRow(36, "jeden sextilion")]
    [DataRow(42, "jeden septilion")]
    [DataRow(48, "jeden oktilion")]
    [DataRow(51, "jedna oktiliarda")]
    [DataRow(54, "jeden nonilion")]
    [DataRow(60, "jeden decilion")]
    [DataRow(84, "jeden kvatuordecilion")]
    [DataRow(90, "jeden kvinkvadecilion")]
    [DataRow(120, "jeden vigintilion")]
    [DataRow(240, "jeden kvadragintilion")]
    [DataRow(300, "jeden kvinkvagintilion")]
    [DataRow(480, "jeden oktogintilion")]
    [DataRow(600, "jeden centilion")]
    [DataRow(2400, "jeden kvadringentilion")]
    [DataRow(3000, "jeden kvingentilion")]
    [DataRow(4800, "jeden oktingentilion")]
    [DataRow(6000, "jeden milinilion")]
    [DataRow(6024, "jeden milikvadrilion")]
    public void Nts36_Czech_GeneratedNames(int exponent, string expected)
        => Assert.AreEqual(expected, Get("CS").Convert(BigInteger.Pow(10, exponent)));

    /// <summary>The Slovak generated names (Slovník cudzích slov: <i>kvadrilión</i>, <i>kvintilión</i>, <i>sextilión</i>, <i>septilión</i>).</summary>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="expected">The expected cardinal.</param>
    [TestMethod]
    [DataRow(12, "jeden bilión")]
    [DataRow(15, "jedna biliarda")]
    [DataRow(18, "jeden trilión")]
    [DataRow(24, "jeden kvadrilión")]
    [DataRow(27, "jedna kvadriliarda")]
    [DataRow(30, "jeden kvintilión")]
    [DataRow(33, "jedna kvintiliarda")]
    [DataRow(36, "jeden sextilión")]
    [DataRow(42, "jeden septilión")]
    [DataRow(48, "jeden oktilión")]
    [DataRow(60, "jeden decilión")]
    [DataRow(84, "jeden kvatuordecilión")]
    [DataRow(240, "jeden kvadragintilión")]
    [DataRow(600, "jeden centilión")]
    [DataRow(4800, "jeden oktingentilión")]
    [DataRow(6024, "jeden milikvadrilión")]
    public void Nts36_Slovak_GeneratedNames(int exponent, string expected)
        => Assert.AreEqual(expected, Get("SK").Convert(BigInteger.Pow(10, exponent)));

    /// <summary>The NTS-30C agreement is unchanged on the respelled names: only the prefix changes.</summary>
    /// <param name="language">The culture.</param>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="expected">The expected cardinal.</param>
    [TestMethod]
    [DataRow("CS", 2L, 24, "dva kvadriliony")]
    [DataRow("CS", 5L, 24, "pět kvadrilionů")]
    [DataRow("CS", 22L, 24, "dvacet dva kvadrilionů")]
    [DataRow("CS", 2L, 27, "dvě kvadriliardy")]
    [DataRow("CS", 5L, 27, "pět kvadriliard")]
    [DataRow("SK", 2L, 24, "dva kvadrilióny")]
    [DataRow("SK", 5L, 24, "päť kvadriliónov")]
    [DataRow("SK", 2L, 27, "dve kvadriliardy")]
    [DataRow("SK", 5L, 27, "päť kvadriliárd")]
    public void Nts36_AgreementUnchanged(string language, long multiplier, int exponent, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(E(multiplier, exponent)));

    /// <summary>The other SCALE-LONG languages keep their own names: NTS-36 changes CS and SK only.</summary>
    [TestMethod]
    public void Nts36_OtherLanguagesUnchanged()
    {
        StringAssert.Contains(Get("FR").Convert(BigInteger.Pow(10, 24)), "quadrillion");
        StringAssert.Contains(Get("SV").Convert(BigInteger.Pow(10, 24)), "kvadriljon");
    }

    // ===== NTS-42 F — Czech =========================================================================

    /// <summary>
    /// Czech oblique numerators from 10^12: every part declined (NTS-40), the generated noun after its IJP paradigm,
    /// singular after one and plural after the other multipliers; after a numeral ending in a scale noun the fraction
    /// noun keeps the genitive plural, as after <c>milion</c>.
    /// </summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="variant">The case.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(1L, 12, "case=genitiv", "jednoho bilionu třetin")]
    [DataRow(1L, 12, "case=dativ", "jednomu bilionu třetin")]
    [DataRow(1L, 12, "case=akuzativ", "jeden bilion třetin")]
    [DataRow(1L, 12, "case=lokál", "jednom bilionu třetin")]
    [DataRow(1L, 12, "case=instrumentál", "jedním bilionem třetin")]
    [DataRow(2L, 12, "case=genitiv", "dvou bilionů třetin")]
    [DataRow(2L, 12, "case=dativ", "dvěma bilionům třetin")]
    [DataRow(2L, 12, "case=akuzativ", "dva biliony třetin")]
    [DataRow(3L, 12, "case=lokál", "třech bilionech třetin")]
    [DataRow(2L, 12, "case=instrumentál", "dvěma biliony třetin")]
    [DataRow(5L, 12, "case=genitiv", "pěti bilionů třetin")]
    [DataRow(5L, 12, "case=instrumentál", "pěti biliony třetin")]
    [DataRow(22L, 12, "case=dativ", "dvaceti dvěma bilionům třetin")]
    [DataRow(1L, 15, "case=genitiv", "jedné biliardy třetin")]
    [DataRow(1L, 15, "case=dativ", "jedné biliardě třetin")]
    [DataRow(1L, 15, "case=akuzativ", "jednu biliardu třetin")]
    [DataRow(1L, 15, "case=instrumentál", "jednou biliardou třetin")]
    [DataRow(2L, 15, "case=akuzativ", "dvě biliardy třetin")]
    [DataRow(2L, 15, "case=dativ", "dvěma biliardám třetin")]
    [DataRow(5L, 15, "case=lokál", "pěti biliardách třetin")]
    [DataRow(1L, 18, "case=instrumentál", "jedním trilionem třetin")]
    [DataRow(1L, 21, "case=genitiv", "jedné triliardy třetin")]
    [DataRow(1L, 24, "case=dativ", "jednomu kvadrilionu třetin")]
    [DataRow(1L, 30, "case=lokál", "jednom kvintilionu třetin")]
    public void Nts42F_Czech_ObliqueScaleNumerators(long multiplier, int exponent, string variant, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(E(multiplier, exponent), 3, variant));

    /// <summary>Czech compounds: every group declined, the noun in the case after a final non-scale group.</summary>
    [TestMethod]
    public void Nts42F_Czech_Compounds()
    {
        var cs = Get("CS");
        Assert.AreEqual("jednomu bilionu dvěma třetinám", cs.ConvertFraction(E(1, 12) + 2, 3, "case=dativ"));
        Assert.AreEqual("jednomu bilionu jednomu milionu třetin", cs.ConvertFraction(E(1, 12) + E(1, 6), 3, "case=dativ"));
        Assert.AreEqual("dvěma bilionům pěti tisícům třetin", cs.ConvertFraction(E(2, 12) + 5000, 3, "case=dativ"));
        Assert.AreEqual("jedné biliardy jednoho bilionu třetin", cs.ConvertFraction(E(1, 15) + E(1, 12), 3, "case=genitiv"));
    }

    /// <summary>
    /// Czech <c>lomeno</c> denominators from 10^12: the instrumental of every part (NTS-41), a final <c>jedna</c> read
    /// <c>jednou</c> as below 10^12 (<i>jedna lomeno jedním milionem jednou</i>).
    /// </summary>
    /// <param name="denominator">The denominator, as a decimal string.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow("1000000000000", "jedna lomeno jedním bilionem")]
    [DataRow("1000000000001", "jedna lomeno jedním bilionem jednou")]
    [DataRow("1000000000003", "jedna lomeno jedním bilionem třemi")]
    [DataRow("2000000000000", "jedna lomeno dvěma biliony")]
    [DataRow("5000000000000", "jedna lomeno pěti biliony")]
    [DataRow("1000000000000000", "jedna lomeno jednou biliardou")]
    [DataRow("5000000000000000", "jedna lomeno pěti biliardami")]
    [DataRow("1000000000000000000000000", "jedna lomeno jedním kvadrilionem")]
    public void Nts42F_Czech_SlashDenominators(string denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(BigInteger.One, BigInteger.Parse(denominator)));

    /// <summary>
    /// What stays closed in Czech: D and E; NTS-42 A (a compound ending in <c>jedna</c>, 10^12 + 1) and B (a multiplier
    /// ending in one after the tens or hundreds, 21 and 101 × 10^12) were opened by NTS-45 over the generated nouns too
    /// (<see cref="NumberToStringNts45Tests"/>); the boundary below 10^12 is unchanged.
    /// </summary>
    [TestMethod]
    public void Nts42F_Czech_StillClosed()
    {
        var cs = Get("CS");
        Assert.AreEqual("jednomu bilionu jedné třetině", cs.ConvertFraction(E(1, 12) + 1, 3, "case=dativ"), "A (NTS-45)");
        Assert.AreEqual("dvaceti jednomu bilionu třetin", cs.ConvertFraction(E(21, 12), 3, "case=dativ"), "B (NTS-45)");
        Assert.AreEqual("sto jednoho bilionu třetin", cs.ConvertFraction(E(101, 12), 3, "case=genitiv"), "B (NTS-45)");
        Assert.AreEqual("jedna lomeno dvaceti jedním bilionem", cs.ConvertFraction(BigInteger.One, E(21, 12)), "B lomeno (NTS-45)");
        Assert.ThrowsExactly<NotSupportedException>(() => cs.ConvertFraction(BigInteger.One, E(1, 12), "case=dativ"), "D");
        Assert.ThrowsExactly<NotSupportedException>(() => cs.ConvertFraction(BigInteger.Zero, 3, "case=dativ"), "E");
        Assert.AreEqual("jednomu milionu třetin", cs.ConvertFraction(1_000_000, 3, "case=dativ"));
    }

    // ===== NTS-42 F — Slovak ========================================================================

    /// <summary>
    /// Slovak oblique numerators from 10^12: only the last component declines (NTS-40); a final generated noun follows
    /// <i>milión</i> (<c>dub</c>) or <i>miliarda</i> (<c>žena</c>) and the fraction noun is in the genitive plural
    /// after it (Morfológia 330).
    /// </summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="variant">The case.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(1L, 12, "case=datív", "jednému biliónu tretín")]
    [DataRow(1L, 12, "case=akuzatív", "jeden bilión tretín")]
    [DataRow(1L, 12, "case=lokál", "jednom bilióne tretín")]
    [DataRow(1L, 12, "case=inštrumentál", "jedným biliónom tretín")]
    [DataRow(2L, 12, "case=genitív", "dvoch biliónov tretín")]
    [DataRow(2L, 12, "case=datív", "dvom biliónom tretín")]
    [DataRow(2L, 12, "case=akuzatív", "dva bilióny tretín")]
    [DataRow(3L, 12, "case=lokál", "troch biliónoch tretín")]
    [DataRow(5L, 12, "case=inštrumentál", "piatimi biliónmi tretín")]
    [DataRow(22L, 12, "case=inštrumentál", "dvadsiatimi dvoma biliónmi tretín")]
    [DataRow(21L, 12, "case=datív", "dvadsaťjeden biliónom tretín")]
    [DataRow(1L, 15, "case=genitív", "jednej biliardy tretín")]
    [DataRow(1L, 15, "case=inštrumentál", "jednou biliardou tretín")]
    [DataRow(2L, 15, "case=akuzatív", "dve biliardy tretín")]
    [DataRow(5L, 15, "case=datív", "piatim biliardám tretín")]
    [DataRow(1L, 18, "case=inštrumentál", "jedným triliónom tretín")]
    [DataRow(1L, 24, "case=datív", "jednému kvadriliónu tretín")]
    public void Nts42F_Slovak_ObliqueScaleNumerators(long multiplier, int exponent, string variant, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(E(multiplier, exponent), 3, variant));

    /// <summary>
    /// Slovak compounds: the words before the last component keep their absolute form (NTS-40, NTS-44), the generated
    /// nouns included.
    /// </summary>
    [TestMethod]
    public void Nts42F_Slovak_Compounds()
    {
        var sk = Get("SK");
        Assert.AreEqual("jeden bilión piatim tretinám", sk.ConvertFraction(E(1, 12) + 5, 3, "case=datív"));
        Assert.AreEqual("jeden bilión jeden tretinám", sk.ConvertFraction(E(1, 12) + 1, 3, "case=datív"));
        Assert.AreEqual("jeden bilión tisíc tretinám", sk.ConvertFraction(E(1, 12) + 1000, 3, "case=datív"));
        Assert.AreEqual("jeden bilión jednému miliónu tretín", sk.ConvertFraction(E(1, 12) + E(1, 6), 3, "case=datív"));
        Assert.AreEqual("dva bilióny piatimi miliardami tretín", sk.ConvertFraction(E(2, 12) + E(5, 9), 3, "case=inštrumentál"));
    }

    /// <summary>Slovak <c>lomené</c> denominators from 10^12 (NTS-41): the instrumental of the last component, or <c>číslom</c>.</summary>
    /// <param name="denominator">The denominator, as a decimal string.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow("1000000000000", "jedna lomené jedným biliónom")]
    [DataRow("2000000000000", "jedna lomené dvoma biliónmi")]
    [DataRow("1000000000003", "jedna lomené jeden bilión troma")]
    [DataRow("1000000000001", "jedna lomené číslom jeden bilión jeden")]
    [DataRow("1000000000000000", "jedna lomené jednou biliardou")]
    [DataRow("1000000000000000000000000", "jedna lomené jedným kvadriliónom")]
    public void Nts42F_Slovak_SlashDenominators(string denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(BigInteger.One, BigInteger.Parse(denominator)));

    /// <summary>
    /// What stays closed in Slovak: the genitive singular of a final generated <c>-ón</c> noun (lexical: <i>bilión
    /// -a</i>, <i>trilión -u</i>), zero (E) and the <c>lomené</c> reading in an oblique case (D).
    /// </summary>
    [TestMethod]
    public void Nts42F_Slovak_StillClosed()
    {
        var sk = Get("SK");
        Assert.ThrowsExactly<NotSupportedException>(() => sk.ConvertFraction(E(1, 12), 3, "case=genitív"), "bilión gen. sg.");
        Assert.ThrowsExactly<NotSupportedException>(() => sk.ConvertFraction(E(1, 18), 3, "case=genitív"), "trilión gen. sg.");
        Assert.ThrowsExactly<NotSupportedException>(() => sk.ConvertFraction(BigInteger.One, E(1, 12), "case=datív"), "D");
        Assert.AreEqual("jedného milióna tretín", sk.ConvertFraction(1_000_000, 3, "case=genitív"));
    }

    // ===== NTS-42 F — boundaries =====================================================================

    /// <summary>
    /// The value below the old boundary is read as before (the milliards are the plugin's own nouns), the first values
    /// from 10^12 through the converter's nouns.
    /// </summary>
    [TestMethod]
    public void Nts42F_Boundary_AroundTenToTheTwelfth()
    {
        var cs = Get("CS");
        var sk = Get("SK");
        Assert.AreEqual(
            "devíti stům devadesáti devíti miliardám devíti stům devadesáti devíti milionům devíti stům devadesáti devíti tisícům devíti stům devadesáti devíti třetinám",
            cs.ConvertFraction(E(1, 12) - 1, 3, "case=dativ"));
        Assert.AreEqual(
            "deväťsto deväťdesiatdeväť miliárd deväťsto deväťdesiatdeväť miliónov deväťstodeväťdesiatdeväťtisíc deväťsto deväťdesiatim deviatim tretinám",
            sk.ConvertFraction(E(1, 12) - 1, 3, "case=datív"));
        Assert.AreEqual("třem bilionům třetin", cs.ConvertFraction(E(3, 12), 3, "case=dativ"));
        Assert.AreEqual("troma biliónmi tretín", sk.ConvertFraction(E(3, 12), 3, "case=inštrumentál"));
        Assert.AreEqual("jedna lomeno třemi biliony", cs.ConvertFraction(BigInteger.One, E(3, 12)));
        Assert.AreEqual("jedna lomené troma biliónmi", sk.ConvertFraction(BigInteger.One, E(3, 12)));
    }

    /// <summary>
    /// A multiplier ending in one after the tens or hundreds: Slovak keeps it in its absolute form before the declined scale
    /// noun, as before <c>milión</c> (Morfológia 330: <i>s päť, osem, dvadsaťdva miliónmi</i>; NTS-41); Czech declines it
    /// in agreement with the scale noun since NTS-45 (NTS-42 B), in the oblique cases as in the <c>lomeno</c> reading.
    /// </summary>
    [TestMethod]
    public void Nts42F_MultiplierEndingInOne()
    {
        var sk = Get("SK");
        Assert.AreEqual("dvadsaťjeden biliónov tretín", sk.ConvertFraction(E(21, 12), 3, "case=genitív"));
        Assert.AreEqual("sto jeden biliónom tretín", sk.ConvertFraction(E(101, 12), 3, "case=datív"));
        Assert.AreEqual("jedna lomené sto jeden biliónmi", sk.ConvertFraction(BigInteger.One, E(101, 12)));
        Assert.AreEqual(sk.ConvertFraction(E(101, 6), 3, "case=datív").Replace("miliónom", "biliónom"),
            sk.ConvertFraction(E(101, 12), 3, "case=datív"));
        var cs = Get("CS");
        Assert.AreEqual("sto jedna bilionů třetin", cs.ConvertFraction(E(101, 12), 3, "case=akuzativ"));
        Assert.AreEqual("jedna lomeno sto jedním bilionem", cs.ConvertFraction(BigInteger.One, E(101, 12)), "B lomeno (NTS-45)");
    }

    /// <summary>The feminine family above <c>biliarda</c>: <c>triliarda</c> after two, in both languages.</summary>
    [TestMethod]
    public void Nts42F_Triliarda()
    {
        Assert.AreEqual("dvěma triliardám třetin", Get("CS").ConvertFraction(E(2, 21), 3, "case=dativ"));
        Assert.AreEqual("jedna lomeno dvěma triliardami", Get("CS").ConvertFraction(BigInteger.One, E(2, 21)));
        Assert.AreEqual("dvoch triliárd tretín", Get("SK").ConvertFraction(E(2, 21), 3, "case=genitív"));
        Assert.AreEqual("jedna lomené dvoma triliardami", Get("SK").ConvertFraction(BigInteger.One, E(2, 21)));
    }

    /// <summary>
    /// The public <see cref="IFractionLanguageSpecifics"/> overload, called without the converter, has no scale noun from
    /// 10^12 and keeps failing closed there; below, it reads as through the converter.
    /// </summary>
    [TestMethod]
    public void Nts42F_PublicOverloadWithoutConverter()
    {
        IFractionLanguageSpecifics cs = new CzechOrdinalLanguageSpecifics();
        IFractionLanguageSpecifics sk = new SlovakOrdinalLanguageSpecifics();
        var csDative = new System.Collections.Generic.Dictionary<string, string> { ["case"] = "dativ" };
        var skDative = new System.Collections.Generic.Dictionary<string, string> { ["case"] = "datív" };
        Assert.IsFalse(cs.TryGetFractionDenominator(E(1, 12), 3, csDative, out _));
        Assert.IsFalse(sk.TryGetFractionDenominator(E(1, 12), 3, skDative, out _));
        Assert.IsFalse(cs.TryGetFractionDenominator(BigInteger.One, E(1, 12), new System.Collections.Generic.Dictionary<string, string>(), out _));
        Assert.IsTrue(cs.TryGetFractionDenominator(E(1, 9), 3, csDative, out var reading));
        Assert.AreEqual("třetin", reading!.Text);
    }

    // ===== NTS-42 F — shared ========================================================================

    /// <summary>
    /// The direct cases agree with the nominative cardinal pipeline: the accusative numerator spelled by the plugin is the
    /// cardinal <c>Convert</c> writes, for the generated scales of both languages (the feminine <c>jedna biliarda</c>,
    /// whose accusative is <c>jednu biliardu</c>, is tested above).
    /// </summary>
    /// <param name="language">The culture.</param>
    /// <param name="variant">The accusative variant.</param>
    /// <param name="noun">The genitive plural of the fraction noun.</param>
    [TestMethod]
    [DataRow("CS", "case=akuzativ", " třetin")]
    [DataRow("SK", "case=akuzatív", " tretín")]
    public void Nts42F_AccusativeMatchesTheCardinal(string language, string variant, string noun)
    {
        BigInteger[] numerators = [E(1, 12), E(2, 12), E(5, 12), E(2, 15), E(5, 15), E(1, 24), E(3, 30) + E(2, 12)];
        foreach (BigInteger numerator in numerators)
            Assert.AreEqual(Get(language).Convert(numerator) + noun, Get(language).ConvertFraction(numerator, 3, variant), numerator.ToString());
    }

    /// <summary>
    /// The fraction plugins hold no generated name: a numerator past the 999th Conway prefix (10^6024) declines the very
    /// name the engine generates.
    /// </summary>
    [TestMethod]
    public void Nts42F_FractionReadsTheEngineName()
    {
        StringAssert.Contains(Get("CS").ConvertFraction(BigInteger.Pow(10, 6024), 3, "case=dativ"), "milikvadrilionu");
        StringAssert.Contains(Get("SK").ConvertFraction(BigInteger.Pow(10, 6024), 3, "case=datív"), "milikvadriliónu");
    }
}
