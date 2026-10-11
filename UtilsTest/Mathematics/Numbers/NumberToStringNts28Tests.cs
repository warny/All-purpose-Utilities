using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of NTS-28 (compound and large-scale ordinals): the guard extension point
/// <c>TryBuildOrdinal</c>, the round-value builders of the Russian, Polish and Ukrainian plugins, the
/// composition of the Romance and Greek configurations, and the separation between the cardinal and the
/// ordinal. The linguistic forms are pinned by the language <c>.feature</c> files.
/// </summary>
[TestClass]
public class NumberToStringNts28Tests
{
    /// <summary>A guard that builds the even values itself and lets the declarative pipeline form the odd ones.</summary>
    private sealed class EvenBuildingGuard : OrdinalDomainGuardLanguageSpecifics
    {
        /// <inheritdoc />
        protected override string LanguageName => "Test";

        /// <inheritdoc />
        protected override string SupportedDomain => "0–9";

        /// <inheritdoc />
        protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number < 10;

        /// <inheritdoc />
        protected override bool TryBuildOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        {
            result = number % 2 == 0 ? $"built {number}" : null;
            return result is not null;
        }
    }

    /// <summary>A guard without override builds nothing (historical behaviour).</summary>
    private sealed class PlainGuard : OrdinalDomainGuardLanguageSpecifics
    {
        /// <inheritdoc />
        protected override string LanguageName => "Plain";

        /// <inheritdoc />
        protected override string SupportedDomain => "0–9";

        /// <inheritdoc />
        protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number < 10;
    }

    /// <summary>The extension point builds inside the domain, defers otherwise, and never runs outside it.</summary>
    [TestMethod]
    public void GuardExtensionPoint_BuildsDefersAndRejects()
    {
        var variants = new Dictionary<string, string>();
        var guard = new EvenBuildingGuard();

        Assert.IsTrue(guard.TryConvertOrdinal(4L, variants, out string? built));
        Assert.AreEqual("built 4", built);
        Assert.IsFalse(guard.TryConvertOrdinal(5L, variants, out string? deferred));
        Assert.IsNull(deferred);
        Assert.ThrowsExactly<NotSupportedException>(() => guard.TryConvertOrdinal(12L, variants, out _));

        Assert.IsFalse(new PlainGuard().TryConvertOrdinal(4, variants, out string? plain));
        Assert.IsNull(plain);
    }

    /// <summary>
    /// Every Russian round thousand from 2000 to 99 000 is one word ending in -тысячный, except the multipliers
    /// ending in one after the tens, which fail closed.
    /// </summary>
    [TestMethod]
    public void Russian_RoundThousands_AreOneWordOrFailClosed()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("RU");
        for (int multiplier = 2; multiplier <= 99; multiplier++)
        {
            long value = multiplier * 1000L;
            if (multiplier >= 21 && multiplier % 10 == 1)
            {
                Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(value), $"{value}");
                continue;
            }
            string ordinal = converter.ConvertOrdinal(value);
            StringAssert.EndsWith(ordinal, "тысячный", $"{value}");
            Assert.IsFalse(ordinal.Contains(' '), $"{value}: {ordinal}");
        }
        Assert.AreEqual("девяностатысячный", converter.ConvertOrdinal(90_000L));
        Assert.AreEqual("девятисотдевяностадевятитысячный", converter.ConvertOrdinal(999_000L));
        Assert.AreEqual("тысячный", converter.ConvertOrdinal(1000L));
    }

    /// <summary>The Russian builder declines -тысячный and -миллионный for every gender and case.</summary>
    [TestMethod]
    [DataRow("gender=maskulin,case=винительный", "двухтысячный")]
    [DataRow("gender=feminin,case=винительный", "двухтысячную")]
    [DataRow("gender=neutrum,case=дательный", "двухтысячному")]
    [DataRow("gender=plural,case=предложный", "двухтысячных")]
    public void Russian_RoundOrdinals_AreDeclined(string variants, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("RU").ConvertOrdinal(2000L, variants.Split(',')));

    /// <summary>
    /// The Polish builder keeps the thousands cardinal with the NTS-27 agreement and makes every round
    /// thousand from 2000 to 99 000 one word ending in tysięczny.
    /// </summary>
    [TestMethod]
    public void Polish_ThousandsAgreeAndRoundThousandsAreOneWord()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("PL");
        Assert.AreEqual("dwa tysiące pierwszy", converter.ConvertOrdinal(2001L));
        Assert.AreEqual("pięć tysięcy pierwszy", converter.ConvertOrdinal(5001L));
        Assert.AreEqual("dwanaście tysięcy pierwszy", converter.ConvertOrdinal(12_001L));
        Assert.AreEqual("dwadzieścia dwa tysiące pierwszy", converter.ConvertOrdinal(22_001L));
        Assert.AreEqual("dwa tysiące sto piętnasty", converter.ConvertOrdinal(2115L));
        for (int multiplier = 2; multiplier <= 99; multiplier++)
        {
            string ordinal = converter.ConvertOrdinal(multiplier * 1000L);
            StringAssert.EndsWith(ordinal, "tysięczny", $"{multiplier}");
            Assert.IsFalse(ordinal.Contains(' '), $"{multiplier}: {ordinal}");
        }
    }

    /// <summary>
    /// The Polish ordinals 10–19 after the thousands (built by the plugin) take the same virile plural nominative
    /// as alone (built by the XML pipeline): -naści with ś (piętnaści), not a concatenation of the stem with the
    /// ending of piąci (piętnasci).
    /// </summary>
    [TestMethod]
    [DataRow(10, "dziesiąci")]
    [DataRow(11, "jedenaści")]
    [DataRow(12, "dwunaści")]
    [DataRow(13, "trzynaści")]
    [DataRow(14, "czternaści")]
    [DataRow(15, "piętnaści")]
    [DataRow(16, "szesnaści")]
    [DataRow(17, "siedemnaści")]
    [DataRow(18, "osiemnaści")]
    [DataRow(19, "dziewiętnaści")]
    public void Polish_TeensAfterThousands_HaveTheVirilePluralNominative(int value, string expected)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("PL");
        string[] variants = ["rodzaj=plural_mos", "przypadek=mianownik"];

        Assert.AreEqual(expected, converter.ConvertOrdinal(value, variants));
        Assert.AreEqual("dwa tysiące " + expected, converter.ConvertOrdinal(2000 + value, variants));
        Assert.AreEqual("dwanaście tysięcy " + expected, converter.ConvertOrdinal(12_000 + value, variants));
    }

    /// <summary>Every form of the Polish teens after the thousands equals the form the XML pipeline gives alone.</summary>
    [TestMethod]
    public void Polish_TeensAfterThousands_MatchTheirStandaloneForms()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("PL");
        foreach (string rodzaj in new[] { "maskulin", "feminin", "nijaki", "plural_mos", "plural" })
            foreach (string przypadek in new[] { "mianownik", "dopełniacz", "celownik", "biernik", "narzędnik", "miejscownik" })
                for (int value = 1; value <= 19; value++)
                {
                    string[] variants = [$"rodzaj={rodzaj}", $"przypadek={przypadek}"];
                    Assert.AreEqual("pięć tysięcy " + converter.ConvertOrdinal(value, variants),
                        converter.ConvertOrdinal(5000 + value, variants), $"{value} {rodzaj} {przypadek}");
                }
    }

    /// <summary>The Ukrainian combining forms stop at 89 and skip a final один after the tens.</summary>
    [TestMethod]
    public void Ukrainian_CombiningForms_HaveTheirDomain()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("UK");
        Assert.AreEqual("вісімдесятидев'ятитисячний", converter.ConvertOrdinal(89_000L));
        Assert.AreEqual("одинадцятитисячний", converter.ConvertOrdinal(11_000L));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(31_000L));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(90_000L));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(200_000L));
    }

    /// <summary>
    /// The ordinal compositions never change the cardinals: Spanish keeps its apocope only in cardinals, the
    /// Galician ordinal replacements do not touch the cardinal, Greek and Finnish cardinals are unchanged.
    /// </summary>
    [TestMethod]
    [DataRow("ES", 21L, "veintiuno")]
    [DataRow("ES", 21_000L, "veintiún mil")]
    [DataRow("ES", 2000L, "dos mil")]
    [DataRow("GL", 21L, "vinte e un")]
    [DataRow("GL", 1201L, "mil douscentos un")]
    [DataRow("EL", 21L, "είκοσι ένα")]
    [DataRow("PT", 21L, "vinte e um")]
    [DataRow("RU", 2000L, "две тысячи")]
    public void Cardinals_AreUnchangedByOrdinalRules(string culture, long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(culture).Convert(number));

    /// <summary>
    /// The attributive form of the Spanish cardinal (veintiún, an apocope before a noun) never becomes the
    /// ordinal: the ordinal of 21 stays vigesimoprimero (before NTS-28 the cardinal "veintiún" was returned
    /// unchanged; the NTS-28 suffix must not turn it into "veintiúno").
    /// </summary>
    [TestMethod]
    [DataRow("form=attributive", "vigesimoprimero")]
    [DataRow("gender=masculino,form=attributive", "vigesimoprimero")]
    [DataRow("gender=femenino,form=attributive", "vigesimoprimera")]
    public void Spanish_AttributiveCardinal_DoesNotLeakIntoOrdinals(string variants, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("ES").ConvertOrdinal(21, variants.Split(',')));

    /// <summary>The composed ordinals agree in every part (ES, PT, EL) or on the last part only (GL).</summary>
    [TestMethod]
    [DataRow("ES", "gender=femenino", 1248L, "milésima ducentésima cuadragésima octava")]
    [DataRow("PT", "gender=feminino", 1248L, "milésima ducentésima quadragésima oitava")]
    [DataRow("EL", "gender=θηλυκό", 1248L, "χιλιοστή διακοσιοστή τεσσαρακοστή όγδοη")]
    [DataRow("GL", "gender=feminino", 1248L, "milésimo ducentésimo cuadraxésimo oitava")]
    public void Compositions_AgreeAsTheLanguageRequires(string culture, string variant, long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(culture).ConvertOrdinal(number, variant));
}
