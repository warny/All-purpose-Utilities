using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-34, final audit of the decimal reading of every built-in language (matrix and sources:
/// <c>docs/NTS-34-decimal-reading-audit.md</c>). Each root language is in one of four categories:
/// <list type="bullet">
/// <item><description>A — the digit-by-digit reading is attested as a normal reading: <c>LanguageDefault</c> keeps the
/// unlimited fallback (DE, DA, NO, ES, ZH).</description></item>
/// <item><description>B — named decimal fractions, configured (FR, EN, NTS-30A, unchanged).</description></item>
/// <item><description>C — the normative reading is known but the primitives cannot render it (declined fraction nouns,
/// the decimal part read as one number, a denominator before its numerator, an attributive form);
/// D — no authoritative source establishes the automatic reading. Both are closed: <c>decimalDigitByDigitMaxDigits="0"</c>,
/// <c>LanguageDefault</c> throws <see cref="NotSupportedException"/> for a non-empty decimal part.</description></item>
/// </list>
/// <see cref="DecimalReadingMode.DigitByDigit"/> is requested by the caller and stays available everywhere, unchanged.
/// </summary>
[TestClass]
public class NumberToStringNts34Tests
{
    /// <summary>
    /// The closed converters (categories C and D) with their explicit <see cref="DecimalReadingMode.DigitByDigit"/>
    /// reading of 21.05, byte-identical to master 7bd9354b.
    /// </summary>
    private static readonly Dictionary<string, string> s_closed = new()
    {
        ["AR"] = "واحد وعشرون فاصل صفر خمسة",
        ["BG"] = "двадесет и едно цяло нула пет",
        ["BG-BG"] = "двадесет и едно цяло нула пет",
        ["CA"] = "vint-i-un coma zero cinc",
        ["ca-ES"] = "vint-i-un coma zero cinc",
        ["ca-ES-valencia"] = "vint-i-un coma zero cinc",
        ["CS"] = "dvacet jedna celá nula pět",
        ["CS-CZ"] = "dvacet jedna celá nula pět",
        ["EE"] = "blaeve vɔ ɖekɛ kpɔ naneke o atɔ̃",
        ["EL"] = "είκοσι ένα κόμμα μηδέν πέντε",
        ["EU"] = "hogeita bat koma zero bost",
        ["eu-ES"] = "hogeita bat koma zero bost",
        ["FA"] = "بیست و یک ممیز صفر پنج",
        ["FA-IR"] = "بیست و یک ممیز صفر پنج",
        ["FI"] = "kaksikymmentäyksi pilkku nolla viisi",
        ["GL"] = "vinte e un coma cero cinco",
        ["gl-ES"] = "vinte e un coma cero cinco",
        ["HE"] = "עשרים ואחד נקודה אפס חמש",
        ["HI"] = "इक्कीस दशमलव शून्य पाँच",
        ["HR"] = "dvadeset jedan zarez nula pet",
        ["HR-HR"] = "dvadeset jedan zarez nula pet",
        ["HU"] = "huszonegyegésznullaöt",
        ["HU-HU"] = "huszonegyegésznullaöt",
        ["ID"] = "dua puluh satu koma nol lima",
        ["ID-ID"] = "dua puluh satu koma nol lima",
        ["IT"] = "ventuno virgola zero cinque",
        ["JA"] = "二十一点零五",
        ["KO"] = "이십일 점 영 오",
        ["MS"] = "dua puluh satu koma sifar lima",
        ["MS-MY"] = "dua puluh satu koma sifar lima",
        ["NL"] = "eenentwintig komma nul vijf",
        ["PL"] = "dwadzieścia jeden przecinek zero pięć",
        ["PT"] = "vinte e um vírgula zero cinco",
        ["RO"] = "douăzeci și unu virgulă zero cinci",
        ["RO-RO"] = "douăzeci și unu virgulă zero cinci",
        ["RU"] = "двадцать один запятая ноль пять",
        ["SK"] = "dvadsaťjeden celá nula päť",
        ["SK-SK"] = "dvadsaťjeden celá nula päť",
        ["SV"] = "tjugoett komma noll fem",
        ["SV-SE"] = "tjugoett komma noll fem",
        ["SW"] = "ishirini na moja nukta sifuri tano",
        ["SW-KE"] = "ishirini na moja nukta sifuri tano",
        ["SW-TZ"] = "ishirini na moja nukta sifuri tano",
        ["TR"] = "yirmi bir virgül sıfır beş",
        ["TR-TR"] = "yirmi bir virgül sıfır beş",
        ["UK"] = "двадцять один кома нуль п'ять",
        ["UK-UA"] = "двадцять один кома нуль п'ять",
        ["VN"] = "hai mươi mốt phẩy không năm",
        ["VI"] = "hai mươi mốt phẩy không năm",
        ["VI-VN"] = "hai mươi mốt phẩy không năm",
        ["WO"] = "ñaar fukk ak benn pojint tus juróom",
        ["ZU"] = "amashumi amabili nkunye phuzu iqanda isihlanu",
    };

    /// <summary>The category A converters with their reading of 21.05 and 21.005.</summary>
    private static readonly Dictionary<string, (string Two, string Three)> s_digitByDigit = new()
    {
        ["DE"] = ("einundzwanzig komma null fünf", "einundzwanzig komma null null fünf"),
        ["de-CH"] = ("einundzwanzig komma null fünf", "einundzwanzig komma null null fünf"),
        ["de-LI"] = ("einundzwanzig komma null fünf", "einundzwanzig komma null null fünf"),
        ["DA"] = ("enogtyve komma nul fem", "enogtyve komma nul nul fem"),
        ["DA-DK"] = ("enogtyve komma nul fem", "enogtyve komma nul nul fem"),
        ["NO"] = ("tjueen komma null fem", "tjueen komma null null fem"),
        ["NB"] = ("tjueen komma null fem", "tjueen komma null null fem"),
        ["NB-NO"] = ("tjueen komma null fem", "tjueen komma null null fem"),
        ["ES"] = ("veintiuno coma cero cinco", "veintiuno coma cero cero cinco"),
        ["ZH"] = ("二十一点零五", "二十一点零零五"),
    };

    /// <summary>The decimals probed for every converter.</summary>
    private static readonly decimal[] s_probes = [1.2m, 1.05m, 21.05m, 21.005m, 0.5m, 1024.007m, 1.00001m, -2.5m];

    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Converts a decimal under an explicit reading mode.</summary>
    /// <param name="language">The culture.</param>
    /// <param name="value">The value.</param>
    /// <param name="mode">The reading mode.</param>
    /// <returns>The text.</returns>
    private static string Read(string language, decimal value, DecimalReadingMode mode)
        => Get(language).Convert(value, -1, new DecimalFormatOptions { ReadingMode = mode });

    /// <summary>Category A: <c>LanguageDefault</c> is the digit-by-digit reading, without limit.</summary>
    [TestMethod]
    public void CategoryA_LanguageDefault_IsDigitByDigit()
    {
        foreach (var (language, expected) in s_digitByDigit)
        {
            Assert.IsNull(Get(language).DecimalDigitByDigitMaxDigits, language);
            Assert.AreEqual(expected.Two, Get(language).Convert(21.05m), language);
            Assert.AreEqual(expected.Three, Get(language).Convert(21.005m), language);
            foreach (decimal probe in s_probes)
                Assert.AreEqual(Read(language, probe, DecimalReadingMode.DigitByDigit), Get(language).Convert(probe), $"{language} {probe}");
        }
    }

    /// <summary>
    /// Categories C and D: <c>LanguageDefault</c> fails closed for every non-empty decimal part (a zero one included),
    /// <c>DigitByDigit</c> keeps its master reading, <c>MathematicalFraction</c> has no fraction to read.
    /// </summary>
    [TestMethod]
    public void CategoriesCD_LanguageDefault_FailsClosed_DigitByDigit_Available()
    {
        foreach (var (language, digitByDigit) in s_closed)
        {
            var converter = Get(language);
            Assert.AreEqual(0, converter.DecimalDigitByDigitMaxDigits, language);
            Assert.AreEqual(0, converter.Fractions.Count, language);
            Assert.IsNull(converter.MixedFractionSeparator, language);
            foreach (decimal probe in s_probes)
            {
                Assert.ThrowsExactly<NotSupportedException>(() => converter.Convert(probe), $"{language} {probe}");
                Assert.ThrowsExactly<NotSupportedException>(() => Read(language, probe, DecimalReadingMode.LanguageDefault), $"{language} {probe}");
                Assert.ThrowsExactly<NotSupportedException>(() => Read(language, probe, DecimalReadingMode.MathematicalFraction), $"{language} {probe}");
                _ = Read(language, probe, DecimalReadingMode.DigitByDigit);
            }
            Assert.AreEqual(digitByDigit, Read(language, 21.05m, DecimalReadingMode.DigitByDigit), language);
            Assert.ThrowsExactly<NotSupportedException>(() => converter.Convert(21m, 2), $"{language} 21,00");
            Assert.ThrowsExactly<NotSupportedException>(() => converter.Convert(1.2), $"{language} double");
        }
    }

    /// <summary>A closed language still reads an integer value, and the caller's suffix keeps its precedence.</summary>
    [TestMethod]
    public void CategoriesCD_IntegersAndCallerSuffix_Unaffected()
    {
        foreach (var language in s_closed.Keys)
        {
            var converter = Get(language);
            Assert.AreEqual(converter.Convert(21), converter.Convert(21m), language);
            Assert.AreEqual(converter.Convert(21), converter.Convert(21.000m), language);
            Assert.AreEqual(converter.Convert(21), converter.Convert(21m, 2, new DecimalFormatOptions { OmitZeroDecimals = true }), language);
            string withSuffix = converter.Convert(1.2m, -1, new DecimalFormatOptions { DecimalSuffix = "SFX" });
            StringAssert.EndsWith(withSuffix, "SFX", language);
        }
    }

    /// <summary>The closure message names the language and the limit.</summary>
    [TestMethod]
    public void ClosureMessage()
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => Get("HU").Convert(38.6m));
        StringAssert.Contains(exception.Message, "[HU]");
        StringAssert.Contains(exception.Message, "(0)");
    }

    /// <summary>The named fractions of FR and EN (NTS-30A) are unchanged.</summary>
    [TestMethod]
    public void CategoryB_Unchanged()
    {
        Assert.AreEqual("vingt-et-un virgule cinq centièmes", Get("FR").Convert(21.05m));
        Assert.AreEqual("vingt-et-un virgule zéro cinq", Read("FR", 21.05m, DecimalReadingMode.DigitByDigit));
        Assert.AreEqual("vingt-et-un et cinq centièmes", Read("FR", 21.05m, DecimalReadingMode.MathematicalFraction));
        Assert.AreEqual("twenty-one point five hundredths", Get("EN").Convert(21.05m));
        Assert.AreEqual("twenty-one point zero five", Read("EN", 21.05m, DecimalReadingMode.DigitByDigit));
        Assert.AreEqual("twenty-one and five hundredths", Read("EN", 21.05m, DecimalReadingMode.MathematicalFraction));
        Assert.IsNull(Get("EN").DecimalDigitByDigitMaxDigits);
        Assert.IsNull(Get("FR").DecimalDigitByDigitMaxDigits);
    }

    /// <summary>Every built-in converter is classified: A, B (FR and EN families) or closed.</summary>
    [TestMethod]
    public void EveryConverter_IsClassified()
    {
        var unclassified = new List<string>();
        foreach (var culture in new[] { "EN", "EN-GB", "EN-uk", "EN-us", "FR", "FR-be", "FR-ca", "FR-ch", "FR-fr" })
            Assert.AreNotEqual(0, Get(culture).Fractions.Count, culture);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            string name = culture.TwoLetterISOLanguageName.ToUpperInvariant();
            if (name.Length != 2 || name is "EN" or "FR" || s_closed.ContainsKey(name) || s_digitByDigit.ContainsKey(name)) continue;
            NumberToStringConverter converter;
            try { converter = Get(name); }
            catch (Exception) { continue; }
            if (converter.LanguageIdentifier.StartsWith("EN", StringComparison.OrdinalIgnoreCase)) continue;
            unclassified.Add(name);
        }
        Assert.AreEqual(0, unclassified.Count, string.Join(", ", unclassified));
    }
}
