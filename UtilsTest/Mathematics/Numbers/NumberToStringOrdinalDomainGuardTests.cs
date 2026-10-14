using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of the ordinal domain guards (<see cref="OrdinalDomainGuardLanguageSpecifics"/> and the
/// Polish plugin limit), as set by NTS-08 and widened by NTS-28 and NTS-31 (Dutch: every magnitude up to
/// <see cref="long.MaxValue"/>, so no Dutch value fails closed): the edges of each validated domain, the
/// fail-closed behaviour outside it (including negative values and <see cref="long.MaxValue"/>), and the absence
/// of any unchanged cardinal returned as an ordinal. The linguistic forms themselves are pinned by the language
/// <c>.feature</c> files.
/// </summary>
[TestClass]
public class NumberToStringOrdinalDomainGuardTests
{
    /// <summary>Ensures the largest validated value of each guarded language is still converted.</summary>
    /// <param name="culture">The culture of the converter.</param>
    /// <param name="number">The largest value inside the validated domain.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow("ES", 900_000_000L, "novecientosmillonésimo")]
    [DataRow("PT", 1999L, "milésimo nongentésimo nonagésimo nono")]
    [DataRow("GL", 1999L, "milésimo noninxentésimo nonaxésimo noveno")]
    [DataRow("EL", 1999L, "χιλιοστός εννιακοσιοστός ενενηκοστός ένατος")]
    [DataRow("FI", 1_999_999L, "miljoonas yhdeksässadasyhdeksäskymmenesyhdeksästuhannes yhdeksässadasyhdeksäskymmenesyhdeksäs")]
    [DataRow("RU", 999_999_999L, "девятьсот девяносто девять миллионов девятьсот девяносто девять тысяч девятьсот девяносто девятый")]
    [DataRow("VN", 999_999_999_999L, "thứ chín trăm chín mươi chín tỷ chín trăm chín mươi chín triệu chín trăm chín mươi chín nghìn chín trăm chín mươi chín")]
    [DataRow("HI", 9_999_999_999_999L, "निन्यानवे खरब निन्यानवे अरब निन्यानवे करोड़ निन्यानवे लाख निन्यानवे हज़ार नौ सौ निन्यानवेवाँ")]
    [DataRow("JA", 9_999_999_999_999_999L, "第九千九百九十九兆九千九百九十九億九千九百九十九万九千九百九十九")]
    [DataRow("KO", 9_999_999_999_999_999L, "제구천구백구십구조 구천구백구십구억 구천구백구십구만 구천구백구십구")]
    [DataRow("ZH", 999_999_999_999L, "第九千九百九十九亿九千九百九十九万九千九百九十九")]
    [DataRow("EU", 999_999_999L, "bederatziehun eta laurogeita hemeretzi milioi bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzigarren")]
    [DataRow("NL", long.MaxValue, "negen triljoen tweehonderddrieëntwintig biljard driehonderdtweeënzeventig biljoen zesendertig miljard achthonderdvierenvijftig miljoen zevenhonderdvijfenzeventigduizend achthonderdzevende")]
    [DataRow("PL", 999_999L, "dziewięćset dziewięćdziesiąt dziewięć tysięcy dziewięćset dziewięćdziesiąty dziewiąty")]
    public void GuardedLanguages_LargestValidatedValue_IsConverted(string culture, long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(culture).ConvertOrdinal(number));

    /// <summary>
    /// Ensures the first value outside each validated domain (or, where the domain has inner gaps, a value of
    /// such a gap), its negative, and <see cref="long.MaxValue"/> fail closed with <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="culture">The culture of the converter.</param>
    /// <param name="number">A value outside the validated domain.</param>
    [TestMethod]
    [DataRow("ES", 1_000_000_000L)]
    [DataRow("ES", 21_000L)]
    [DataRow("PT", 2000L)]
    [DataRow("GL", 2000L)]
    [DataRow("EL", 2000L)]
    [DataRow("FI", 2_000_000L)]
    [DataRow("RU", 1_000_000_000L)]
    [DataRow("RU", 21_000L)]
    [DataRow("VN", 1_000_000_000_000L)]
    [DataRow("VN", 1_000_005L)]
    [DataRow("HI", 10_000_000_000_000L)]
    [DataRow("JA", 10_000_000_000_000_000L)]
    [DataRow("KO", 10_000_000_000_000_000L)]
    [DataRow("ZH", 1_000_000_000_000L)]
    [DataRow("EU", 1_000_000_000L)]
    [DataRow("PL", 1_000_000L)]
    [DataRow("PL", 100_000L)]
    public void GuardedLanguages_OutsideValidatedDomain_FailClosed(string culture, long number)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(number));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(-number));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(long.MaxValue));
    }

    /// <summary>
    /// Ensures no guarded language returns the unchanged cardinal as an ordinal on 0–2100: every value either
    /// fails closed or yields a text different from the cardinal (the Chinese, Japanese and Korean prefixes,
    /// the suffixes, or the lexical ordinals all change it).
    /// </summary>
    /// <param name="culture">The culture of the converter.</param>
    [TestMethod]
    [DataRow("ES")]
    [DataRow("PT")]
    [DataRow("GL")]
    [DataRow("EL")]
    [DataRow("FI")]
    [DataRow("RU")]
    [DataRow("VN")]
    [DataRow("HI")]
    [DataRow("JA")]
    [DataRow("KO")]
    [DataRow("ZH")]
    [DataRow("EU")]
    [DataRow("PL")]
    [DataRow("NL")]
    public void GuardedLanguages_NeverReturnTheCardinalAsOrdinal(string culture)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);
        var accepted = new List<int>();

        for (int value = 0; value <= 2100; value++)
        {
            string ordinal;
            try
            {
                ordinal = converter.ConvertOrdinal(value);
            }
            catch (NotSupportedException)
            {
                continue;
            }
            accepted.Add(value);
            Assert.AreNotEqual(converter.Convert(value), ordinal, $"{culture} {value}");
        }
        Assert.IsTrue(accepted.Count > 0, $"{culture} accepts no ordinal at all.");
    }

    /// <summary>Ensures the Finnish guard accepts only the nominative, under either dimension name.</summary>
    /// <param name="variant">The requested variant.</param>
    [TestMethod]
    [DataRow("case=genetiivi")]
    [DataRow("case=partitiivi")]
    [DataRow("sijamuoto=genetiivi")]
    [DataRow("sijamuoto=partitiivi")]
    public void FinnishOrdinals_InflectedCases_FailClosed(string variant)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("FI");

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(3, variant));
        Assert.AreEqual("kolmas", converter.ConvertOrdinal(3, "case=nominatiivi"));
        Assert.AreEqual("kolmas", converter.ConvertOrdinal(3, "sijamuoto=nominatiivi"));
    }

    /// <summary>Ensures the zero policies of the guarded languages: attested zero ordinals stay, Basque fails closed.</summary>
    /// <param name="culture">The culture of the converter.</param>
    /// <param name="expected">The expected ordinal of zero, or <see langword="null"/> when it must fail closed.</param>
    [TestMethod]
    [DataRow("FI", "nollas")]
    [DataRow("RU", "нулевой")]
    [DataRow("VN", "thứ không")]
    [DataRow("JA", "第零")]
    [DataRow("ZH", "第零")]
    [DataRow("NL", "nulde")]
    [DataRow("EU", null)]
    [DataRow("ES", null)]
    public void GuardedLanguages_ZeroOrdinalPolicy(string culture, string? expected)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        if (expected is null)
            Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
        else
            Assert.AreEqual(expected, converter.ConvertOrdinal(0));
    }
}
