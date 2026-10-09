using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of the productive <see cref="IOrdinalLanguageSpecifics"/> plugins added for the
/// NTS-08 languages: range limits, irregular forms, compounds, round scales, and the fail-closed
/// contract outside the implemented range. Linguistic coverage of ordinary values lives in the
/// language <c>.feature</c> files.
/// </summary>
[TestClass]
public class NumberToStringOrdinalPluginTests
{
    /// <summary>Ensures the Danish and Norwegian plugins cover the whole <see cref="int"/> range with productive compounds.</summary>
    [TestMethod]
    [DataRow("DA", "to milliarder hundrede og syvogfyrre millioner fire hundrede og treogfirs tusind seks hundrede og syvogfyrretyvende")]
    [DataRow("NO", "to milliarder hundre og førtisju millioner fire hundre og åttitre tusen seks hundre og førtisjuende")]
    public void ScandinavianOrdinals_IntMaxValue_IsProductive(string culture, string expected)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.IsTrue(converter.SupportsOrdinals);
        Assert.AreEqual(expected, converter.ConvertOrdinal(int.MaxValue));
    }

    /// <summary>Ensures round scale values ordinalize only their last scale word.</summary>
    [TestMethod]
    [DataRow("DA", 21_000, "enogtyve tusinde")]
    [DataRow("DA", 2_000_000, "to millionte")]
    [DataRow("DA", 1_000_000_000, "milliardte")]
    [DataRow("DA", 1_000_001, "en million og første")]
    [DataRow("NO", 21_000, "tjueen tusende")]
    [DataRow("NO", 1_000_000_000, "milliardte")]
    [DataRow("SV", 21_000, "tjugoentusende")]
    [DataRow("SV", 101_000, "hundraettusende")]
    [DataRow("SV", 1_100, "tusenhundrade")]
    [DataRow("SV", 1_000_000, "miljonte")]
    public void ScandinavianOrdinals_RoundScales_OrdinalizeLastScaleWord(string culture, int number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(culture).ConvertOrdinal(number));

    /// <summary>
    /// Ensures values outside the implemented range fail closed instead of returning a cardinal:
    /// the Scandinavian plugins only implement the <see cref="int"/> range and have no declarative
    /// fallback, and zero has no ordinal.
    /// </summary>
    [TestMethod]
    [DataRow("DA")]
    [DataRow("NO")]
    [DataRow("SV")]
    public void ScandinavianOrdinals_OutsideImplementedRange_FailClosed(string culture)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal((long)int.MaxValue + 1));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
    }

    /// <summary>
    /// Ensures the Swedish plugin stops at one million (NTS-08): above it miljon and miljard stand apart
    /// and no consulted source settles the compound ordinal, so every larger value fails closed.
    /// </summary>
    [TestMethod]
    [DataRow(1_000_001)]
    [DataRow(2_000_000)]
    [DataRow(1_000_000_000)]
    [DataRow(int.MaxValue)]
    public void SwedishOrdinals_AboveOneMillion_FailClosed(int number)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("SV");

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(number));
        Assert.AreEqual("miljonte", converter.ConvertOrdinal(1_000_000));
    }

    /// <summary>Ensures the Bulgarian plugin keeps "и" before the last element up to <see cref="int.MaxValue"/>.</summary>
    [TestMethod]
    public void BulgarianOrdinals_IntMaxValue_IsProductive()
        => Assert.AreEqual(
            "два милиарда сто четиридесет и седем милиона четиристотин осемдесет и три хиляди шестстотин четиридесет и седми",
            NumberToStringConverter.GetConverter("BG").ConvertOrdinal(int.MaxValue));

    /// <summary>Ensures Bulgarian round scales use the verified single-word compounds and gender endings.</summary>
    [TestMethod]
    [DataRow(1_002_000, "", "един милион и двехиляден")]
    [DataRow(1_000_000_000, "", "милиарден")]
    [DataRow(3_000, "gender=feminine", "трихилядна")]
    [DataRow(1_000_000, "gender=neuter", "милионно")]
    public void BulgarianOrdinals_RoundScales_AreSingleWords(int number, string variants, string expected)
        => Assert.AreEqual(
            expected,
            NumberToStringConverter.GetConverter("BG").ConvertOrdinal(number, variants.Length == 0 ? [] : [variants]));

    /// <summary>
    /// Ensures Bulgarian values whose compound adjective forms were not verified (a multi-word
    /// multiplier of a round thousand, round millions above one), values above the <see cref="int"/>
    /// range, and zero fail closed instead of returning a cardinal.
    /// </summary>
    [TestMethod]
    [DataRow(21_000L)]
    [DataRow(2_000_000L)]
    [DataRow(2_147_483_648L)]
    [DataRow(0L)]
    public void BulgarianOrdinals_UnverifiedOrOutOfRange_FailClosed(long number)
        => Assert.ThrowsExactly<NotSupportedException>(() => NumberToStringConverter.GetConverter("BG").ConvertOrdinal(number));

    /// <summary>Ensures Hungarian ordinals stay productive and hyphenated above 2000 up to <see cref="int.MaxValue"/>.</summary>
    [TestMethod]
    [DataRow(int.MaxValue, "kétmilliárd-száznegyvenhétmillió-négyszáznyolcvanháromezer-hatszáznegyvenhetedik")]
    [DataRow(21_000, "huszonegyezredik")]
    [DataRow(2_000_000, "kétmilliomodik")]
    [DataRow(1_000_000_000, "egymilliárdodik")]
    [DataRow(1_999, "ezerkilencszázkilencvenkilencedik")]
    [DataRow(2_002, "kétezer-kettedik")]
    public void HungarianOrdinals_AreProductive(int number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("HU").ConvertOrdinal(number));

    /// <summary>Ensures Hungarian values outside the implemented range fail closed.</summary>
    [TestMethod]
    [DataRow(0L)]
    [DataRow(2_147_483_648L)]
    public void HungarianOrdinals_OutsideImplementedRange_FailClosed(long number)
        => Assert.ThrowsExactly<NotSupportedException>(() => NumberToStringConverter.GetConverter("HU").ConvertOrdinal(number));

    /// <summary>
    /// Ensures the declarative Croatian word rules cover every word that can end a cardinal: outside
    /// the round scale values that fail closed, the ordinal differs from the cardinal for the whole
    /// sweep, including zero and the lexical scale ordinals.
    /// </summary>
    [TestMethod]
    public void CroatianOrdinals_DeclarativeRulesCoverEveryFinalWord()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("HR");

        foreach (long value in Enumerable.Range(0, 2_101).Select(v => (long)v)
            .Concat([1_000_000L, 1_000_000_000L, 2_000_001L, 21_000_021L, long.MaxValue]))
        {
            if (value != 0 && value % 1000 == 0 && value is not (1_000 or 1_000_000 or 1_000_000_000))
                continue;
            Assert.AreNotEqual(converter.Convert(value), converter.ConvertOrdinal(value), value.ToString());
        }
    }

    /// <summary>
    /// Ensures Croatian ordinals of round thousands, millions, milliards and larger scales other than
    /// the single lexical units (tisućiti, milijunti, milijarditi) fail closed: their forms were not
    /// verified, and the declarative rules would otherwise end with an inflected scale noun.
    /// </summary>
    [TestMethod]
    [DataRow(2_000L)]
    [DataRow(21_000L)]
    [DataRow(2_000_000L)]
    [DataRow(2_000_000_000L)]
    [DataRow(1_000_000_000_000L)]
    public void CroatianOrdinals_UnverifiedRoundScales_FailClosed(long number)
        => Assert.ThrowsExactly<NotSupportedException>(() => NumberToStringConverter.GetConverter("HR").ConvertOrdinal(number));

    /// <summary>Ensures the single scale units use their lexical Croatian ordinals.</summary>
    [TestMethod]
    [DataRow(1_000L, "tisućiti")]
    [DataRow(1_000_000L, "milijunti")]
    [DataRow(1_000_000_000L, "milijarditi")]
    public void CroatianOrdinals_SingleScaleUnits_AreLexical(long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("HR").ConvertOrdinal(number));

    /// <summary>
    /// Ensures an ordinal is never the unchanged cardinal for a broad sample. 100 is excluded: its
    /// Danish ordinal is genuinely identical to the cardinal (<c>hundrede</c>, Retskrivningsordbogen).
    /// </summary>
    [TestMethod]
    [DataRow("DA")]
    [DataRow("NO")]
    [DataRow("SV")]
    public void ScandinavianOrdinals_NeverReturnTheCardinal(string culture)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        foreach (int value in new[] { 1, 2, 3, 7, 11, 19, 20, 21, 45, 99, 101, 999, 1000, 1001, 21000, 1_000_000 })
            Assert.AreNotEqual(converter.Convert(value), converter.ConvertOrdinal(value), $"{culture} {value}");
    }

    /// <summary>Ensures the Slavic adjectival plugins decline every component and cover their largest verified values.</summary>
    [TestMethod]
    [DataRow("CS", 9_999L, "", "devítitisící devítistý devadesátý devátý")]
    [DataRow("CS", 1_100L, "", "tisící stý")]
    [DataRow("CS", 121L, "gender=ženský,case=instrumentál", "stou dvacátou první")]
    [DataRow("CS", 3L, "gender=ženský,case=instrumentál", "třetí")]
    [DataRow("SK", 9_999L, "", "deväťtisícdeväťsto deväťdesiaty deviaty")]
    [DataRow("SK", 1_100L, "", "tisícstý")]
    [DataRow("SK", 101L, "gender=ženský", "stoprvá")]
    [DataRow("SK", 1_000_000L, "", "miliónty")]
    [DataRow("UK", int.MaxValue, "", "два мільярди сто сорок сім мільйонів чотириста вісімдесят три тисячі шістсот сорок сьомий")]
    [DataRow("UK", 10_000L, "", "десятитисячний")]
    [DataRow("UK", 1_000_000_000L, "", "мільярдний")]
    [DataRow("UK", 21L, "gender=жіночий,case=орудний", "двадцять першою")]
    public void SlavicAdjectivalOrdinals_AreProductive(string culture, long number, string variants, string expected)
        => Assert.AreEqual(
            expected,
            NumberToStringConverter.GetConverter(culture).ConvertOrdinal(number, variants.Length == 0 ? [] : variants.Split(',')));

    /// <summary>
    /// Ensures Slavic values whose compound forms were not verified, values above the <see cref="int"/>
    /// range, and zero fail closed instead of returning a cardinal.
    /// </summary>
    [TestMethod]
    [DataRow("CS", 21_000L)]
    [DataRow("CS", 2_000_000L)]
    [DataRow("CS", 2_147_483_647L)]
    [DataRow("CS", 0L)]
    [DataRow("SK", 10_000L)]
    [DataRow("SK", 2_000L)]
    [DataRow("SK", 2_000_000L)]
    [DataRow("UK", 11_000L)]
    [DataRow("UK", 2_000_000L)]
    [DataRow("UK", 2_147_483_648L)]
    public void SlavicAdjectivalOrdinals_UnverifiedOrOutOfRange_FailClosed(string culture, long number)
        => Assert.ThrowsExactly<NotSupportedException>(() => NumberToStringConverter.GetConverter(culture).ConvertOrdinal(number));

    /// <summary>
    /// Ensures the Greek feminine cardinal variant (added for clock hours) never leaks a feminine
    /// cardinal word into a feminine ordinal: every cardinal word must be ordinalized. Since NTS-08
    /// the values outside the validated ordinal domain (compounds above twenty, whose ordinal is made
    /// of agreeing ordinals such as "εκατοστή πρώτη") fail closed instead of mixing a cardinal and an
    /// ordinal ("διακόσια πρώτη").
    /// </summary>
    [TestMethod]
    public void GreekFeminineOrdinals_DoNotLeakFeminineCardinalWords()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("EL");
        string[] cardinalWords = ["μία", "τρεις", "τέσσερις", "χίλιες", "διακόσιες", "εννιακόσιες"];
        int supported = 0;

        for (int value = 1; value <= 1_100; value++)
        {
            string ordinal;
            try
            {
                ordinal = converter.ConvertOrdinal(value, "gender=θηλυκό");
            }
            catch (NotSupportedException)
            {
                continue;
            }
            supported++;
            foreach (string word in ordinal.Split(' '))
                CollectionAssert.DoesNotContain(cardinalWords, word, $"{value}: {ordinal}");
        }
        Assert.AreEqual(37, supported, "1-20, the round tens 30-90, the nine round hundreds and 1000.");
        Assert.AreEqual("πρώτη", converter.ConvertOrdinal(1, "gender=θηλυκό"));
        Assert.AreEqual("δέκατη τρίτη", converter.ConvertOrdinal(13, "gender=θηλυκό"));
        Assert.AreEqual("διακοσιοστή", converter.ConvertOrdinal(200, "gender=θηλυκό"));
        Assert.AreEqual("χιλιοστή", converter.ConvertOrdinal(1000, "gender=θηλυκό"));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(201, "gender=θηλυκό"));
    }

    /// <summary>Ensures the Romanian plugin changes only the last word, in both genders, up to its range limit.</summary>
    [TestMethod]
    [DataRow(999_999, "", "al nouă sute nouăzeci și nouă de mii nouă sute nouăzeci și nouălea")]
    [DataRow(999_999, "gen=feminin", "a nouă sute nouăzeci și nouă de mii nouă sute nouăzeci și noua")]
    [DataRow(11_000, "", "al unsprezece miilea")]
    [DataRow(1_000_000, "", "al un milionulea")]
    public void RomanianOrdinals_AreProductive(int number, string variants, string expected)
        => Assert.AreEqual(
            expected,
            NumberToStringConverter.GetConverter("RO").ConvertOrdinal(number, variants.Length == 0 ? [] : [variants]));

    /// <summary>
    /// Ensures Romanian values outside the verified range (round "de mii" thousands, values above one
    /// million, the feminine of one million, zero) fail closed instead of returning a cardinal.
    /// </summary>
    [TestMethod]
    [DataRow(20_000L, "")]
    [DataRow(21_000L, "")]
    [DataRow(1_000_001L, "")]
    [DataRow(1_000_000L, "gen=feminin")]
    [DataRow(0L, "")]
    public void RomanianOrdinals_UnverifiedOrOutOfRange_FailClosed(long number, string variants)
        => Assert.ThrowsExactly<NotSupportedException>(
            () => NumberToStringConverter.GetConverter("RO").ConvertOrdinal(number, variants.Length == 0 ? [] : [variants]));

    /// <summary>
    /// Ensures Arabic ordinals outside the documented contract (1-99, 100, 1000) fail closed instead
    /// of returning an unchanged cardinal, including zero, which the partial XML (1-19) would
    /// otherwise render as the cardinal "صفر".
    /// </summary>
    [TestMethod]
    [DataRow(0L)]
    [DataRow(101L)]
    [DataRow(150L)]
    [DataRow(2_000L)]
    [DataRow(3_000_000_000L)]
    public void ArabicOrdinals_OutsideContract_FailClosed(long number)
        => Assert.ThrowsExactly<NotSupportedException>(() => NumberToStringConverter.GetConverter("AR").ConvertOrdinal(number));

    /// <summary>Ensures every Arabic ordinal from 20 to 99 is the unit ordinal joined to the tens cardinal, in both genders.</summary>
    [TestMethod]
    public void ArabicOrdinals_TwentyToNinetyNine_AreProductive()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("AR");
        for (int value = 21; value <= 99; value++)
        {
            if (value % 10 == 0)
                continue;
            string masculine = converter.ConvertOrdinal(value);
            string feminine = converter.ConvertOrdinal(value, "gender=muʾannath");
            StringAssert.Contains(masculine, " و", value.ToString());
            Assert.AreNotEqual(converter.Convert(value), masculine, value.ToString());
            Assert.AreNotEqual(masculine, feminine, value.ToString());
        }
    }

    /// <summary>
    /// Ensures the declarative Turkish and Persian ordinal rules cover every word that can end a
    /// cardinal: the ordinal differs from the cardinal for the whole sweep and the largest values.
    /// </summary>
    [TestMethod]
    [DataRow("TR", long.MaxValue)]
    [DataRow("FA", 999_999_999_999_999L)]
    public void DeclarativeOrdinals_CoverEveryFinalWord(string culture, long largest)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        for (long value = 0; value <= 2_100; value++)
            Assert.AreNotEqual(converter.Convert(value), converter.ConvertOrdinal(value), $"{culture} {value}");
        foreach (long value in new[] { 21_000L, 1_000_000L, 1_000_000_000L, largest })
            Assert.AreNotEqual(converter.Convert(value), converter.ConvertOrdinal(value), $"{culture} {value}");
    }
}
