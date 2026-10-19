using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-42 — audit of the UK, CS and SK fraction readings NTS-40/41 left closed. Every family was reproduced on master
/// <c>4cd45b72</c> as a deliberate fail-closed reading of the language plugin (<see cref="NotSupportedException"/>), never
/// as a silently wrong text, and none of them goes through a language-specific path of the generic engine. These tests
/// pin each closure over every oblique case, the neighbouring values that stay open (so a later fix cannot widen or
/// narrow a family unnoticed), and the generic boundary between the engine and the plugin. Audit:
/// <c>DONE-2026-10-19(1).md</c>.
/// </summary>
[TestClass]
public class NumberToStringNts42Tests
{
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
    /// Family A: a compound numerator ending in <c>jedna</c> has no sourced oblique form (the IJP gives only <i>dvacet
    /// jeden žák</i> / <i>dvacet jedna žáků</i>), after the tens, the hundreds or a scale group, whatever the caller's gender.
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
        foreach (string gender in new[] { "gender=mužský", "gender=ženský", "gender=střední" })
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
    /// Family B: a multiplier ending in one after the tens or hundreds (<c>dvacet jedna tisíc</c>) has no sourced form
    /// outside the nominative and accusative, before <c>tisíc</c>, <c>milion</c> and <c>miliarda</c> alike: in the
    /// oblique cases of a noun fraction and in the instrumental of the <c>lomeno</c> reading.
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
    /// Family C: a numerator ending in a bare <c>jeden</c> after a larger group has no sourced oblique form (Morfológia
    /// 326 covers the indeclinable <c>-jeden</c> compounds, not <c>sto jeden</c>); the accusative is its nominative.
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
    /// Family D: no source declines the <c>lomeno</c>/<c>lomené</c> phrase, whose denominator is already an
    /// instrumental; a fraction without a fraction noun fails closed in every case but the nominative, the accusative
    /// included, while its nominative reading stays open.
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
    /// Family E: no source declines <c>нуль</c>/<c>nula</c> with a fraction; a zero numerator fails closed in every oblique
    /// case, with or without a fraction noun, while the sourced nominatives stay open (CS <c>nula lomeno třemi</c>,
    /// SK <c>nula tretín</c>; UK has none, NTS-39).
    /// </summary>
    [TestMethod]
    public void E_ZeroNumerator_ClosedInEveryObliqueCase()
    {
        foreach (long denominator in new[] { 2L, 3L, 23L, 100L, 1000L })
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
        foreach (BigInteger numerator in new[] { first, first + 1, 2 * first, BigInteger.Pow(10, 15), BigInteger.Pow(10, 24) })
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
        foreach (BigInteger denominator in new[] { first, first + 1, 2 * first, BigInteger.Pow(10, 15) + 3 })
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
        foreach (BigInteger numerator in new[] { 2 * BigInteger.Pow(10, 12), 5 * BigInteger.Pow(10, 15), BigInteger.Pow(10, 24) })
            Assert.AreEqual(Get(language).Convert(numerator) + suffix, Get(language).ConvertFraction(numerator, 3), numerator.ToString());
    }
}
