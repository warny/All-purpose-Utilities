using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-39: the Ukrainian, Bulgarian, Czech and Slovak fraction domains left closed by NTS-30H. Compound numerators follow
/// each language's agreement rule, Czech and Slovak denominators without a fraction noun take the mathematical
/// <c>lomeno</c>/<c>lomené</c> + instrumental reading, the Slovak noun paradigms are complete, zero is read where the
/// construction admits it; denominator one and the oblique cases stay closed. Sources:
/// <c>docs/NTS-08-linguistic-sources.md</c>, NTS-39 section.
/// </summary>
[TestClass]
public class NumberToStringNts39Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    // ===== UK compound numerators ====================================================================

    /// <summary>
    /// The denominator agrees with the last component of the numerator: singular after a compound ending in одна
    /// (<c>тридцять одна сота</c>), genitive plural after two to four (<c>двадцять дві шістдесятих</c>).
    /// </summary>
    [TestMethod]
    [DataRow(21L, 37L, "двадцять одна тридцять сьома")]
    [DataRow(31L, 100L, "тридцять одна сота")]
    [DataRow(101L, 1000L, "сто одна тисячна")]
    [DataRow(21L, 3L, "двадцять одна третя")]
    [DataRow(101L, 3L, "сто одна третя")]
    [DataRow(1001L, 3L, "тисяча одна третя")]
    [DataRow(1_000_001L, 3L, "один мільйон одна третя")]
    [DataRow(22L, 37L, "двадцять дві тридцять сьомих")]
    [DataRow(22L, 60L, "двадцять дві шістдесятих")]
    [DataRow(34L, 43L, "тридцять чотири сорок третіх")]
    [DataRow(23L, 3L, "двадцять три третіх")]
    [DataRow(24L, 100L, "двадцять чотири сотих")]
    [DataRow(11L, 12L, "одинадцять дванадцятих")]
    [DataRow(111L, 1000L, "сто одинадцять тисячних")]
    public void Ukrainian_CompoundNumerators(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("UK").ConvertFraction(numerator, denominator));

    // ===== BG compound numerators ====================================================================

    /// <summary>A compound ending in една takes the plural (ИБЕ-БАН <c>двайсет и един футболисти</c>; Клет <c>сто и една хилядни</c>).</summary>
    [TestMethod]
    [DataRow(21L, 37L, "двадесет и една тридесет и седми")]
    [DataRow(101L, 1000L, "сто и една хилядни")]
    [DataRow(21L, 100L, "двадесет и една стотни")]
    [DataRow(31L, 3L, "тридесет и една трети")]
    [DataRow(21L, 4L, "двадесет и една четвърти")]
    [DataRow(1_000_001L, 3L, "един милион и една трети")]
    [DataRow(22L, 3L, "двадесет и две трети")]
    [DataRow(24L, 100L, "двадесет и четири стотни")]
    [DataRow(111L, 1000L, "сто и единадесет хилядни")]
    public void Bulgarian_CompoundNumerators(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("BG").ConvertFraction(numerator, denominator));

    /// <summary>One alone keeps the singular and the noun <c>четвърт</c>.</summary>
    [TestMethod]
    public void Bulgarian_OneStaysSingular()
    {
        Assert.AreEqual("една четвърт", Get("BG").ConvertFraction(1, 4));
        Assert.AreEqual("една тридесет и седма", Get("BG").ConvertFraction(1, 37));
    }

    // ===== CS compound numerator agreement ===========================================================

    /// <summary>
    /// After a compound the noun is in the genitive plural and the numeral keeps its counting form (IJP: <c>dvacet
    /// jedna žáků</c>, <c>dvacet dva žáků</c>, "more common and more natural").
    /// </summary>
    [TestMethod]
    [DataRow(21L, 3L, "dvacet jedna třetin")]
    [DataRow(22L, 3L, "dvacet dva třetin")]
    [DataRow(23L, 3L, "dvacet tři třetin")]
    [DataRow(24L, 3L, "dvacet čtyři třetin")]
    [DataRow(31L, 100L, "třicet jedna setin")]
    [DataRow(42L, 100L, "čtyřicet dva setin")]
    [DataRow(101L, 3L, "sto jedna třetin")]
    [DataRow(122L, 7L, "sto dvacet dva sedmin")]
    [DataRow(1024L, 1000L, "tisíc dvacet čtyři tisícin")]
    [DataRow(1_000_001L, 3L, "jeden milion jedna třetin")]
    [DataRow(2L, 3L, "dvě třetiny")]
    [DataRow(4L, 3L, "čtyři třetiny")]
    public void Czech_CompoundNumerators(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator));

    /// <summary>The fraction nouns of the IJP added by NTS-39 (13–20, round tens, 100 000, 10^9).</summary>
    [TestMethod]
    [DataRow(1L, 13L, "jedna třináctina")]
    [DataRow(2L, 14L, "dvě čtrnáctiny")]
    [DataRow(5L, 15L, "pět patnáctin")]
    [DataRow(1L, 16L, "jedna šestnáctina")]
    [DataRow(3L, 17L, "tři sedmnáctiny")]
    [DataRow(7L, 18L, "sedm osmnáctin")]
    [DataRow(1L, 19L, "jedna devatenáctina")]
    [DataRow(1L, 20L, "jedna dvacetina")]
    [DataRow(2L, 30L, "dvě třicetiny")]
    [DataRow(1L, 40L, "jedna čtyřicetina")]
    [DataRow(1L, 50L, "jedna padesátina")]
    [DataRow(7L, 60L, "sedm šedesátin")]
    [DataRow(1L, 70L, "jedna sedmdesátina")]
    [DataRow(1L, 80L, "jedna osmdesátina")]
    [DataRow(1L, 90L, "jedna devadesátina")]
    [DataRow(1L, 100_000L, "jedna stotisícina")]
    [DataRow(1L, 1_000_000_000L, "jedna miliardtina")]
    [DataRow(5L, 1_000_000_000L, "pět miliardtin")]
    public void Czech_NewFractionNouns(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator));

    // ===== CS productive denominator reading =========================================================

    /// <summary>
    /// Without a fraction noun: <c>lomeno</c> + instrumental (SSJČ <c>dvě lomeno třemi</c>, SSČ <c>pět lomeno dvěma</c>),
    /// every part declined (<c>třemi sty šedesáti pěti</c>) but <c>sto</c> before another numeral.
    /// </summary>
    [TestMethod]
    [DataRow(1L, 22L, "jedna lomeno dvaceti dvěma")]
    [DataRow(1L, 23L, "jedna lomeno dvaceti třemi")]
    [DataRow(2L, 24L, "dvě lomeno dvaceti čtyřmi")]
    [DataRow(5L, 25L, "pět lomeno dvaceti pěti")]
    [DataRow(1L, 26L, "jedna lomeno dvaceti šesti")]
    [DataRow(1L, 27L, "jedna lomeno dvaceti sedmi")]
    [DataRow(1L, 28L, "jedna lomeno dvaceti osmi")]
    [DataRow(1L, 29L, "jedna lomeno dvaceti devíti")]
    [DataRow(3L, 99L, "tři lomeno devadesáti devíti")]
    [DataRow(1L, 102L, "jedna lomeno sto dvěma")]
    [DataRow(5L, 120L, "pět lomeno sto dvaceti")]
    [DataRow(1L, 111L, "jedna lomeno sto jedenácti")]
    [DataRow(1L, 200L, "jedna lomeno dvěma sty")]
    [DataRow(2L, 365L, "dvě lomeno třemi sty šedesáti pěti")]
    [DataRow(1L, 400L, "jedna lomeno čtyřmi sty")]
    [DataRow(1L, 512L, "jedna lomeno pěti sty dvanácti")]
    [DataRow(4L, 999L, "čtyři lomeno devíti sty devadesáti devíti")]
    public void Czech_Lomeno(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator));

    /// <summary>A compound numerator before <c>lomeno</c> keeps its counting form, as in arithmetic (IJP <c>k tisíc sedm set dvacet dva korunám</c>).</summary>
    [TestMethod]
    public void Czech_Lomeno_CompoundNumeratorKeepsCountingForm()
    {
        Assert.AreEqual("dvacet dva lomeno dvaceti třemi", Get("CS").ConvertFraction(22, 23));
        Assert.AreEqual("dvacet jedna lomeno třiceti sedmi", Get("CS").ConvertFraction(21, 37));
        Assert.AreEqual("sto lomeno dvaceti třemi", Get("CS").ConvertFraction(100, 23));
    }

    /// <summary>
    /// The Czech reading still fails closed: a denominator ending in one after the tens or hundreds (bare <c>jedna</c>
    /// in the instrumental is not sourced), from 1000 without a noun.
    /// </summary>
    [TestMethod]
    [DataRow(1L, 21L)]
    [DataRow(1L, 31L)]
    [DataRow(1L, 101L)]
    [DataRow(1L, 121L)]
    [DataRow(1L, 1001L)]
    [DataRow(1L, 2023L)]
    [DataRow(1L, 10_000L)]
    [DataRow(1L, 1_000_001L)]
    public void Czech_StillClosed(long numerator, long denominator)
        => Assert.ThrowsExactly<NotSupportedException>(() => Get("CS").ConvertFraction(numerator, denominator));

    // ===== SK compound numerator agreement ===========================================================

    /// <summary>
    /// A compound keeps its base form for every gender and governs the genitive plural (Jarošová 2021 <c>dvadsaťdva
    /// žien</c>, also 102, 122; Páleníková 2026 <c>dvadsaťjeden žien</c>; Morfológia <c>dvadsaťdva jabĺk</c>).
    /// </summary>
    [TestMethod]
    [DataRow(21L, 3L, "dvadsaťjeden tretín")]
    [DataRow(22L, 3L, "dvadsaťdva tretín")]
    [DataRow(23L, 3L, "dvadsaťtri tretín")]
    [DataRow(24L, 3L, "dvadsaťštyri tretín")]
    [DataRow(31L, 3L, "tridsaťjeden tretín")]
    [DataRow(102L, 3L, "sto dva tretín")]
    [DataRow(121L, 3L, "sto dvadsaťjeden tretín")]
    [DataRow(21L, 100L, "dvadsaťjeden stotín")]
    [DataRow(1_000_001L, 3L, "jeden milión jeden tretín")]
    public void Slovak_CompoundNumerators(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    // ===== SK fraction noun paradigms ================================================================

    /// <summary>The Krátky slovník paradigms (nominative plural = genitive singular; genitive plural).</summary>
    [TestMethod]
    [DataRow(2L, 2L, "dve polovice")]
    [DataRow(5L, 2L, "päť polovíc")]
    [DataRow(2L, 4L, "dve štvrtiny")]
    [DataRow(5L, 4L, "päť štvrtín")]
    [DataRow(2L, 5L, "dve pätiny")]
    [DataRow(5L, 5L, "päť pätín")]
    [DataRow(2L, 6L, "dve šestiny")]
    [DataRow(5L, 6L, "päť šestín")]
    [DataRow(2L, 7L, "dve sedminy")]
    [DataRow(5L, 7L, "päť sedmín")]
    [DataRow(3L, 8L, "tri osminy")]
    [DataRow(5L, 9L, "päť devätín")]
    [DataRow(3L, 10L, "tri desatiny")]
    [DataRow(5L, 12L, "päť dvanástin")]
    [DataRow(5L, 16L, "päť šestnástin")]
    [DataRow(1L, 17L, "jedna sedemnástina")]
    [DataRow(5L, 19L, "päť devätnástin")]
    [DataRow(5L, 20L, "päť dvadsatín")]
    [DataRow(5L, 30L, "päť tridsatín")]
    [DataRow(2L, 40L, "dve štyridsatiny")]
    [DataRow(5L, 60L, "päť šesťdesiatin")]
    [DataRow(1L, 70L, "jedna sedemdesiatina")]
    [DataRow(5L, 80L, "päť osemdesiatin")]
    [DataRow(2L, 100L, "dve stotiny")]
    [DataRow(5L, 100L, "päť stotín")]
    [DataRow(2L, 1000L, "dve tisíciny")]
    [DataRow(5L, 1000L, "päť tisícin")]
    [DataRow(2L, 1_000_000L, "dve milióntiny")]
    [DataRow(5L, 1_000_000L, "päť milióntin")]
    public void Slovak_NounParadigms(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    /// <summary>The nouns formed by the rule of the Morfológia (cardinal + <c>-ina</c>; <c>miliardtina</c> on the extended stem).</summary>
    [TestMethod]
    [DataRow(1L, 11L, "jedna jedenástina")]
    [DataRow(2L, 13L, "dve trinástiny")]
    [DataRow(5L, 14L, "päť štrnástin")]
    [DataRow(1L, 15L, "jedna pätnástina")]
    [DataRow(5L, 18L, "päť osemnástin")]
    [DataRow(1L, 50L, "jedna päťdesiatina")]
    [DataRow(5L, 90L, "päť deväťdesiatin")]
    [DataRow(1L, 1_000_000_000L, "jedna miliardtina")]
    public void Slovak_RuleFormedNouns(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    // ===== SK lomené fallback ========================================================================

    /// <summary>
    /// Ondrus (1968) §10: <c>lomené</c> + instrumental of a declinable numeral (<c>jedna lomené troma</c>), the compound
    /// as two words (Jarošová <c>dvadsiatimi dvoma</c>, Morfológia <c>deväťdesiatimi piatimi</c>), the numerator in
    /// its absolute form.
    /// </summary>
    [TestMethod]
    [DataRow(1L, 22L, "jedna lomené dvadsiatimi dvoma")]
    [DataRow(1L, 23L, "jedna lomené dvadsiatimi troma")]
    [DataRow(1L, 24L, "jedna lomené dvadsiatimi štyrmi")]
    [DataRow(2L, 25L, "dva lomené dvadsiatimi piatimi")]
    [DataRow(1L, 26L, "jedna lomené dvadsiatimi šiestimi")]
    [DataRow(1L, 27L, "jedna lomené dvadsiatimi siedmimi")]
    [DataRow(1L, 28L, "jedna lomené dvadsiatimi ôsmimi")]
    [DataRow(1L, 29L, "jedna lomené dvadsiatimi deviatimi")]
    [DataRow(1L, 67L, "jedna lomené šesťdesiatimi siedmimi")]
    [DataRow(5L, 95L, "päť lomené deväťdesiatimi piatimi")]
    [DataRow(22L, 23L, "dvadsaťdva lomené dvadsiatimi troma")]
    public void Slovak_LomeneInstrumental(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    /// <summary>
    /// From 100 only the last component is declined, the words before it keep their absolute form (Slovenská reč 43,
    /// 1978: <c>jeden lomené sto štyridsiatimi dvoma</c>, <c>dvanásť lomené štyridsiatimi piatimi</c>; the numerator
    /// of one stays the absolute <c>jedna</c> of Ondrus 1968).
    /// </summary>
    [TestMethod]
    [DataRow(1L, 142L, "jedna lomené sto štyridsiatimi dvoma")]
    [DataRow(12L, 45L, "dvanásť lomené štyridsiatimi piatimi")]
    [DataRow(1L, 120L, "jedna lomené sto dvadsiatimi")]
    [DataRow(1L, 111L, "jedna lomené sto jedenástimi")]
    [DataRow(1L, 102L, "jedna lomené sto dvoma")]
    [DataRow(2L, 365L, "dva lomené tristo šesťdesiatimi piatimi")]
    [DataRow(1L, 999L, "jedna lomené deväťsto deväťdesiatimi deviatimi")]
    [DataRow(1L, 1042L, "jedna lomené tisíc štyridsiatimi dvoma")]
    [DataRow(1L, 2003L, "jedna lomené dvetisíc troma")]
    [DataRow(1L, 123_456L, "jedna lomené stodvadsaťtritisíc štyristo päťdesiatimi šiestimi")]
    public void Slovak_LomeneInstrumental_LastComponent(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    /// <summary>
    /// A denominator whose last component is indeclinable (a compound with <c>-jeden</c>, <c>sto</c>, <c>tisíc</c> and
    /// their compounds, Ondrus §8: the last component decides) or a bare final <c>jeden</c> takes <c>lomené číslom</c>
    /// + the absolute form (Ondrus: <c>jedna lomené číslom dvadsaťjeden</c>, <c>jedna lomené číslom sto</c>).
    /// </summary>
    [TestMethod]
    [DataRow(1L, 21L, "jedna lomené číslom dvadsaťjeden")]
    [DataRow(1L, 91L, "jedna lomené číslom deväťdesiatjeden")]
    [DataRow(1L, 101L, "jedna lomené číslom sto jeden")]
    [DataRow(1L, 200L, "jedna lomené číslom dvesto")]
    [DataRow(1L, 221L, "jedna lomené číslom dvesto dvadsaťjeden")]
    [DataRow(3L, 500L, "tri lomené číslom päťsto")]
    [DataRow(1L, 1001L, "jedna lomené číslom tisíc jeden")]
    [DataRow(1L, 1100L, "jedna lomené číslom tisíc sto")]
    [DataRow(1L, 2000L, "jedna lomené číslom dvetisíc")]
    [DataRow(1L, 21_000L, "jedna lomené číslom dvadsaťjedentisíc")]
    public void Slovak_LomeneCislom(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    /// <summary>
    /// Over the whole supported domain, <c>číslom</c> is chosen exactly when the last component is indeclinable (a
    /// round hundred or thousand, a final one other than eleven), and both readings reuse the cardinal of the
    /// configuration: whole after <c>číslom</c>, all but its last word before the instrumental.
    /// </summary>
    [TestMethod]
    public void Slovak_Lomene_ChoiceFollowsTheLastComponent()
    {
        var sk = Get("SK");
        for (int denominator = 2; denominator <= 999_999; denominator += denominator < 3000 ? 1 : 997)
        {
            string reading = sk.ConvertFraction(1, denominator);
            if (!reading.StartsWith("jedna lomené ", StringComparison.Ordinal))
                continue;
            string cardinal = sk.Convert(denominator);
            int rest = denominator % 100;
            bool indeclinable = rest == 0 || (rest % 10 == 1 && rest != 11);
            if (indeclinable)
            {
                Assert.AreEqual("jedna lomené číslom " + cardinal, reading, denominator.ToString());
                continue;
            }
            int lastSpace = cardinal.LastIndexOf(' ');
            string head = lastSpace < 0 ? string.Empty : cardinal[..(lastSpace + 1)];
            Assert.IsTrue(reading.StartsWith("jedna lomené " + head, StringComparison.Ordinal), denominator + ": " + reading);
            Assert.IsFalse(reading.Contains("číslom", StringComparison.Ordinal), denominator + ": " + reading);
        }
    }

    /// <summary>Above 999 999 without a noun the Slovak reading fails closed.</summary>
    [TestMethod]
    public void Slovak_StillClosedAboveTheDomain()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => Get("SK").ConvertFraction(1, 1_000_001));
        Assert.ThrowsExactly<NotSupportedException>(() => Get("SK").ConvertFraction(1, 2_000_000));
    }

    // ===== zero numerator ============================================================================

    /// <summary>
    /// Slovak <c>nula</c> governs the genitive plural (Morfológia: <c>nula stupňov</c>); without a noun, and in Czech,
    /// zero takes the slash reading (<c>nula lomeno třemi</c>). UK and BG have no sourced reading.
    /// </summary>
    [TestMethod]
    public void ZeroNumerator()
    {
        Assert.AreEqual("nula tretín", Get("SK").ConvertFraction(0, 3));
        Assert.AreEqual("nula stotín", Get("SK").ConvertFraction(0, 100));
        Assert.AreEqual("nula polovíc", Get("SK").ConvertFraction(0, 2));
        Assert.AreEqual("nula lomené dvadsiatimi troma", Get("SK").ConvertFraction(0, 23));
        Assert.AreEqual("nula lomeno třemi", Get("CS").ConvertFraction(0, 3));
        Assert.AreEqual("nula lomeno dvěma", Get("CS").ConvertFraction(0, 2));
        Assert.AreEqual("nula lomeno sto dvaceti", Get("CS").ConvertFraction(0, 120));
        Assert.AreEqual("nula lomeno stem", Get("CS").ConvertFraction(0, 100), "sto alone is stem (IJP)");
        Assert.ThrowsExactly<NotSupportedException>(() => Get("CS").ConvertFraction(0, 1000), "no lomeno reading from 1000");
        Assert.ThrowsExactly<NotSupportedException>(() => Get("UK").ConvertFraction(0, 3));
        Assert.ThrowsExactly<NotSupportedException>(() => Get("BG").ConvertFraction(0, 3));
    }

    // ===== denominator one ===========================================================================

    /// <summary>No language has a sourced reading of a written <c>n/1</c>; the fraction is not reduced.</summary>
    [TestMethod]
    [DataRow("UK")]
    [DataRow("BG")]
    [DataRow("CS")]
    [DataRow("SK")]
    public void DenominatorOne_StaysClosed(string culture)
    {
        var converter = Get(culture);
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(1, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(2, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(5, 1));
    }

    /// <summary>The supplied numerator and denominator are read as written, not reduced.</summary>
    [TestMethod]
    public void NoReduction()
    {
        Assert.AreEqual("dve štvrtiny", Get("SK").ConvertFraction(2, 4));
        Assert.AreEqual("päť pätín", Get("SK").ConvertFraction(5, 5));
        Assert.AreEqual("sto třetin", Get("CS").ConvertFraction(100, 3));
        Assert.AreEqual("дві четвертих", Get("UK").ConvertFraction(2, 4));
    }

    // ===== oblique cases / deliberate closures =======================================================

    /// <summary>
    /// The oblique cases stay closed: §107 declines the numerator as a cardinal (<c>двом третім</c>), and the UK, CS and
    /// SK cardinals have no case declension (moved to NTS-40).
    /// </summary>
    [TestMethod]
    [DataRow("UK", "case=родовий")]
    [DataRow("UK", "case=давальний")]
    [DataRow("UK", "case=орудний")]
    [DataRow("CS", "case=genitiv")]
    [DataRow("CS", "case=dativ")]
    [DataRow("CS", "case=instrumentál")]
    [DataRow("SK", "case=genitív")]
    [DataRow("SK", "case=datív")]
    [DataRow("SK", "case=inštrumentál")]
    public void ObliqueCases_StayClosed(string culture, string variant)
    {
        var converter = Get(culture);
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(21, 37, variant));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(1, 3, variant));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(0, 3, variant));
    }

    // ===== caller variants ===========================================================================

    /// <summary>The form the fraction imposes on its numerator wins over the caller's gender, for the numerator only.</summary>
    [TestMethod]
    public void CallerVariants_FractionLocalFormWins()
    {
        Assert.AreEqual("двадцять одна тридцять сьома", Get("UK").ConvertFraction(21, 37, "gender=чоловічий"));
        Assert.AreEqual("двадесет и една тридесет и седми", Get("BG").ConvertFraction(21, 37, "gender=masculine"));
        Assert.AreEqual("dvacet dva třetin", Get("CS").ConvertFraction(22, 3, "gender=ženský"));
        Assert.AreEqual("dvě lomeno dvaceti třemi", Get("CS").ConvertFraction(2, 23, "gender=mužský"));
        Assert.AreEqual("dvadsaťdva tretín", Get("SK").ConvertFraction(22, 3, "gender=ženský"));
        Assert.AreEqual("dva lomené dvadsiatimi troma", Get("SK").ConvertFraction(2, 23, "gender=ženský"));
        Assert.AreEqual("jedna lomené dvadsiatimi troma", Get("SK").ConvertFraction(1, 23, "gender=mužský"));
        Assert.AreEqual("dva", Get("SK").Convert(2), "no forced gender leaks into the next conversion");
        Assert.AreEqual("dvacet dva", Get("CS").Convert(22, "gender=standalone"));
        Assert.AreEqual("dvacet dvě", Get("CS").Convert(22, "gender=ženský"));
    }

    // ===== Number parity =============================================================================

    /// <summary><c>Convert(Number)</c> reads the same irreducible fraction as <c>ConvertFraction</c>.</summary>
    [TestMethod]
    [DataRow("UK", 21L, 37L)]
    [DataRow("UK", 101L, 1000L)]
    [DataRow("BG", 21L, 37L)]
    [DataRow("BG", 101L, 1000L)]
    [DataRow("CS", 22L, 3L)]
    [DataRow("CS", 1L, 23L)]
    [DataRow("CS", 2L, 365L)]
    [DataRow("CS", 1L, 13L)]
    [DataRow("SK", 22L, 3L)]
    [DataRow("SK", 5L, 7L)]
    [DataRow("SK", 1L, 23L)]
    [DataRow("SK", 1L, 101L)]
    [DataRow("SK", -21L, 37L)]
    public void NumberParity(string culture, long numerator, long denominator)
    {
        var converter = Get(culture);
        Assert.AreEqual(converter.ConvertFraction(numerator, denominator), converter.Convert(new Number(numerator, denominator)));
    }

    /// <summary><c>Number</c> keeps its own normalization: zero is the integer zero, a reducible value is reduced.</summary>
    [TestMethod]
    public void Number_KeepsItsNormalization()
    {
        Assert.AreEqual("nula", Get("SK").Convert(new Number(0, 3)));
        Assert.AreEqual("jedna polovica", Get("SK").Convert(new Number(2, 4)));
        Assert.AreEqual("päť", Get("SK").Convert(new Number(5, 1)));
    }

    // ===== signs =====================================================================================

    /// <summary>The minus wraps the whole fraction once, whichever term carried it; zero has no sign.</summary>
    [TestMethod]
    [DataRow("UK", 21L, 37L, "мінус двадцять одна тридцять сьома")]
    [DataRow("BG", 21L, 37L, "минус двадесет и една тридесет и седми")]
    [DataRow("CS", 22L, 3L, "minus dvacet dva třetin")]
    [DataRow("CS", 1L, 23L, "minus jedna lomeno dvaceti třemi")]
    [DataRow("SK", 21L, 3L, "mínus dvadsaťjeden tretín")]
    [DataRow("SK", 1L, 23L, "mínus jedna lomené dvadsiatimi troma")]
    [DataRow("SK", 1L, 101L, "mínus jedna lomené číslom sto jeden")]
    public void Signs(string culture, long numerator, long denominator, string negative)
    {
        var converter = Get(culture);
        string positive = negative[(negative.IndexOf(' ') + 1)..];
        Assert.AreEqual(negative, converter.ConvertFraction(-numerator, denominator));
        Assert.AreEqual(negative, converter.ConvertFraction(numerator, -denominator));
        Assert.AreEqual(positive, converter.ConvertFraction(-numerator, -denominator));
    }

    /// <summary>A zero numerator with a negative denominator has no sign.</summary>
    [TestMethod]
    public void Signs_Zero()
    {
        Assert.AreEqual("nula lomeno třemi", Get("CS").ConvertFraction(0, -3));
        Assert.AreEqual("nula tretín", Get("SK").ConvertFraction(0, -3));
    }

    // ===== regression ================================================================================

    /// <summary>The NTS-30H readings and the other surfaces are unchanged.</summary>
    [TestMethod]
    public void Regression()
    {
        Assert.AreEqual("одна третя", Get("UK").ConvertFraction(1, 3));
        Assert.AreEqual("дві третіх", Get("UK").ConvertFraction(2, 3));
        Assert.AreEqual("една втора", Get("BG").ConvertFraction(1, 2));
        Assert.AreEqual("jedna třetina", Get("CS").ConvertFraction(1, 3));
        Assert.AreEqual("pět sedmin", Get("CS").ConvertFraction(5, 7));
        Assert.AreEqual("dva miliony třetin", Get("CS").ConvertFraction(2_000_000, 3));
        Assert.AreEqual("jedna tretina", Get("SK").ConvertFraction(1, 3));
        Assert.AreEqual("dva milióny tretín", Get("SK").ConvertFraction(2_000_000, 3));
        Assert.AreEqual("dvadsaťdva", Get("SK").Convert(22));
        Assert.AreEqual("dvacet dva", Get("CS").Convert(22));
        Assert.AreEqual("třetí", Get("CS").ConvertOrdinal(3));
        Assert.AreEqual("jedna celá dva", Get("CS").Convert(1.2m));
        Assert.AreEqual("двадесет и едно", Get("BG").Convert(21));
        Assert.AreEqual("третя", Get("UK").ConvertOrdinal(3, "gender=жіночий"));
    }

    /// <summary>A large numerator keeps the cardinal pipeline (scale nouns) before the new readings.</summary>
    [TestMethod]
    public void LargeNumerators()
    {
        Assert.AreEqual("п'ять мільйонів двадцять третіх", Get("UK").ConvertFraction(5_000_000, 23));
        Assert.AreEqual("pět milionů lomeno dvaceti třemi", Get("CS").ConvertFraction(5_000_000, 23));
        Assert.AreEqual("päť miliónov lomené dvadsiatimi troma", Get("SK").ConvertFraction(5_000_000, 23));
        Assert.AreEqual(Get("BG").Convert(new BigInteger(21_000_001), "gender=feminine") + " трети", Get("BG").ConvertFraction(21_000_001, 3));
    }
}
