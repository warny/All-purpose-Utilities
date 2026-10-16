using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30H: <c>ConvertFraction</c> for Ukrainian, Bulgarian, Czech and Slovak. The connector reading "numerator,
/// connector, denominator cardinal" (<c>один на тисяча</c>, <c>jedna ku tisíc</c>) is replaced by each language's
/// sourced fraction reading through the generic <see cref="IFractionLanguageSpecifics"/> extension point; values
/// outside the sourced domain fail closed. Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-30H section.
/// </summary>
[TestClass]
public class NumberToStringNts30HTests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    // ===== generic extension point ===================================================================

    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>A synthetic fraction plugin: "Nths" forcing gender b on the numerator, no reading for a denominator of 7.</summary>
    private sealed class SyntheticFractions : INumberToStringLanguageSpecifics, IFractionLanguageSpecifics
    {
        /// <summary>The number of finalizations.</summary>
        public int Finalizations;

        /// <summary>The last variants received.</summary>
        public IReadOnlyDictionary<string, string>? LastVariants;

        /// <summary>The variants forced on the numerator.</summary>
        public ForcedVariantSet Forced = ForcedVariantSet.Parse("gender=b");

        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            Interlocked.Increment(ref Finalizations);
            return text;
        }

        /// <inheritdoc />
        public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
            out FractionDenominatorReading? reading)
        {
            LastVariants = variants;
            reading = denominator == 7 ? null : new FractionDenominatorReading($"{denominator}ths", Forced);
            return reading != null;
        }
    }

    /// <summary>Builds a synthetic converter using <paramref name="specifics"/>.</summary>
    /// <param name="specifics">The language specifics.</param>
    /// <param name="extra">Additional elements of the Language (Fractions …).</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter BuildSynthetic(INumberToStringLanguageSpecifics specifics, string extra = "")
    {
        string name = $"NTS30H-{Guid.NewGuid():N}";
        NumberToStringConverter.RegisterLanguageSpecifics(name, specifics);
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u}ty" buildString="{u}ty *" />"""));
        string document = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="2" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" fractionSeparator="over" maxNumber="9999">
                <Culture>{name}</Culture>
                <Groups><Group level="1">{units}</Group><Group level="2">{tens}</Group></Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="hundred" /></StaticNames></NumberScale>
                <Variants>
                  <Dimension name="gender" values="a,b" />
                  <Variant type="gender" variant="b"><Replacement oldValue="one" newValue="oneB" scope="LastWord" /><Replacement oldValue="two" newValue="twoB" scope="LastWord" /></Variant>
                </Variants>
                <LanguageSpecifics>{name}</LanguageSpecifics>
                {extra}
              </Language>
            </Numbers>
            """;
        return NumberToStringConverter.ReadConfiguration(document)[name];
    }

    /// <summary>The plugin spells the denominator; its forced variants reach the numerator only, over the caller's.</summary>
    [TestMethod]
    public void Generic_DenominatorAndNumeratorForcedVariants()
    {
        var specifics = new SyntheticFractions();
        var converter = BuildSynthetic(specifics);

        Assert.AreEqual("oneB 3ths", converter.ConvertFraction(1, 3));
        Assert.AreEqual("twoB 3ths", converter.ConvertFraction(2, 3, "gender=a"));
        Assert.AreEqual("a", specifics.LastVariants!["gender"], "the plugin receives the caller's query, defaults filled in");
        Assert.AreEqual("five 9ths", converter.ConvertFraction(5, 9));
        Assert.AreEqual("twoty oneB 100ths", converter.ConvertFraction(21, 100), "the numerator is a whole cardinal");
    }

    /// <summary>No reading means no fallback to the connector reading: the converter fails closed.</summary>
    [TestMethod]
    public void Generic_NoReading_FailsClosed()
    {
        var converter = BuildSynthetic(new SyntheticFractions());
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(1, 7));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.Convert(new Number(1, 7)));
    }

    /// <summary>The engine keeps the zero-denominator rejection, the sign normalization and one finalization.</summary>
    [TestMethod]
    public void Generic_SignZeroAndSingleFinalization()
    {
        var specifics = new SyntheticFractions();
        var converter = BuildSynthetic(specifics);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.ConvertFraction(1, 0));
        Assert.AreEqual("minus oneB 3ths", converter.ConvertFraction(-1, 3));
        Assert.AreEqual("minus oneB 3ths", converter.ConvertFraction(1, -3));
        Assert.AreEqual("oneB 3ths", converter.ConvertFraction(-1, -3));
        int before = specifics.Finalizations;
        converter.ConvertFraction(2, 3);
        Assert.AreEqual(before + 1, specifics.Finalizations);
    }

    /// <summary><c>Convert(Number)</c> uses the same reading as <c>ConvertFraction</c>.</summary>
    [TestMethod]
    public void Generic_NumberParity()
    {
        var converter = BuildSynthetic(new SyntheticFractions());
        Assert.AreEqual(converter.ConvertFraction(2, 3), converter.Convert(new Number(2, 3)));
        Assert.AreEqual(converter.ConvertFraction(-2, 3), converter.Convert(new Number(-2, 3)));
    }

    /// <summary>Named power-of-ten fractions keep precedence over the plugin for <c>ConvertFraction</c>.</summary>
    [TestMethod]
    public void Generic_NamedFractionsKeepPrecedence()
    {
        var converter = BuildSynthetic(new SyntheticFractions(), """<Fractions><Fraction digits="2" string="percent" /></Fractions>""");
        Assert.AreEqual("three percent", converter.ConvertFraction(3, 100));
        Assert.AreEqual("three 9ths", converter.ConvertFraction(3, 9));
    }

    /// <summary>The forced variants are validated against the language: an unknown dimension or value is UNTS006.</summary>
    [TestMethod]
    public void Generic_UnknownForcedVariant_IsRejected()
    {
        var specifics = new SyntheticFractions { Forced = ForcedVariantSet.Parse("case=b") };
        var converter = BuildSynthetic(specifics);
        var error = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => converter.ConvertFraction(1, 3));
        Assert.AreEqual("UNTS006", error.ErrorCode);
    }

    /// <summary>A reading needs a denominator text; no forced variants means none.</summary>
    [TestMethod]
    public void Generic_ReadingValidation()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FractionDenominatorReading(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new FractionDenominatorReading(null!));
        Assert.IsTrue(new FractionDenominatorReading("x").NumeratorForcedVariants.IsEmpty);
    }

    // ===== Ukrainian =================================================================================

    /// <summary>Український правопис 2019, §107.1, verbatim (the ASCII apostrophe of the configuration).</summary>
    [TestMethod]
    [DataRow(1, 2, "одна друга")]
    [DataRow(1, 3, "одна третя")]
    [DataRow(1, 4, "одна четверта")]
    [DataRow(5, 2, "п'ять других")]
    [DataRow(2, 3, "дві третіх")]
    [DataRow(3, 4, "три четвертих")]
    [DataRow(4, 3, "чотири третіх")]
    [DataRow(3, 5, "три п'ятих")]
    [DataRow(2, 7, "дві сьомих")]
    [DataRow(9, 10, "дев'ять десятих")]
    public void Ukrainian_Pravopys107(int numerator, int denominator, string expected)
        => Assert.AreEqual(expected, Get("UK").ConvertFraction(numerator, denominator));

    /// <summary>The §107 rule applied to the other denominators the ordinals form.</summary>
    [TestMethod]
    [DataRow(5L, 7L, "п'ять сьомих")]
    [DataRow(1L, 6L, "одна шоста")]
    [DataRow(5L, 13L, "п'ять тринадцятих")]
    [DataRow(11L, 12L, "одинадцять дванадцятих")]
    [DataRow(2L, 25L, "дві двадцять п'ятих")]
    [DataRow(1L, 10L, "одна десята")]
    [DataRow(2L, 10L, "дві десятих")]
    [DataRow(5L, 10L, "п'ять десятих")]
    [DataRow(1L, 100L, "одна сота")]
    [DataRow(2L, 100L, "дві сотих")]
    [DataRow(5L, 100L, "п'ять сотих")]
    [DataRow(1L, 1000L, "одна тисячна")]
    [DataRow(2L, 1000L, "дві тисячних")]
    [DataRow(5L, 1000L, "п'ять тисячних")]
    [DataRow(1L, 101L, "одна сто перша")]
    [DataRow(1L, 1001L, "одна тисяча перша")]
    [DataRow(1L, 1_000_000L, "одна мільйонна")]
    [DataRow(2L, 1_000_000L, "дві мільйонних")]
    [DataRow(5L, 1_000_000L, "п'ять мільйонних")]
    [DataRow(1L, 1_000_001L, "одна мільйон перша")]
    [DataRow(22L, 37L, "двадцять дві тридцять сьомих")]
    [DataRow(2_000L, 3L, "дві тисячі третіх")]
    public void Ukrainian_Productive(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("UK").ConvertFraction(numerator, denominator));

    /// <summary>
    /// Outside the sourced domain the Ukrainian reading fails closed. The compound numerators ending in один were opened
    /// by NTS-39 (singular denominator, <see cref="NumberToStringNts39Tests"/>), the oblique cases by NTS-40
    /// (<see cref="NumberToStringNts40Nts41Tests"/>).
    /// </summary>
    [TestMethod]
    public void Ukrainian_FailsClosed()
    {
        var uk = Get("UK");
        Assert.AreEqual("двадцять одна тридцять сьома", uk.ConvertFraction(21, 37), "NTS-39");
        Assert.AreEqual("сто одна тисячна", uk.ConvertFraction(101, 1000), "NTS-39");
        Assert.AreEqual("однієї третьої", uk.ConvertFraction(1, 3, "case=родовий"), "NTS-40");
        Assert.ThrowsExactly<NotSupportedException>(() => uk.ConvertFraction(0, 3), "zero numerator");
        Assert.ThrowsExactly<NotSupportedException>(() => uk.ConvertFraction(0, 3, "case=родовий"), "zero numerator, oblique case");
        Assert.ThrowsExactly<NotSupportedException>(() => uk.ConvertFraction(5, 1), "denominator one");
        Assert.ThrowsExactly<NotSupportedException>(() => uk.ConvertFraction(1, 90_000), "ordinal declined by the plugin");
    }

    // ===== Bulgarian =================================================================================

    /// <summary>Institute for Bulgarian Language style guide and P. Kancheva (2016), verbatim.</summary>
    [TestMethod]
    [DataRow(1, 2, "една втора")]
    [DataRow(1, 8, "една осма")]
    [DataRow(2, 3, "две трети")]
    [DataRow(3, 4, "три четвърти")]
    [DataRow(1, 4, "една четвърт")]
    [DataRow(7, 8, "седем осми")]
    public void Bulgarian_Sourced(int numerator, int denominator, string expected)
        => Assert.AreEqual(expected, Get("BG").ConvertFraction(numerator, denominator));

    /// <summary>The same rule on the other denominators the ordinals form (the и of compounds included).</summary>
    [TestMethod]
    [DataRow(1L, 3L, "една трета")]
    [DataRow(5L, 7L, "пет седми")]
    [DataRow(1L, 6L, "една шеста")]
    [DataRow(5L, 13L, "пет тринадесети")]
    [DataRow(1L, 10L, "една десета")]
    [DataRow(2L, 10L, "две десети")]
    [DataRow(5L, 10L, "пет десети")]
    [DataRow(1L, 100L, "една стотна")]
    [DataRow(2L, 100L, "две стотни")]
    [DataRow(5L, 100L, "пет стотни")]
    [DataRow(1L, 1000L, "една хилядна")]
    [DataRow(2L, 1000L, "две хилядни")]
    [DataRow(5L, 1000L, "пет хилядни")]
    [DataRow(1L, 101L, "една сто и първа")]
    [DataRow(1L, 1001L, "една хиляда и първа")]
    [DataRow(1L, 1_000_000L, "една милионна")]
    [DataRow(2L, 1_000_000L, "две милионни")]
    [DataRow(22L, 37L, "двадесет и две тридесет и седми")]
    [DataRow(25L, 4L, "двадесет и пет четвърти")]
    public void Bulgarian_Productive(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("BG").ConvertFraction(numerator, denominator));

    /// <summary>
    /// Outside the sourced domain the Bulgarian reading fails closed. The compound numerators ending in една were opened
    /// by NTS-39 (plural denominator, <see cref="NumberToStringNts39Tests"/>).
    /// </summary>
    [TestMethod]
    public void Bulgarian_FailsClosed()
    {
        var bg = Get("BG");
        Assert.AreEqual("двадесет и една тридесет и седми", bg.ConvertFraction(21, 37), "NTS-39");
        Assert.ThrowsExactly<NotSupportedException>(() => bg.ConvertFraction(0, 3));
        Assert.ThrowsExactly<NotSupportedException>(() => bg.ConvertFraction(5, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => bg.ConvertFraction(1, 2_000_000), "ordinal declined by the plugin");
    }

    // ===== Czech =====================================================================================

    /// <summary>The IJP fraction nouns: 1 → nominative singular, 2–4 → nominative plural, 5+ → genitive plural.</summary>
    [TestMethod]
    [DataRow(1L, 2L, "jedna polovina")]
    [DataRow(1L, 3L, "jedna třetina")]
    [DataRow(2L, 3L, "dvě třetiny")]
    [DataRow(3L, 4L, "tři čtvrtiny")]
    [DataRow(3L, 5L, "tři pětiny")]
    [DataRow(5L, 7L, "pět sedmin")]
    [DataRow(7L, 8L, "sedm osmin")]
    [DataRow(11L, 12L, "jedenáct dvanáctin")]
    [DataRow(1L, 10L, "jedna desetina")]
    [DataRow(1L, 100L, "jedna setina")]
    [DataRow(2L, 1000L, "dvě tisíciny")]
    [DataRow(1L, 1000L, "jedna tisícina")]
    [DataRow(5L, 1000L, "pět tisícin")]
    [DataRow(1L, 1_000_000L, "jedna miliontina")]
    [DataRow(100L, 3L, "sto třetin")]
    public void Czech_FractionNouns(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator));

    /// <summary>
    /// <c>ku</c> reads a ratio and is never produced; 1/13 (IJP noun), compound numerators (genitive plural) and zero
    /// (<c>lomeno</c>) were opened by NTS-39 (<see cref="NumberToStringNts39Tests"/>), the denominators ending in one by
    /// NTS-41 and the oblique cases of the fraction nouns by NTS-40 (<see cref="NumberToStringNts40Nts41Tests"/>); the
    /// oblique cases of a compound numerator ending in <c>jedna</c> and of the <c>lomeno</c> reading still fail closed.
    /// </summary>
    [TestMethod]
    public void Czech_FailsClosed()
    {
        var cs = Get("CS");
        Assert.AreEqual("jedna třináctina", cs.ConvertFraction(1, 13), "NTS-39");
        Assert.AreEqual("jedna lomeno sto jednou", cs.ConvertFraction(1, 101), "NTS-41");
        Assert.AreEqual("jedna lomeno tisícem jednou", cs.ConvertFraction(1, 1001), "NTS-41");
        Assert.AreEqual("dvacet jedna setin", cs.ConvertFraction(21, 100), "NTS-39");
        Assert.AreEqual("dvacet dva třetin", cs.ConvertFraction(22, 3), "NTS-39");
        Assert.AreEqual("nula lomeno třemi", cs.ConvertFraction(0, 3), "NTS-39");
        Assert.AreEqual("jedné třetiny", cs.ConvertFraction(1, 3, "case=genitiv"), "NTS-40");
        Assert.ThrowsExactly<NotSupportedException>(() => cs.ConvertFraction(21, 100, "case=genitiv"), "compound ending in jedna");
        Assert.ThrowsExactly<NotSupportedException>(() => cs.ConvertFraction(1, 23, "case=genitiv"), "lomeno is nominative only");
    }

    // ===== Slovak ====================================================================================

    /// <summary>Ondrus (1968) and KSSJ/PSP <c>tretina -y -tín</c>.</summary>
    [TestMethod]
    [DataRow(1L, 2L, "jedna polovica")]
    [DataRow(1L, 3L, "jedna tretina")]
    [DataRow(2L, 3L, "dve tretiny")]
    [DataRow(4L, 3L, "štyri tretiny")]
    [DataRow(5L, 3L, "päť tretín")]
    [DataRow(1L, 5L, "jedna pätina")]
    [DataRow(1L, 100L, "jedna stotina")]
    [DataRow(1L, 1000L, "jedna tisícina")]
    public void Slovak_FractionNouns(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    /// <summary>
    /// <c>ku</c> (a ratio) is no longer produced. The forms NTS-30H left closed were opened by NTS-39 (noun paradigms,
    /// compound numerators, <c>lomené</c>; <see cref="NumberToStringNts39Tests"/>), the oblique cases of the fraction nouns
    /// by NTS-40 (<see cref="NumberToStringNts40Nts41Tests"/>); the denominator one and the oblique cases of a numerator
    /// ending in a bare <c>jeden</c> and of the <c>lomené</c> reading still fail closed.
    /// </summary>
    [TestMethod]
    public void Slovak_FailsClosed()
    {
        var sk = Get("SK");
        Assert.AreEqual("päť sedmín", sk.ConvertFraction(5, 7), "NTS-39");
        Assert.AreEqual("dve stotiny", sk.ConvertFraction(2, 100), "NTS-39");
        Assert.AreEqual("jedna trinástina", sk.ConvertFraction(1, 13), "NTS-39");
        Assert.AreEqual("jedna lomené číslom tisíc jeden", sk.ConvertFraction(1, 1001), "NTS-39");
        Assert.AreEqual("dvadsaťjeden tretín", sk.ConvertFraction(21, 3), "NTS-39");
        Assert.AreEqual("jednej tretiny", sk.ConvertFraction(1, 3, "case=genitív"), "NTS-40");
        Assert.ThrowsExactly<NotSupportedException>(() => sk.ConvertFraction(5, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => sk.ConvertFraction(101, 3, "case=genitív"), "bare final jeden");
        Assert.ThrowsExactly<NotSupportedException>(() => sk.ConvertFraction(1, 23, "case=genitív"), "lomené is nominative only");
    }

    // ===== composites ================================================================================

    /// <summary>Signs: the minus of the language wraps the whole fraction once, whichever term carried it.</summary>
    [TestMethod]
    [DataRow("UK", "мінус одна третя", "одна третя")]
    [DataRow("BG", "минус една трета", "една трета")]
    [DataRow("CS", "minus jedna třetina", "jedna třetina")]
    [DataRow("SK", "mínus jedna tretina", "jedna tretina")]
    public void Signs(string culture, string negative, string positive)
    {
        var converter = Get(culture);
        Assert.AreEqual(negative, converter.ConvertFraction(-1, 3));
        Assert.AreEqual(negative, converter.ConvertFraction(1, -3));
        Assert.AreEqual(positive, converter.ConvertFraction(-1, -3));
    }

    /// <summary><c>Convert(Number)</c> reads the same fraction (irreducible values).</summary>
    [TestMethod]
    [DataRow("UK", 2L, 3L)]
    [DataRow("UK", 1L, 1000L)]
    [DataRow("BG", 7L, 8L)]
    [DataRow("BG", -2L, 3L)]
    [DataRow("CS", 5L, 7L)]
    [DataRow("CS", 1L, 1000L)]
    [DataRow("SK", 2L, 3L)]
    [DataRow("SK", -1L, 100L)]
    public void NumberParity(string culture, long numerator, long denominator)
    {
        var converter = Get(culture);
        Assert.AreEqual(converter.ConvertFraction(numerator, denominator), converter.Convert(new Number(numerator, denominator)));
    }

    /// <summary>A large numerator keeps its scale nouns (NTS-30B/C) and the Bulgarian и (NTS-30D).</summary>
    [TestMethod]
    public void LargeNumerators_KeepTheCardinalPipeline()
    {
        Assert.AreEqual("п'ять мільйонів третіх", Get("UK").ConvertFraction(5_000_000, 3));
        Assert.AreEqual("двадесет и две хиляди трети", Get("BG").ConvertFraction(22_000, 3));
        Assert.AreEqual("dva miliony třetin", Get("CS").ConvertFraction(2_000_000, 3));
        Assert.AreEqual("dva milióny tretín", Get("SK").ConvertFraction(2_000_000, 3));
    }

    // ===== regression ================================================================================

    /// <summary>The standalone cardinals, ordinals and decimals keep their outputs.</summary>
    [TestMethod]
    public void Regression_OtherSurfacesUnchanged()
    {
        Assert.AreEqual("дві тисячі", Get("UK").Convert(2_000));
        Assert.AreEqual("два мільйони", Get("UK").Convert(2_000_000));
        Assert.AreEqual("п'ять мільйонів", Get("UK").Convert(5_000_000));
        Assert.AreEqual("двадесет и едно", Get("BG").Convert(21));
        Assert.AreEqual("две хиляди", Get("BG").Convert(2_000));
        Assert.AreEqual("два милиона", Get("BG").Convert(2_000_000));
        Assert.AreEqual("dva tisíce", Get("CS").Convert(2_000));
        Assert.AreEqual("dva miliony", Get("CS").Convert(2_000_000));
        Assert.AreEqual("dvě miliardy", Get("CS").Convert(2_000_000_000));
        Assert.AreEqual("dvetisíc", Get("SK").Convert(2_000));
        Assert.AreEqual("dva milióny", Get("SK").Convert(2_000_000));
        Assert.AreEqual("dve miliardy", Get("SK").Convert(2_000_000_000));
        Assert.AreEqual("третя", Get("UK").ConvertOrdinal(3, "gender=жіночий"));
        Assert.AreEqual("třetí", Get("CS").ConvertOrdinal(3));
    }

    /// <summary>Only these four languages read fractions through the extension point.</summary>
    [TestMethod]
    public void Regression_OnlyTheFourLanguagesOptIn()
    {
        foreach (string culture in new[] { "UK", "UK-UA", "BG", "BG-BG", "CS", "CS-CZ", "SK", "SK-SK" })
            Assert.IsInstanceOfType<IFractionLanguageSpecifics>(Get(culture).LanguageSpecifics, culture);
        foreach (string culture in new[] { "EN", "FR", "RU", "PL", "DE", "ES", "HR" })
            Assert.IsNotInstanceOfType<IFractionLanguageSpecifics>(Get(culture).LanguageSpecifics, culture);
    }
}
