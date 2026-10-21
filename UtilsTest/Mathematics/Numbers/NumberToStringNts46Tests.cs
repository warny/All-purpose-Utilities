using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-46 — the Ukrainian NTS-42 families E (zero numerator) and F (numerators from 10^12).
/// <list type="bullet">
/// <item><b>E/UK</b>: <c>нуль</c> is a masculine noun of the second declension (VESUM: <c>нуля</c>, <c>нулеві</c>,
/// <c>нулем</c>, <c>нулю</c>; Курс лекцій «Лінгвістичні основи документознавства», ПНТУ, Полтава 2015, p. 132: "нуль,
/// тисяча, мільйон, мільярд, трильйон, квадрильйон … відмінюються як іменники"). As a noun it governs the genitive plural
/// of what it counts, which then stays unchanged in every case (same source, p. 135: "Якщо десятковий дріб починається на
/// нуль, то частина цілих не відмінюється": <i>нулем цілих</i>, against <i>трьома цілими</i>). The denominator ordinal is
/// therefore the genitive plural after every case of <c>нуль</c> (<c>нулем третіх</c>), as after <c>тисяча</c> or
/// <c>мільйон</c> (NTS-40). The forms of <c>нуль</c> are the first ones of VESUM, the policy of the plugin
/// (<c>мільйонові</c>, <c>мільйоні</c>): dative <c>нулеві</c>, locative <c>нулю</c>.</item>
/// <item><b>F/UK</b>: the generated <c>трильйон</c>, <c>квадрильйон</c>, <c>квінтильйон</c> … are masculine nouns of the
/// second declension, hard group, declined like <c>мільйон</c> (VESUM: <c>трильйона</c>, <c>трильйонові</c>,
/// <c>трильйоном</c>, <c>трильйоні</c>; pl. <c>трильйонів</c>, <c>трильйонам</c>, <c>трильйонами</c>,
/// <c>трильйонах</c>). Their case forms are suffix fragments of the UK <c>&lt;GeneratedScaleForm&gt;</c> read through the
/// converter; the plugin holds no generated name.</item>
/// </list>
/// The expected strings apply these rules; they are not quotations.
/// </summary>
[TestClass]
public class NumberToStringNts46Tests
{
    private static readonly string[] s_obliqueCases = ["case=родовий", "case=давальний", "case=знахідний", "case=орудний", "case=місцевий"];
    private static readonly string[] s_allCases = ["case=називний", .. s_obliqueCases];
    private static readonly string[] s_genders = ["gender=чоловічий", "gender=жіночий", "gender=середній"];

    /// <summary>Gets the Ukrainian converter.</summary>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Uk() => NumberToStringConverter.GetConverter("UK");

    /// <summary>Returns <paramref name="multiplier"/> × 10^<paramref name="exponent"/>.</summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <returns>The value.</returns>
    private static BigInteger E(long multiplier, int exponent) => multiplier * BigInteger.Pow(10, exponent);

    // ===== E — zero numerator =========================================================================

    /// <summary>Zero over 3 in every case: <c>нуль</c> declined, the denominator the genitive plural.</summary>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow("case=називний", "нуль третіх")]
    [DataRow("case=родовий", "нуля третіх")]
    [DataRow("case=давальний", "нулеві третіх")]
    [DataRow("case=знахідний", "нуль третіх")]
    [DataRow("case=орудний", "нулем третіх")]
    [DataRow("case=місцевий", "нулю третіх")]
    public void E_ZeroOverThree_EveryCase(string variant, string expected)
        => Assert.AreEqual(expected, Uk().ConvertFraction(0, 3, variant));

    /// <summary>Without a case variant the zero fraction is the nominative.</summary>
    [TestMethod]
    public void E_ZeroWithoutCase_IsTheNominative()
    {
        Assert.AreEqual("нуль третіх", Uk().ConvertFraction(0, 3));
        Assert.AreEqual("нуль п'ятих", Uk().ConvertFraction(0, 5));
    }

    /// <summary>
    /// Other denominators: the hard (<c>п'ятий</c>, <c>десятий</c>, <c>другий</c>) and soft (<c>третій</c>) paradigms, a
    /// compound (<c>двадцять перший</c>, only its last part declined) and a one-word round ordinal (<c>тисячний</c>).
    /// </summary>
    /// <param name="denominator">The denominator.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(5L, "case=родовий", "нуля п'ятих")]
    [DataRow(5L, "case=давальний", "нулеві п'ятих")]
    [DataRow(5L, "case=орудний", "нулем п'ятих")]
    [DataRow(10L, "case=називний", "нуль десятих")]
    [DataRow(10L, "case=орудний", "нулем десятих")]
    [DataRow(10L, "case=місцевий", "нулю десятих")]
    [DataRow(2L, "case=давальний", "нулеві других")]
    [DataRow(21L, "case=орудний", "нулем двадцять перших")]
    [DataRow(23L, "case=родовий", "нуля двадцять третіх")]
    [DataRow(1000L, "case=місцевий", "нулю тисячних")]
    public void E_ZeroOverOtherDenominators(long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Uk().ConvertFraction(0, denominator, variant));

    /// <summary>The caller's gender does not change a zero fraction: <c>нуль</c> has one gender, the fraction words theirs.</summary>
    [TestMethod]
    public void E_GenderVariantsDoNotChangeZero()
    {
        foreach (string variant in s_allCases)
        {
            string reading = Uk().ConvertFraction(0, 3, variant);
            foreach (string gender in s_genders)
                Assert.AreEqual(reading, Uk().ConvertFraction(0, 3, variant, gender), $"{variant} {gender}");
        }
    }

    /// <summary>A negative zero does not exist: <c>-0</c> is zero; a denominator of one stays closed with a zero numerator.</summary>
    [TestMethod]
    public void E_ZeroBoundaries()
    {
        Assert.AreEqual("нуль третіх", Uk().ConvertFraction(BigInteger.Zero, 3));
        Assert.ThrowsExactly<NotSupportedException>(() => Uk().ConvertFraction(0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => Uk().ConvertFraction(0, 1, "case=орудний"));
    }

    /// <summary>The non-zero numerators keep their NTS-40 agreement: the plural of the case after two, three, five.</summary>
    [TestMethod]
    public void E_NonZeroNeighboursUnchanged()
    {
        Assert.AreEqual("однією третьою", Uk().ConvertFraction(1, 3, "case=орудний"));
        Assert.AreEqual("двома третіми", Uk().ConvertFraction(2, 3, "case=орудний"));
        Assert.AreEqual("п'ятьма третіми", Uk().ConvertFraction(5, 3, "case=орудний"));
        Assert.AreEqual("тисячею третіх", Uk().ConvertFraction(1000, 3, "case=орудний"));
    }

    // ===== F — numerators from 10^12 ===================================================================

    /// <summary>The last value below the generated scales keeps its NTS-40 reading.</summary>
    [TestMethod]
    public void F_LastMilliardValueUnchanged()
        => Assert.AreEqual(
            "дев'ятьмастами дев'яноста дев'ятьма мільярдами дев'ятьмастами дев'яноста дев'ятьма мільйонами дев'ятьмастами дев'яноста дев'ятьма тисячами дев'ятьмастами дев'яноста дев'ятьма третіми",
            Uk().ConvertFraction(E(1, 12) - 1, 3, "case=орудний"));

    /// <summary>10^12 in every oblique case: the noun governs the genitive plural of the denominator.</summary>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow("case=родовий", "одного трильйона третіх")]
    [DataRow("case=давальний", "одному трильйонові третіх")]
    [DataRow("case=знахідний", "один трильйон третіх")]
    [DataRow("case=орудний", "одним трильйоном третіх")]
    [DataRow("case=місцевий", "одному трильйоні третіх")]
    public void F_Trillion(string variant, string expected)
        => Assert.AreEqual(expected, Uk().ConvertFraction(E(1, 12), 3, variant));

    /// <summary>10^12 + 1 ends in one: the denominator is the feminine singular of the case (NTS-39/40).</summary>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow("case=родовий", "одного трильйона однієї третьої")]
    [DataRow("case=давальний", "одному трильйонові одній третій")]
    [DataRow("case=знахідний", "один трильйон одну третю")]
    [DataRow("case=орудний", "одним трильйоном однією третьою")]
    [DataRow("case=місцевий", "одному трильйоні одній третій")]
    public void F_TrillionPlusOne(string variant, string expected)
        => Assert.AreEqual(expected, Uk().ConvertFraction(E(1, 12) + 1, 3, variant));

    /// <summary>The multipliers of the generated scales follow the §105 rules of every other multiplier.</summary>
    /// <param name="multiplier">The multiplier of 10^12.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(2L, "case=родовий", "двох трильйонів третіх")]
    [DataRow(2L, "case=давальний", "двом трильйонам третіх")]
    [DataRow(2L, "case=знахідний", "два трильйони третіх")]
    [DataRow(2L, "case=орудний", "двома трильйонами третіх")]
    [DataRow(2L, "case=місцевий", "двох трильйонах третіх")]
    [DataRow(3L, "case=давальний", "трьом трильйонам третіх")]
    [DataRow(3L, "case=знахідний", "три трильйони третіх")]
    [DataRow(5L, "case=родовий", "п'яти трильйонів третіх")]
    [DataRow(5L, "case=знахідний", "п'ять трильйонів третіх")]
    [DataRow(5L, "case=орудний", "п'ятьма трильйонами третіх")]
    [DataRow(21L, "case=родовий", "двадцяти одного трильйона третіх")]
    [DataRow(21L, "case=давальний", "двадцяти одному трильйонові третіх")]
    [DataRow(21L, "case=знахідний", "двадцять один трильйон третіх")]
    [DataRow(21L, "case=орудний", "двадцятьма одним трильйоном третіх")]
    [DataRow(21L, "case=місцевий", "двадцяти одному трильйоні третіх")]
    [DataRow(22L, "case=родовий", "двадцяти двох трильйонів третіх")]
    [DataRow(22L, "case=знахідний", "двадцять два трильйони третіх")]
    [DataRow(22L, "case=орудний", "двадцятьма двома трильйонами третіх")]
    [DataRow(101L, "case=родовий", "ста одного трильйона третіх")]
    [DataRow(101L, "case=знахідний", "сто один трильйон третіх")]
    [DataRow(101L, "case=орудний", "ста одним трильйоном третіх")]
    public void F_TrillionMultipliers(long multiplier, string variant, string expected)
        => Assert.AreEqual(expected, Uk().ConvertFraction(E(multiplier, 12), 3, variant));

    /// <summary>Higher generated scales: the same suffix fragments on each engine prefix.</summary>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(15, "case=давальний", "одному квадрильйонові третіх")]
    [DataRow(15, "case=родовий", "одного квадрильйона третіх")]
    [DataRow(18, "case=орудний", "одним квінтильйоном третіх")]
    [DataRow(18, "case=місцевий", "одному квінтильйоні третіх")]
    [DataRow(24, "case=місцевий", "одному септильйоні третіх")]
    [DataRow(30, "case=родовий", "одного нонильйона третіх")]
    public void F_HigherScales(int exponent, string variant, string expected)
        => Assert.AreEqual(expected, Uk().ConvertFraction(E(1, exponent), 3, variant));

    /// <summary>A numerator with several scales and units: every part declined (§105.6).</summary>
    /// <param name="variant">The case variant.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow("case=родовий", "двох квадрильйонів трьох трильйонів п'яти третіх")]
    [DataRow("case=давальний", "двом квадрильйонам трьом трильйонам п'яти третім")]
    [DataRow("case=знахідний", "два квадрильйони три трильйони п'ять третіх")]
    [DataRow("case=орудний", "двома квадрильйонами трьома трильйонами п'ятьма третіми")]
    [DataRow("case=місцевий", "двох квадрильйонах трьох трильйонах п'яти третіх")]
    public void F_SeveralScales(string variant, string expected)
        => Assert.AreEqual(expected, Uk().ConvertFraction(E(2, 15) + E(3, 12) + 5, 3, variant));

    /// <summary>A generated scale followed by the static ones.</summary>
    [TestMethod]
    public void F_GeneratedThenStaticScales()
    {
        Assert.AreEqual("одному трильйоні одному мільярді третіх", Uk().ConvertFraction(E(1, 12) + E(1, 9), 3, "case=місцевий"));
        Assert.AreEqual("одним трильйоном тисячею третіх", Uk().ConvertFraction(E(1, 12) + 1000, 3, "case=орудний"));
    }

    /// <summary>
    /// The morphology is carried by the suffix fragments, not by whole words: for three different engine prefixes, every
    /// singular and plural case form is the nominative singular with the same ending.
    /// </summary>
    /// <param name="exponent">The power of ten (трильйон, квадрильйон, квінтильйон).</param>
    [TestMethod]
    [DataRow(12)]
    [DataRow(15)]
    [DataRow(18)]
    public void F_SuffixCarriesTheMorphology(int exponent)
    {
        string name = Uk().Convert(E(1, exponent))["один ".Length..];
        string[] singular = ["одного " + name + "а", "одному " + name + "ові", "один " + name, "одним " + name + "ом", "одному " + name + "і"];
        string[] plural = ["двох " + name + "ів", "двом " + name + "ам", "два " + name + "и", "двома " + name + "ами", "двох " + name + "ах"];
        for (int i = 0; i < s_obliqueCases.Length; i++)
        {
            Assert.AreEqual(singular[i] + " третіх", Uk().ConvertFraction(E(1, exponent), 3, s_obliqueCases[i]), s_obliqueCases[i]);
            Assert.AreEqual(plural[i] + " третіх", Uk().ConvertFraction(E(2, exponent), 3, s_obliqueCases[i]), s_obliqueCases[i]);
        }
    }

    /// <summary>The accusative of an inanimate quantity is its nominative: it equals the cardinal pipeline from 10^12.</summary>
    [TestMethod]
    public void F_AccusativeMatchesTheCardinalPipeline()
    {
        BigInteger[] numerators = [E(1, 12), E(2, 12), E(5, 12), E(22, 12), E(2, 15), E(5, 15) + E(3, 12) + 7, E(1, 24), E(3, 30) + E(2, 12)];
        foreach (BigInteger numerator in numerators)
            Assert.AreEqual(Uk().ConvertFraction(numerator, 3), Uk().ConvertFraction(numerator, 3, "case=знахідний"), numerator.ToString());
    }

    /// <summary>
    /// The plugin holds no generated name: past the 999th Conway prefix (10^6024, <c>більсептильйон</c> in the UK spelling)
    /// the declined noun is the very name the engine generates, with the suffix's case ending.
    /// </summary>
    [TestMethod]
    public void F_FractionReadsTheEngineName()
    {
        BigInteger value = BigInteger.Pow(10, 6024);
        string name = Uk().Convert(value)["один ".Length..];
        Assert.AreEqual("більсептильйон", name);
        Assert.AreEqual("одному " + name + "ові третіх", Uk().ConvertFraction(value, 3, "case=давальний"));
        Assert.AreEqual("одному більсептильйонові третіх", Uk().ConvertFraction(value, 3, "case=давальний"));
        Assert.AreEqual("двома мільнільйонами третіх", Uk().ConvertFraction(E(2, 3003), 3, "case=орудний"));
    }

    /// <summary>
    /// The public <see cref="IFractionLanguageSpecifics"/> overload, called without the converter, has no generated scale
    /// noun: it declines up to the milliards and fails closed from 10^12, as the CS and SK plugins do.
    /// </summary>
    [TestMethod]
    public void F_PublicOverloadWithoutTheConverter()
    {
        IFractionLanguageSpecifics uk = new UkrainianOrdinalLanguageSpecifics();
        var dative = new Dictionary<string, string> { ["case"] = "давальний" };
        Assert.IsFalse(uk.TryGetFractionDenominator(E(1, 12), 3, dative, out _));
        Assert.IsTrue(uk.TryGetFractionDenominator(E(1, 9), 3, dative, out var reading));
        Assert.AreEqual("третіх", reading!.Text);
        Assert.IsTrue(uk.TryGetFractionDenominator(BigInteger.Zero, 3, dative, out reading));
        Assert.AreEqual("третіх", reading!.Text);
    }

    /// <summary>The nominative from 10^12 keeps going through the cardinal pipeline (NTS-42 F characterization).</summary>
    [TestMethod]
    public void F_NominativeUnchanged()
    {
        Assert.AreEqual("один трильйон третіх", Uk().ConvertFraction(E(1, 12), 3));
        Assert.AreEqual("два трильйони третіх", Uk().ConvertFraction(E(2, 12), 3));
        Assert.AreEqual("один трильйон одна третя", Uk().ConvertFraction(E(1, 12) + 1, 3));
    }

    /// <summary>The cardinals are untouched by the new case keys of the generated scale form.</summary>
    /// <param name="multiplier">The multiplier of 10^12.</param>
    /// <param name="expected">The expected cardinal.</param>
    [TestMethod]
    [DataRow(1L, "один трильйон")]
    [DataRow(2L, "два трильйони")]
    [DataRow(5L, "п'ять трильйонів")]
    [DataRow(21L, "двадцять один трильйон")]
    [DataRow(22L, "двадцять два трильйони")]
    public void F_CardinalsUnchanged(long multiplier, string expected)
        => Assert.AreEqual(expected, Uk().Convert(E(multiplier, 12)));

    // ===== E + F =======================================================================================

    /// <summary>
    /// Zero and the generated scales together: both read in every case over the same denominators, and both make the
    /// denominator the genitive plural after their noun (<c>нулем п'ятих</c>, <c>трильйоном п'ятих</c>).
    /// </summary>
    [TestMethod]
    public void EF_ZeroAndGeneratedScalesTogether()
    {
        long[] denominators = [2, 3, 5, 10, 21, 1000];
        foreach (long denominator in denominators)
        {
            foreach (string variant in s_allCases)
            {
                string zero = Uk().ConvertFraction(0, denominator, variant);
                string trillion = Uk().ConvertFraction(E(1, 12), denominator, variant);
                string zeroDenominator = zero[(zero.IndexOf(' ') + 1)..];
                Assert.IsTrue(trillion.EndsWith(" " + zeroDenominator), $"{denominator} {variant}: {zero} / {trillion}");
            }
        }
        Assert.AreEqual("нулем п'ятих", Uk().ConvertFraction(0, 5, "case=орудний"));
        Assert.AreEqual("одним трильйоном п'ятих", Uk().ConvertFraction(E(1, 12), 5, "case=орудний"));
    }

    /// <summary>The other languages are not touched: CS still fails closed on a zero in an oblique case.</summary>
    [TestMethod]
    public void OtherLanguagesUnchanged()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => NumberToStringConverter.GetConverter("CS").ConvertFraction(0, 3, "case=dativ"));
        Assert.AreEqual("nula tretinám", NumberToStringConverter.GetConverter("SK").ConvertFraction(0, 3, "case=datív"));
    }
}
