using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-42 — audit of the UK, CS and SK fraction readings NTS-40/41 left closed. Every family was reproduced on master
/// <c>4cd45b72</c> as a deliberate fail-closed reading of the language plugin (<see cref="NotSupportedException"/>), never
/// as a silently wrong text, and none of them goes through a language-specific path of the generic engine.
/// <para>
/// These are <b>characterization tests of the current implementation</b>, not a grammatical specification: a closure
/// pinned here means "no reading is implemented yet", not "the language has no such form". They pin each closure over
/// every oblique case, the neighbouring values that stay open (so a later change cannot widen or narrow a family
/// unnoticed), and the generic boundary between the engine and the plugin. A change that opens a family on a sourced
/// reading is expected to update the matching test. The level of evidence of each family (documented, partially
/// documented, not established) is in <c>DONE-2026-10-19(1).md</c>.
/// </para>
/// </summary>
[TestClass]
public class NumberToStringNts42Tests
{
    private static readonly string[] s_czechGenders = ["gender=mužský", "gender=ženský", "gender=střední"];
    private static readonly string[] s_czechObliqueCases = ["case=genitiv", "case=dativ", "case=lokál", "case=instrumentál"];
    private static readonly string[] s_slovakObliqueCases = ["case=genitív", "case=datív", "case=lokál", "case=inštrumentál"];
    private static readonly string[] s_ukrainianObliqueCases = ["case=родовий", "case=давальний", "case=знахідний", "case=орудний", "case=місцевий"];

    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Asserts that a fraction fails closed under each of the given case variants.</summary>
    /// <param name="language">The culture.</param>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="cases">The case variants.</param>
    private static void AssertClosed(string language, BigInteger numerator, BigInteger denominator, IEnumerable<string> cases)
    {
        NumberToStringConverter converter = Get(language);
        foreach (string variant in cases)
            Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(numerator, denominator, variant),
                $"{language} {numerator}/{denominator} {variant}");
    }

    // ===== A — CS compound numerator ending in jedna ================================================

    /// <summary>
    /// Family A (characterization): a compound numerator ending in <c>jedna</c> is not implemented in the oblique cases,
    /// after the tens, the hundreds or a scale group, whatever the caller's gender. Partially documented: the IJP gives
    /// the nominative agreements (<i>dvacet jeden žák</i> / <i>dvacet jedna žáků</i>) and three declension strategies
    /// for multi-word numerals, but no source chooses one for a compound ending in <i>jedna</i> with a fraction noun.
    /// </summary>
    [TestMethod]
    [DataRow(21L, 3L)]
    [DataRow(31L, 100L)]
    [DataRow(91L, 3L)]
    [DataRow(101L, 3L)]
    [DataRow(121L, 1000L)]
    [DataRow(1001L, 3L)]
    [DataRow(1021L, 100L)]
    [DataRow(1_000_001L, 3L)]
    public void A_CzechCompoundEndingInJedna_ClosedInTheObliqueCases(long numerator, long denominator)
    {
        AssertClosed("CS", numerator, denominator, s_czechObliqueCases);
        foreach (string gender in s_czechGenders)
            Assert.ThrowsExactly<NotSupportedException>(() => Get("CS").ConvertFraction(numerator, denominator, "case=dativ", gender));
    }

    /// <summary>
    /// Family A, neighbours that stay open: a simple <c>jedna</c> and <c>jedenáct</c> decline, and the accusative of a
    /// compound keeps its counting form, as the nominative does.
    /// </summary>
    [TestMethod]
    [DataRow(1L, 3L, "case=dativ", "jedné třetině")]
    [DataRow(11L, 3L, "case=dativ", "jedenácti třetinám")]
    [DataRow(21L, 3L, "case=akuzativ", "dvacet jedna třetin")]
    [DataRow(1001L, 3L, "case=akuzativ", "tisíc jedna třetin")]
    public void A_CzechNeighboursStayOpen(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator, variant));

    // ===== B — CS multiplier ending in one ==========================================================

    /// <summary>
    /// Family B (characterization): a multiplier ending in one after the tens or hundreds (<c>dvacet jedna tisíc</c>) is
    /// not implemented outside the nominative and accusative, before <c>tisíc</c>, <c>milion</c> and <c>miliarda</c>
    /// alike: in the oblique cases of a noun fraction and in the instrumental of the <c>lomeno</c> reading. Partially
    /// documented: the IJP rules for the scale nouns and multi-word numerals apply, but even the nominative of the
    /// multiplier varies (<i>dvacet jedna tisíc</i> / <i>dvacet jeden tisíc</i>), and no oblique example is sourced.
    /// </summary>
    [TestMethod]
    [DataRow(21_000L)]
    [DataRow(31_000L)]
    [DataRow(101_000L)]
    [DataRow(121_000L)]
    [DataRow(21_000_000L)]
    [DataRow(101_000_000L)]
    [DataRow(21_000_000_000L)]
    [DataRow(21_000_000_001L)]
    public void B_CzechMultiplierEndingInOne_Closed(long value)
    {
        AssertClosed("CS", value, 3, s_czechObliqueCases);
        Assert.ThrowsExactly<NotSupportedException>(() => Get("CS").ConvertFraction(1, value), "lomeno " + value);
    }

    /// <summary>
    /// Family B, neighbours that stay open: the accusative keeps the counting form, a multiplier ending in two declines,
    /// and a multiplier of one alone is the bare scale noun.
    /// </summary>
    [TestMethod]
    [DataRow(21_000L, 3L, "case=akuzativ", "dvacet jedna tisíc třetin")]
    [DataRow(22_000L, 3L, "case=dativ", "dvaceti dvěma tisícům třetin")]
    [DataRow(1L, 22_000L, "", "jedna lomeno dvaceti dvěma tisíci")]
    [DataRow(1L, 1_001_000L, "", "jedna lomeno jedním milionem tisícem")]
    public void B_CzechNeighboursStayOpen(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, variant.Length == 0
            ? Get("CS").ConvertFraction(numerator, denominator)
            : Get("CS").ConvertFraction(numerator, denominator, variant));

    // ===== C — SK numerator ending in a bare jeden ==================================================

    /// <summary>
    /// Family C (characterization): a numerator ending in a bare <c>jeden</c> after a larger group is not implemented in
    /// the oblique cases; the accusative is its nominative. Documented as a rule since the audit revision: Jarošová
    /// (Slovenská reč 86, 2021, 223 and 226) states that <i>stojeden / sto jeden</i> and the higher compounds ending in
    /// <i>jeden</i> are indeclinable like <i>dvadsaťjeden</i>, and PSP 1953 (88–89, as quoted there) attests <i>so
    /// stojeden družstvami</i>. Opening it is a separate behaviour change, out of this audit.
    /// </summary>
    [TestMethod]
    [DataRow(101L)]
    [DataRow(1001L)]
    [DataRow(2001L)]
    [DataRow(1_000_001L)]
    [DataRow(1_000_000_001L)]
    public void C_SlovakBareJeden_ClosedInTheObliqueCases(long numerator)
    {
        AssertClosed("SK", numerator, 3, s_slovakObliqueCases);
        Assert.AreEqual(Get("SK").ConvertFraction(numerator, 3), Get("SK").ConvertFraction(numerator, 3, "case=akuzatív"));
    }

    /// <summary>
    /// Family C, neighbours that stay open: the <c>-jeden</c> compounds are indeclinable with the noun in the case
    /// (Morfológia 326: <i>ku štyridsaťjeden družstvám</i>), a simple <c>jeden</c> declines, and the nominative
    /// <c>lomené</c> reading of a bare final <c>jeden</c> takes <c>číslom</c>.
    /// </summary>
    [TestMethod]
    [DataRow(21L, 3L, "case=datív", "dvadsaťjeden tretinám")]
    [DataRow(121L, 3L, "case=inštrumentál", "sto dvadsaťjeden tretinami")]
    [DataRow(1L, 3L, "case=datív", "jednej tretine")]
    [DataRow(1L, 101L, "", "jedna lomené číslom sto jeden")]
    public void C_SlovakNeighboursStayOpen(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, variant.Length == 0
            ? Get("SK").ConvertFraction(numerator, denominator)
            : Get("SK").ConvertFraction(numerator, denominator, variant));

    // ===== D — lomeno / lomené under an oblique case ================================================

    /// <summary>
    /// Family D (characterization): a fraction read with <c>lomeno</c>/<c>lomené</c> (no fraction noun) is not
    /// implemented in any case but the nominative, the accusative included, while its nominative reading stays open.
    /// Not established: <i>lomeno</i> itself is invariable and governs the instrumental (SSJČ <i>dvě lomeno třemi</i>),
    /// but no source shows the whole <i>x lomeno y</i> phrase in a sentence that requires an oblique case.
    /// </summary>
    [TestMethod]
    [DataRow("CS", 1L, 21L)]
    [DataRow("CS", 1L, 23L)]
    [DataRow("CS", 2L, 365L)]
    [DataRow("CS", 21L, 23L)]
    [DataRow("CS", 1L, 1001L)]
    [DataRow("CS", 1L, 2_000_000L)]
    [DataRow("SK", 1L, 23L)]
    [DataRow("SK", 1L, 142L)]
    [DataRow("SK", 1L, 200L)]
    [DataRow("SK", 1L, 1001L)]
    [DataRow("SK", 1L, 2_000_000L)]
    public void D_SlashReading_ClosedInEveryObliqueCase(string language, long numerator, long denominator)
    {
        string[] cases = language == "CS"
            ? [.. s_czechObliqueCases, "case=akuzativ"]
            : [.. s_slovakObliqueCases, "case=akuzatív"];
        AssertClosed(language, numerator, denominator, cases);
        StringAssert.Contains(Get(language).ConvertFraction(numerator, denominator), language == "CS" ? " lomeno " : " lomené ");
    }

    // ===== E — zero numerator under an oblique case =================================================

    /// <summary>
    /// Family E (characterization): a zero numerator is not implemented in any oblique case, with or without a fraction
    /// noun, while the current nominatives stay open (CS <c>nula lomeno třemi</c>, SK <c>nula tretín</c>; UK has none,
    /// NTS-39). The lexical declension of <c>нуль</c>/<c>nula</c> is documented; its combination with a fraction noun or
    /// a <i>lomeno</i> phrase in an oblique case is not.
    /// </summary>
    [TestMethod]
    public void E_ZeroNumerator_ClosedInEveryObliqueCase()
    {
        long[] denominators = [2, 3, 23, 100, 1000];
        foreach (long denominator in denominators)
        {
            AssertClosed("CS", 0, denominator, [.. s_czechObliqueCases, "case=akuzativ"]);
            AssertClosed("SK", 0, denominator, [.. s_slovakObliqueCases, "case=akuzatív"]);
            AssertClosed("UK", 0, denominator, s_ukrainianObliqueCases);
        }
        Assert.AreEqual("nula lomeno třemi", Get("CS").ConvertFraction(0, 3));
        Assert.AreEqual("nula tretín", Get("SK").ConvertFraction(0, 3));
        Assert.ThrowsExactly<NotSupportedException>(() => Get("UK").ConvertFraction(0, 3));
    }

    /// <summary>
    /// Family E, engine boundary: the generic engine does not special-case zero. A zero numerator reaches the language's
    /// <see cref="IFractionLanguageSpecifics"/> with the caller's case, exactly as a non-zero one does, and the closure is
    /// the plugin's answer turned into the engine's <see cref="NotSupportedException"/>.
    /// </summary>
    [TestMethod]
    [DataRow("UK", "case=давальний", "давальний")]
    [DataRow("CS", "case=dativ", "dativ")]
    [DataRow("SK", "case=datív", "datív")]
    public void E_ZeroReachesThePluginWithTheCase(string culture, string variant, string caseValue)
    {
        NumberToStringConverter source = Get(culture);
        var recorder = new RecordingFractionSpecifics(source.LanguageSpecifics);
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source) { LanguageSpecifics = recorder });

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(0, 3, variant));
        Assert.AreEqual(1, recorder.Calls.Count, "the plugin is asked exactly once");
        Assert.AreEqual(BigInteger.Zero, recorder.Calls[0].Numerator);
        Assert.AreEqual(caseValue, recorder.Calls[0].Variants["case"]);
        Assert.IsFalse(recorder.Calls[0].Answered);

        Assert.AreEqual(Get(culture).ConvertFraction(2, 3, variant), converter.ConvertFraction(2, 3, variant));
        Assert.AreEqual(2, recorder.Calls.Count);
        Assert.AreEqual(new BigInteger(2), recorder.Calls[1].Numerator, "a non-zero numerator takes the same path");
        Assert.AreEqual(caseValue, recorder.Calls[1].Variants["case"]);
        Assert.IsTrue(recorder.Calls[1].Answered);
    }

    /// <summary>One call of <see cref="IFractionLanguageSpecifics.TryGetFractionDenominator"/>.</summary>
    /// <param name="Numerator">The numerator passed by the engine.</param>
    /// <param name="Variants">A copy of the variants passed by the engine.</param>
    /// <param name="Answered">Whether the plugin returned a reading.</param>
    private sealed record FractionCall(BigInteger Numerator, Dictionary<string, string> Variants, bool Answered);

    /// <summary>Wraps a built-in fraction plugin and records every fraction request the engine makes.</summary>
    /// <param name="inner">The built-in language specifics.</param>
    private sealed class RecordingFractionSpecifics(INumberToStringLanguageSpecifics inner)
        : INumberToStringLanguageSpecifics, IFractionLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        /// <summary>Gets the fraction requests, in call order.</summary>
        public List<FractionCall> Calls { get; } = [];

        /// <inheritdoc />
        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
            => ((IOrdinalLanguageSpecifics)inner).TryConvertOrdinal(number, activeVariants, out result);

        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text) => inner.FinalizeWriting(languageIdentifier, text);

        /// <inheritdoc />
        public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
            out FractionDenominatorReading? reading)
        {
            bool answered = ((IFractionLanguageSpecifics)inner).TryGetFractionDenominator(numerator, denominator, variants, out reading);
            Calls.Add(new FractionCall(numerator, new Dictionary<string, string>(variants), answered));
            return answered;
        }
    }

    // ===== F — 10^12 and above ======================================================================

    /// <summary>
    /// Family F: the declined-cardinal builders stop below the first generated scale (<c>bilion</c>, <c>bilión</c>,
    /// <c>трильйон</c>): the last value of the milliards still declines, the first generated one fails closed, in every
    /// oblique case.
    /// </summary>
    [TestMethod]
    [DataRow("CS")]
    [DataRow("SK")]
    [DataRow("UK")]
    public void F_ObliqueNumerators_BoundAtTenToTheTwelve(string language)
    {
        string[] cases = language switch
        {
            "CS" => s_czechObliqueCases,
            "SK" => s_slovakObliqueCases,
            _ => s_ukrainianObliqueCases,
        };
        BigInteger last = 999_999_999_999;
        foreach (string variant in cases)
            Assert.IsFalse(string.IsNullOrWhiteSpace(Get(language).ConvertFraction(last, 3, variant)), variant);
        BigInteger first = BigInteger.Pow(10, 12);
        BigInteger[] numerators = [first, first + 1, 2 * first, BigInteger.Pow(10, 15), BigInteger.Pow(10, 24)];
        foreach (BigInteger numerator in numerators)
            AssertClosed(language, numerator, 3, cases);
    }

    /// <summary>Family F: the CS and SK slash denominators decline up to 999 999 999 999 and fail closed from 10^12.</summary>
    [TestMethod]
    [DataRow("CS")]
    [DataRow("SK")]
    public void F_SlashDenominators_BoundAtTenToTheTwelve(string language)
    {
        StringAssert.StartsWith(Get(language).ConvertFraction(1, 999_999_999_999), language == "CS" ? "jedna lomeno " : "jedna lomené ");
        BigInteger first = BigInteger.Pow(10, 12);
        BigInteger[] denominators = [first, first + 1, 2 * first, BigInteger.Pow(10, 15) + 3];
        foreach (BigInteger denominator in denominators)
            Assert.ThrowsExactly<NotSupportedException>(() => Get(language).ConvertFraction(BigInteger.One, denominator), denominator.ToString());
    }

    /// <summary>
    /// Family F: from 10^12 the nominative numerator keeps going through the cardinal pipeline, whose generated Conway
    /// scale nouns (NTS-30C) are the only spelling of these names: the fraction reads the very cardinal of
    /// <c>Convert</c>, and no plugin builder spells a scale noun of its own.
    /// </summary>
    [TestMethod]
    [DataRow("CS", " třetin")]
    [DataRow("SK", " tretín")]
    [DataRow("UK", " третіх")]
    public void F_NominativeNumerators_UseTheGeneratedScales(string language, string suffix)
    {
        BigInteger[] numerators = [2 * BigInteger.Pow(10, 12), 5 * BigInteger.Pow(10, 15), BigInteger.Pow(10, 24)];
        foreach (BigInteger numerator in numerators)
            Assert.AreEqual(Get(language).Convert(numerator) + suffix, Get(language).ConvertFraction(numerator, 3), numerator.ToString());
    }
}
