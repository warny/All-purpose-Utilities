using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of the NTS-08 ordinal domain guards (<see cref="OrdinalDomainGuardLanguageSpecifics"/>
/// and the Polish plugin limit): the edges of each validated domain, the fail-closed behaviour outside it
/// (including negative values and values above <see cref="int"/>), and the absence of any unchanged cardinal
/// returned as an ordinal. The linguistic forms themselves are pinned by the language <c>.feature</c> files.
/// </summary>
[TestClass]
public class NumberToStringOrdinalDomainGuardTests
{
    /// <summary>Ensures the largest validated value of each guarded language is still converted.</summary>
    /// <param name="culture">The culture of the converter.</param>
    /// <param name="number">The largest value inside the validated domain.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow("ES", 1000L, "milésimo")]
    [DataRow("PT", 1000L, "milésimo")]
    [DataRow("GL", 1000L, "milésimo")]
    [DataRow("EL", 1000L, "χιλιοστός")]
    [DataRow("FI", 1000L, "tuhannes")]
    [DataRow("RU", 1999L, "тысяча девятьсот девяносто девятый")]
    [DataRow("VN", 999L, "thứ chín trăm chín mươi chín")]
    [DataRow("HI", 99_999L, "निन्यानवे हज़ार नौ सौ निन्यानवेवाँ")]
    [DataRow("JA", 1000L, "第千")]
    [DataRow("KO", 1000L, "제천")]
    [DataRow("ZH", 900L, "第九百")]
    [DataRow("EU", 1000L, "milagarren")]
    [DataRow("PL", 1999L, "tysiąc dziewięćset dziewięćdziesiąty dziewiąty")]
    public void GuardedLanguages_LargestValidatedValue_IsConverted(string culture, long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(culture).ConvertOrdinal(number));

    /// <summary>
    /// Ensures the first value outside each validated domain, its negative, and a value above
    /// <see cref="int"/> fail closed with <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="culture">The culture of the converter.</param>
    /// <param name="number">The first value outside the validated domain.</param>
    [TestMethod]
    [DataRow("ES", 31L)]
    [DataRow("PT", 21L)]
    [DataRow("GL", 21L)]
    [DataRow("EL", 21L)]
    [DataRow("FI", 21L)]
    [DataRow("RU", 2000L)]
    [DataRow("VN", 1000L)]
    [DataRow("HI", 100_000L)]
    [DataRow("JA", 1001L)]
    [DataRow("KO", 1001L)]
    [DataRow("ZH", 101L)]
    [DataRow("EU", 1001L)]
    [DataRow("PL", 2000L)]
    public void GuardedLanguages_OutsideValidatedDomain_FailClosed(string culture, long number)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(number));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(-number));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal((long)int.MaxValue + 1));
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
