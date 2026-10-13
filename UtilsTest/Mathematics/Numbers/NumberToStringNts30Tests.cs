using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of NTS-30A, the decimal reading modes of <see cref="NumberToStringConverter.Convert(decimal, int, DecimalFormatOptions?, string[])"/>:
/// <list type="bullet">
/// <item><see cref="DecimalReadingMode.LanguageDefault"/>: caller <see cref="DecimalFormatOptions.DecimalSuffix"/>, else the
/// configured <c>&lt;Fraction&gt;</c>, else digit by digit within the language's <c>decimalDigitByDigitMaxDigits</c>, else
/// <see cref="NotSupportedException"/>;</item>
/// <item><see cref="DecimalReadingMode.DigitByDigit"/>: every decimal digit read, a 0 as the standalone zero and 1–9 as their
/// standalone cardinals (zero as an empty cardinal position is not zero as a pronounced decimal digit);</item>
/// <item><see cref="DecimalReadingMode.MathematicalFraction"/>: the decimal part as a numerator over the named
/// denominator 10^n, joined to a non-zero integer part by the language's <c>mixedFractionSeparator</c>.</item>
/// </list>
/// The French and English forms are pinned against the sources recorded in <c>docs/NTS-08-linguistic-sources.md</c>.
/// </summary>
[TestClass]
public class NumberToStringNts30Tests
{
    /// <summary>Gets the converter for a language key.</summary>
    /// <param name="language">Language key.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Parses an invariant decimal literal, keeping its scale (trailing zeros).</summary>
    /// <param name="number">Invariant literal.</param>
    /// <returns>The decimal value.</returns>
    private static decimal D(string number) => decimal.Parse(number, CultureInfo.InvariantCulture);

    /// <summary>Gets options selecting a reading mode.</summary>
    /// <param name="mode">Reading mode.</param>
    /// <param name="omitZeroDecimals">Whether a zero decimal part is omitted.</param>
    /// <returns>The options.</returns>
    private static DecimalFormatOptions Mode(DecimalReadingMode mode, bool omitZeroDecimals = false)
        => new() { ReadingMode = mode, OmitZeroDecimals = omitZeroDecimals };

    // ── LanguageDefault: digit-by-digit languages ───────────────────────────────────────────────

    /// <summary>Every decimal zero is read in a digit-by-digit language: leading, trailing, internal, repeated.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("NL", "21.05", "eenentwintig komma nul vijf")]
    [DataRow("NL", "21.50", "eenentwintig komma vijf nul")]
    [DataRow("NL", "21.505", "eenentwintig komma vijf nul vijf")]
    [DataRow("NL", "21.005", "eenentwintig komma nul nul vijf")]
    [DataRow("NL", "0.05", "nul komma nul vijf")]
    [DataRow("NL", "-21.05", "min eenentwintig komma nul vijf")]
    [DataRow("NL", "1000000.07", "een miljoen komma nul zeven")]
    [DataRow("JA", "21.05", "二十一点零五")]
    [DataRow("JA", "21.505", "二十一点五零五")]
    [DataRow("JA", "-21.05", "マイナス 二十一点零五")]
    [DataRow("ZH", "21.05", "二十一点零五")]
    [DataRow("ZH", "21.005", "二十一点零零五")]
    [DataRow("ZH", "-21.05", "负 二十一点零五")]
    public void LanguageDefault_DigitByDigit_ZeroDigitsAreRead(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(D(number)));

    /// <summary>The zero padding of <c>mandatoryDecimalDigits</c> is read, including a whole zero decimal part.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Value.</param>
    /// <param name="digits">Mandatory decimal digits.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("NL", 21.5, 2, "eenentwintig komma vijf nul")]
    [DataRow("NL", 21.5, 3, "eenentwintig komma vijf nul nul")]
    [DataRow("NL", 21.0, 2, "eenentwintig komma nul nul")]
    [DataRow("NL", 0.0, 2, "nul komma nul nul")]
    [DataRow("NL", 21.005, 3, "eenentwintig komma nul nul vijf")]
    [DataRow("NL", -21.05, 2, "min eenentwintig komma nul vijf")]
    [DataRow("ZH", 21.5, 3, "二十一点五零零")]
    [DataRow("ZH", 21.0, 2, "二十一点零零")]
    public void LanguageDefault_DigitByDigit_MandatoryDigitsAreRead(string language, double number, int digits, string expected)
        => Assert.AreEqual(expected, Get(language).Convert((decimal)number, digits));

    /// <summary>Decimal parts without a zero digit are byte-identical to the output before NTS-30A.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text (captured on master before the change).</param>
    [TestMethod]
    [DataRow("NL", "21.57", "eenentwintig komma vijf zeven")]
    [DataRow("NL", "123456.789", "honderddrieëntwintigduizend vierhonderdzesenvijftig komma zeven acht negen")]
    [DataRow("JA", "21.57", "二十一点五七")]
    [DataRow("ZH", "21.57", "二十一点五七")]
    public void LanguageDefault_DigitByDigit_NonZeroDigitsAreUnchanged(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(D(number)));

    /// <summary>
    /// A pronounced decimal digit is the standalone cardinal of that digit, not its level-1 table text, which is a
    /// composition form in three languages: FR <c>et un</c> (<i>vingt et un</i>), DE <c>ein</c> (<i>einundzwanzig</i>),
    /// HU <c>két</c> (attributive, <i>kétszáz</i>).
    /// </summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("DE", "-3.14", "minus drei komma eins vier")]
    [DataRow("DE", "1.12", "eins komma eins zwei")]
    [DataRow("HU", "1.2", "egyegészkettő")]
    public void DigitByDigit_DigitsAreStandaloneCardinals(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(D(number)));

    /// <summary>
    /// Digit mapping in digit-by-digit mode: a decimal 0 is <see cref="NumberToStringConverter.Zero"/> and 1–9 are
    /// the standalone cardinals of the digits, in every language without a final phrase adjustment of the digit.
    /// </summary>
    [TestMethod]
    public void DigitMapping_ZeroIsZero_OtherDigitsAreStandaloneCardinals()
    {
        foreach (var language in new[] { "NL", "JA", "ZH", "KO", "FR" })
        {
            var converter = Get(language);
            var options = Mode(DecimalReadingMode.DigitByDigit);
            string prefix = converter.Convert(7m, 0) + converter.Separator + converter.DecimalSeparator + converter.Separator;
            for (int d = 0; d <= 9; d++)
            {
                string expectedDigit = d == 0 ? converter.Zero : converter.Convert(d);
                Assert.AreEqual((prefix + expectedDigit).Trim(), converter.Convert(7m + d / 10m, 1, options), $"{language} digit {d}");
            }
        }
    }

    /// <summary>The maximum decimal precision (28 digits) is read in full.</summary>
    [TestMethod]
    public void DigitByDigit_MaximumPrecision_EveryDigitIsRead()
    {
        Assert.AreEqual("nul komma " + string.Join(" ", Enumerable.Repeat("nul", 27)) + " een",
            Get("NL").Convert(0.0000000000000000000000000001m));
        Assert.AreEqual("零点" + string.Concat(Enumerable.Repeat("一零", 14)),
            Get("ZH").Convert(0.1010101010101010101010101010m, 28));
    }

    // ── LanguageDefault: named fractions ────────────────────────────────────────────────────────

    /// <summary>The configured fractions up to three digits are byte-identical to the output before NTS-30A.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text (captured on master before the change).</param>
    [TestMethod]
    [DataRow("EN", "21.05", "twenty-one point five hundredths")]
    [DataRow("EN", "21.50", "twenty-one point fifty hundredths")]
    [DataRow("EN", "21.005", "twenty-one point five thousandths")]
    [DataRow("EN", "0.05", "zero point five hundredths")]
    [DataRow("EN", "-21.05", "minus twenty-one point five hundredths")]
    [DataRow("FR", "21.05", "vingt et un virgule cinq centièmes")]
    [DataRow("FR", "21.505", "vingt et un virgule cinq cent cinq millièmes")]
    [DataRow("FR", "0.05", "zéro virgule cinq centièmes")]
    public void LanguageDefault_NamedFractions_AreUnchanged(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(D(number)));

    /// <summary>The configured fraction branch with zero padding is unchanged (the numerator 0 is the cardinal zero).</summary>
    [TestMethod]
    public void LanguageDefault_NamedFractions_MandatoryDigits_AreUnchanged()
    {
        Assert.AreEqual("twenty-one point zero hundredth", Get("EN").Convert(21m, 2));
        Assert.AreEqual("twenty-one point five hundred thousandths", Get("EN").Convert(21.5m, 3));
        Assert.AreEqual("vingt et un virgule zéro centième", Get("FR").Convert(21m, 2));
    }

    /// <summary>
    /// French names the denominator beyond the thousandths, hyphenated (OQLF <i>dix-millièmes</i>, <i>cent-millièmes</i>;
    /// Académie <i>millionième</i>, <i>milliardième</i>): configured for 4 to 11 digits.
    /// </summary>
    /// <param name="language">Language key (FR and its regional children, which inherit the fractions).</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("FR", "0.0001", "zéro virgule un dix-millième")]
    [DataRow("FR", "100.0001", "cent virgule un dix-millième")]
    [DataRow("FR", "21.0005", "vingt et un virgule cinq dix-millièmes")]
    [DataRow("FR", "0.00001", "zéro virgule un cent-millième")]
    [DataRow("FR", "0.000001", "zéro virgule un millionième")]
    [DataRow("FR", "0.000003", "zéro virgule trois millionièmes")]
    [DataRow("FR", "0.0000001", "zéro virgule un dix-millionième")]
    [DataRow("FR", "0.00000001", "zéro virgule un cent-millionième")]
    [DataRow("FR", "0.000000001", "zéro virgule un milliardième")]
    [DataRow("FR", "0.0000000001", "zéro virgule un dix-milliardième")]
    [DataRow("FR", "0.00000000001", "zéro virgule un cent-milliardième")]
    [DataRow("FR-be", "0.0001", "zéro virgule un dix-millième")]
    [DataRow("FR-ch", "0.00071", "zéro virgule septante et un cent-millièmes")]
    [DataRow("FR-ca", "0.000001", "zéro virgule un millionième")]
    public void LanguageDefault_French_NamesTheDenominatorThroughTheMilliard(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(D(number)));

    /// <summary>French fractions cover 1–11 digits; from 12 digits (no sourced denominator) the digits are read.</summary>
    [TestMethod]
    public void LanguageDefault_French_BeyondTheSourcedRange_ReadsDigits()
    {
        var converter = Get("FR");
        CollectionAssert.AreEquivalent(Enumerable.Range(1, 11).ToArray(), converter.Fractions.Keys.ToArray());
        Assert.AreEqual("zéro virgule " + string.Join(" ", Enumerable.Repeat("zéro", 11)) + " un",
            converter.Convert(0.000000000001m));
    }

    /// <summary>English names ten-thousandths and hundred-thousandths (OpenStax), then reads digits.</summary>
    [TestMethod]
    public void LanguageDefault_English_TenAndHundredThousandths()
    {
        Assert.AreEqual("one hundred point one ten-thousandth", Get("EN").Convert(100.0001m));
        Assert.AreEqual("zero point twelve hundred-thousandths", Get("EN").Convert(0.00012m));
        Assert.AreEqual("zero point zero zero zero zero zero one", Get("EN").Convert(0.000001m));
    }

    /// <summary>Power-of-ten fractions use the same named denominators.</summary>
    [TestMethod]
    public void ConvertFraction_PowerOfTen_UsesTheNamedDenominators()
    {
        Assert.AreEqual("deux mille cinq dix-millièmes", Get("FR").ConvertFraction(2005, 10000));
        Assert.AreEqual("un millionième", Get("FR").ConvertFraction(1, 1000000));
        Assert.AreEqual("seven ten-thousandths", Get("EN").ConvertFraction(7, 10000));
    }

    // ── DigitByDigit mode ───────────────────────────────────────────────────────────────────────

    /// <summary>The forced digit-by-digit mode reads every digit, even where a fraction is configured.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("FR", "0.0001", "zéro virgule zéro zéro zéro un")]
    [DataRow("FR", "21.05", "vingt et un virgule zéro cinq")]
    [DataRow("FR", "21.505", "vingt et un virgule cinq zéro cinq")]
    [DataRow("FR", "-21.05", "moins vingt et un virgule zéro cinq")]
    [DataRow("EN", "21.005", "twenty-one point zero zero five")]
    [DataRow("NL", "21.05", "eenentwintig komma nul vijf")]
    [DataRow("NL", "21.005", "eenentwintig komma nul nul vijf")]
    [DataRow("JA", "21.05", "二十一点零五")]
    [DataRow("ZH", "21.05", "二十一点零五")]
    [DataRow("ZH", "21.505", "二十一点五零五")]
    public void DigitByDigit_ReadsEveryDigit(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(D(number), -1, Mode(DecimalReadingMode.DigitByDigit)));

    /// <summary>Mandatory padding is read in the forced digit-by-digit mode.</summary>
    [TestMethod]
    public void DigitByDigit_MandatoryDigits()
    {
        var options = Mode(DecimalReadingMode.DigitByDigit);
        Assert.AreEqual("vingt et un virgule cinq zéro zéro", Get("FR").Convert(21.5m, 3, options));
        Assert.AreEqual("eenentwintig komma vijf nul nul", Get("NL").Convert(21.5m, 3, options));
        Assert.AreEqual("twenty-one point zero zero", Get("EN").Convert(21m, 2, options));
    }

    // ── MathematicalFraction mode ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A zero integer part gives the fraction alone (no zero, no separator); a non-zero one is joined by the
    /// language's mixed-number connector (Éduscol: <i>2,4 se lira « deux et quatre dixièmes »</i>; OpenStax:
    /// <i>5.24 = five and twenty-four hundredths</i>, <i>0.05 = five hundredths</i>).
    /// </summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("FR", "0.1", "un dixième")]
    [DataRow("FR", "0.01", "un centième")]
    [DataRow("FR", "0.001", "un millième")]
    [DataRow("FR", "0.0001", "un dix-millième")]
    [DataRow("FR", "0.0002", "deux dix-millièmes")]
    [DataRow("FR", "0.000001", "un millionième")]
    [DataRow("FR", "2.4", "deux et quatre dixièmes")]
    [DataRow("FR", "2.0001", "deux et un dix-millième")]
    [DataRow("FR", "-0.0001", "moins un dix-millième")]
    [DataRow("FR", "-2.0001", "moins deux et un dix-millième")]
    [DataRow("FR-ch", "71.71", "septante et un et septante et un centièmes")]
    [DataRow("EN", "0.05", "five hundredths")]
    [DataRow("EN", "5.24", "five and twenty-four hundredths")]
    [DataRow("EN", "2.0001", "two and one ten-thousandth")]
    [DataRow("EN", "-2.074", "minus two and seventy-four thousandths")]
    public void MathematicalFraction_ReadsTheNamedFraction(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(D(number), -1, Mode(DecimalReadingMode.MathematicalFraction)));

    /// <summary>Mandatory digits keep their precision: 21.5 with three digits is five hundred thousandths.</summary>
    [TestMethod]
    public void MathematicalFraction_MandatoryDigits_KeepThePrecision()
    {
        var options = Mode(DecimalReadingMode.MathematicalFraction);
        Assert.AreEqual("vingt et un et cinq cents millièmes", Get("FR").Convert(21.5m, 3, options));
        Assert.AreEqual("twenty-one and five hundred thousandths", Get("EN").Convert(21.5m, 3, options));
    }

    /// <summary>A zero decimal part is suppressed: no "et zéro centième", whatever <c>OmitZeroDecimals</c> says.</summary>
    [TestMethod]
    public void MathematicalFraction_ZeroDecimalPart_IsSuppressed()
    {
        Assert.AreEqual("deux", Get("FR").Convert(2m, 4, Mode(DecimalReadingMode.MathematicalFraction)));
        Assert.AreEqual("deux", Get("FR").Convert(2m, 4, Mode(DecimalReadingMode.MathematicalFraction, omitZeroDecimals: true)));
        Assert.AreEqual("zéro", Get("FR").Convert(0m, 2, Mode(DecimalReadingMode.MathematicalFraction)));
        Assert.AreEqual("nul", Get("NL").Convert(0m, 2, Mode(DecimalReadingMode.MathematicalFraction)),
            "a zero decimal part needs no denominator");
    }

    /// <summary>Without a named denominator for the digit count, the mathematical mode fails closed.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    [TestMethod]
    [DataRow("FR", "0.000000000001")]
    [DataRow("EN", "0.000001")]
    [DataRow("NL", "0.05")]
    [DataRow("JA", "21.05")]
    public void MathematicalFraction_WithoutDenominator_Throws(string language, string number)
        => Assert.ThrowsExactly<NotSupportedException>(
            () => Get(language).Convert(D(number), -1, Mode(DecimalReadingMode.MathematicalFraction)));

    // ── OmitZeroDecimals and DecimalSuffix across modes ─────────────────────────────────────────

    /// <summary><see cref="DecimalFormatOptions.OmitZeroDecimals"/> suppresses a zero decimal part in every mode.</summary>
    /// <param name="mode">Reading mode.</param>
    [TestMethod]
    [DataRow(DecimalReadingMode.LanguageDefault)]
    [DataRow(DecimalReadingMode.DigitByDigit)]
    [DataRow(DecimalReadingMode.MathematicalFraction)]
    public void OmitZeroDecimals_SuppressesTheZeroDecimalPart_InEveryMode(DecimalReadingMode mode)
    {
        Assert.AreEqual("vingt et un", Get("FR").Convert(21m, 2, Mode(mode, omitZeroDecimals: true)));
        Assert.AreEqual("eenentwintig", Get("NL").Convert(21m, 2, Mode(mode, omitZeroDecimals: true)));
        Assert.AreEqual("零", Get("JA").Convert(0m, 2, Mode(mode, omitZeroDecimals: true)));
    }

    /// <summary>A non-zero decimal part keeps its zeros under <see cref="DecimalFormatOptions.OmitZeroDecimals"/>.</summary>
    [TestMethod]
    public void OmitZeroDecimals_KeepsZerosOfANonZeroDecimalPart()
        => Assert.AreEqual("eenentwintig komma nul vijf",
            Get("NL").Convert(21.05m, 2, new DecimalFormatOptions { OmitZeroDecimals = true }));

    /// <summary>
    /// A caller <see cref="DecimalFormatOptions.DecimalSuffix"/> takes precedence over every reading mode: the digits
    /// are read as one numerator after the decimal separator word, unchanged by NTS-30A.
    /// </summary>
    /// <param name="mode">Reading mode.</param>
    [TestMethod]
    [DataRow(DecimalReadingMode.LanguageDefault)]
    [DataRow(DecimalReadingMode.DigitByDigit)]
    [DataRow(DecimalReadingMode.MathematicalFraction)]
    public void DecimalSuffix_TakesPrecedenceOverTheReadingMode(DecimalReadingMode mode)
    {
        var options = new DecimalFormatOptions { DecimalSuffix = "cent(s)", ReadingMode = mode };
        Assert.AreEqual("eenentwintig komma vijf cents", Get("NL").Convert(21.05m, -1, options));
        Assert.AreEqual("eenentwintig komma vijf cents", Get("NL").Convert(21.005m, -1, options));
        Assert.AreEqual("eenentwintig komma nul cent", Get("NL").Convert(21m, 2, options));
        Assert.AreEqual("twenty-one point five cents", Get("EN").Convert(21.05m, -1, options));
    }

    // ── Configuration: decimalDigitByDigitMaxDigits and mixedFractionSeparator ──────────────────

    /// <summary>Creates a unique culture name so tests never share registry state.</summary>
    /// <returns>The culture name.</returns>
    private static string NewCulture() => $"NTS30-{Guid.NewGuid():N}";

    /// <summary>Creates a synthetic one-digit-group language (0–9) with optional decimal attributes and fractions.</summary>
    /// <param name="culture">Culture identifier.</param>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="fractions">An optional <c>&lt;Fractions&gt;</c> element.</param>
    /// <param name="extraLanguages">Additional Language elements appended to the document.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string attributes = "", string fractions = "", string extraLanguages = "")
    {
        string[] units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];
        string digits = string.Concat(units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="1" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="9" {attributes}>
                <Culture>{culture}</Culture>
                <Groups><Group level="1">{digits}</Group></Groups>
                {fractions}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds a synthetic converter.</summary>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="fractions">An optional <c>&lt;Fractions&gt;</c> element.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string attributes = "", string fractions = "")
    {
        string culture = NewCulture();
        return NumberToStringConverter.ReadConfiguration(Document(culture, attributes, fractions))[culture];
    }

    /// <summary>Without the attribute the automatic digit-by-digit fallback stays unlimited (backward compatible).</summary>
    [TestMethod]
    public void FallbackLimit_Absent_IsUnlimited()
    {
        var converter = Build();
        Assert.IsNull(converter.DecimalDigitByDigitMaxDigits);
        Assert.AreEqual("one point two three four five six seven eight nine one two", converter.Convert(1.2345678912m));
        Assert.AreEqual("one point zero zero five", converter.Convert(1.005m));
    }

    /// <summary>A positive limit allows the automatic fallback up to that many digits only.</summary>
    [TestMethod]
    public void FallbackLimit_Positive_LimitsTheAutomaticFallbackOnly()
    {
        var converter = Build("""decimalDigitByDigitMaxDigits="3" """);
        Assert.AreEqual(3, converter.DecimalDigitByDigitMaxDigits);
        Assert.AreEqual("one point two three four", converter.Convert(1.234m));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.Convert(1.2345m));
        Assert.AreEqual("one point two three four five",
            converter.Convert(1.2345m, -1, Mode(DecimalReadingMode.DigitByDigit)));
        Assert.AreEqual("one point five SFX",
            converter.Convert(1.0005m, -1, new DecimalFormatOptions { DecimalSuffix = "SFX" }),
            "a caller suffix is not limited");
    }

    /// <summary>A configured fraction is used before the limit is considered.</summary>
    [TestMethod]
    public void FallbackLimit_ConfiguredFraction_StillApplies()
    {
        var converter = Build("""decimalDigitByDigitMaxDigits="3" """,
            """<Fractions><Fraction digits="4" string="tenthousandth(s)" /></Fractions>""");
        Assert.AreEqual("one point five tenthousandths", converter.Convert(1.0005m));
    }

    /// <summary>Zero disables the automatic fallback but not the forced digit-by-digit mode.</summary>
    [TestMethod]
    public void FallbackLimit_Zero_DisablesTheAutomaticFallback()
    {
        var converter = Build("""decimalDigitByDigitMaxDigits="0" """);
        Assert.AreEqual(0, converter.DecimalDigitByDigitMaxDigits);
        Assert.ThrowsExactly<NotSupportedException>(() => converter.Convert(1.5m));
        Assert.AreEqual("one point five", converter.Convert(1.5m, -1, Mode(DecimalReadingMode.DigitByDigit)));
        Assert.AreEqual("one", converter.Convert(1m), "an integer value has no decimal part to read");
    }

    /// <summary>A negative limit is rejected by the schema.</summary>
    [TestMethod]
    public void FallbackLimit_Negative_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build("""decimalDigitByDigitMaxDigits="-1" """));

    /// <summary>A negative programmatic limit is rejected by the converter.</summary>
    [TestMethod]
    public void FallbackLimit_NegativeProgrammatic_IsRejected()
    {
        var options = new NumberToStringConverterOptions(Build()) { DecimalDigitByDigitMaxDigits = -1 };
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NumberToStringConverter(options));
    }

    /// <summary>
    /// <c>baseOn</c>: a child without the attributes inherits them; a child's explicit 0 overrides the parent's
    /// limit (0 is not "absent").
    /// </summary>
    [TestMethod]
    public void DecimalAttributes_AreInheritedThroughBaseOn()
    {
        string parent = NewCulture();
        string inheriting = NewCulture();
        string overriding = NewCulture();
        string document = Document(parent, """decimalDigitByDigitMaxDigits="3" mixedFractionSeparator="and" """,
            """<Fractions><Fraction digits="1" string="tenth(s)" /></Fractions>""",
            $"""
            <Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language>
            <Language baseOn="{parent}" decimalDigitByDigitMaxDigits="0"><Culture>{overriding}</Culture></Language>
            """);
        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual(3, converters[inheriting].DecimalDigitByDigitMaxDigits);
        Assert.AreEqual("and", converters[inheriting].MixedFractionSeparator);
        Assert.AreEqual("two and five tenths", converters[inheriting].Convert(2.5m, -1, Mode(DecimalReadingMode.MathematicalFraction)));
        Assert.AreEqual(0, converters[overriding].DecimalDigitByDigitMaxDigits);
        Assert.ThrowsExactly<NotSupportedException>(() => converters[overriding].Convert(1.25m));
    }

    /// <summary>Cloning a converter through its options keeps both decimal settings.</summary>
    [TestMethod]
    public void DecimalAttributes_OptionsClone_PreservesThem()
    {
        var original = Build("""decimalDigitByDigitMaxDigits="2" mixedFractionSeparator="and" """);
        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));
        Assert.AreEqual(2, clone.DecimalDigitByDigitMaxDigits);
        Assert.AreEqual("and", clone.MixedFractionSeparator);
    }

    /// <summary>
    /// Without a mixed-number connector the mathematical mode supports a zero integer part only: a non-zero integer
    /// part fails closed instead of inventing a connector.
    /// </summary>
    [TestMethod]
    public void MathematicalFraction_WithoutMixedConnector_FailsClosedForANonZeroIntegerPart()
    {
        var converter = Build(fractions: """<Fractions><Fraction digits="1" string="tenth(s)" /></Fractions>""");
        Assert.IsNull(converter.MixedFractionSeparator);
        var options = Mode(DecimalReadingMode.MathematicalFraction);
        Assert.AreEqual("five tenths", converter.Convert(0.5m, -1, options));
        Assert.AreEqual("minus five tenths", converter.Convert(-0.5m, -1, options));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.Convert(2.5m, -1, options));
        Assert.AreEqual("two", converter.Convert(2m, 1, options), "a zero decimal part needs no connector");
    }

    /// <summary>The built-in mixed-number connectors are the sourced ones only.</summary>
    [TestMethod]
    public void MixedFractionSeparator_BuiltInLanguages()
    {
        Assert.AreEqual("et", Get("FR").MixedFractionSeparator);
        Assert.AreEqual("et", Get("FR-be").MixedFractionSeparator);
        Assert.AreEqual("and", Get("EN").MixedFractionSeparator);
        Assert.AreEqual("and", Get("EN-GB").MixedFractionSeparator);
        Assert.IsNull(Get("NL").MixedFractionSeparator);
        Assert.IsNull(Get("DE").MixedFractionSeparator);
    }

    /// <summary>No built-in language declares a fallback limit: none was justified by the audit.</summary>
    [TestMethod]
    public void FallbackLimit_NoBuiltInLanguageDeclaresOne()
    {
        foreach (var language in new[] { "FR", "EN", "NL", "DE", "JA", "ZH", "KO", "VN" })
            Assert.IsNull(Get(language).DecimalDigitByDigitMaxDigits, language);
    }
}
