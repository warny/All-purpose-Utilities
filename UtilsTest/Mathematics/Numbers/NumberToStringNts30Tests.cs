using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of NTS-30A: when no <c>&lt;Fraction&gt;</c> suffix applies, the decimal part is read digit by
/// digit and a digit 0 is read with the standalone <see cref="NumberToStringConverter.Zero"/>, not with the empty
/// level-1 cardinal digit (zero as an empty cardinal position is not zero as a pronounced decimal digit). The
/// numerator/suffix branch (configured fractions and <see cref="DecimalFormatOptions.DecimalSuffix"/>) is unchanged.
/// </summary>
[TestClass]
public class NumberToStringNts30Tests
{
    /// <summary>Gets the converter for a language key.</summary>
    /// <param name="language">Language key.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Every decimal zero is read in a digit-by-digit language: leading, trailing, internal, repeated.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal (its scale is kept by <see cref="decimal.Parse(string)"/>).</param>
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
    public void DigitByDigit_ZeroDigitsAreRead(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture)));

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
    public void DigitByDigit_MandatoryDigitsAreRead(string language, double number, int digits, string expected)
        => Assert.AreEqual(expected, Get(language).Convert((decimal)number, digits));

    /// <summary><see cref="DecimalFormatOptions.OmitZeroDecimals"/> still suppresses a zero decimal part entirely.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Value.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("NL", 21.0, "eenentwintig")]
    [DataRow("NL", 0.0, "nul")]
    [DataRow("ZH", 21.0, "二十一")]
    [DataRow("JA", 0.0, "零")]
    public void OmitZeroDecimals_StillOmitsTheZeroDecimalPart(string language, double number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert((decimal)number, 2, new DecimalFormatOptions { OmitZeroDecimals = true }));

    /// <summary>A non-zero decimal part keeps its zeros under <see cref="DecimalFormatOptions.OmitZeroDecimals"/>.</summary>
    [TestMethod]
    public void OmitZeroDecimals_KeepsZerosOfANonZeroDecimalPart()
        => Assert.AreEqual("eenentwintig komma nul vijf",
            Get("NL").Convert(21.05m, 2, new DecimalFormatOptions { OmitZeroDecimals = true }));

    /// <summary>Decimal parts without a zero digit are byte-identical to the output before NTS-30A.</summary>
    /// <param name="language">Language key.</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text (captured on master before the change).</param>
    [TestMethod]
    [DataRow("NL", "21.57", "eenentwintig komma vijf zeven")]
    [DataRow("NL", "123456.789", "honderddrieëntwintigduizend vierhonderdzesenvijftig komma zeven acht negen")]
    [DataRow("JA", "21.57", "二十一点五七")]
    [DataRow("ZH", "21.57", "二十一点五七")]
    public void DigitByDigit_NonZeroDigitsAreUnchanged(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>
    /// The configured <c>&lt;Fraction&gt;</c> branch reads the digits as a numerator over the named denominator; a
    /// zero position is carried by the denominator, so its output is byte-identical to the output before NTS-30A.
    /// </summary>
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
    public void NamedFractions_AreUnchanged(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>The configured fraction branch with zero padding is unchanged (the numerator 0 is the cardinal zero).</summary>
    [TestMethod]
    public void NamedFractions_MandatoryDigits_AreUnchanged()
    {
        Assert.AreEqual("twenty-one point zero hundredth", Get("EN").Convert(21m, 2));
        Assert.AreEqual("twenty-one point five hundred thousandths", Get("EN").Convert(21.5m, 3));
        Assert.AreEqual("vingt et un virgule zéro centième", Get("FR").Convert(21m, 2));
    }

    /// <summary>
    /// Beyond its configured fraction digits, EN falls back to the digit-by-digit branch, whose zeros are now read
    /// (the non-zero digits keep their level-1 text).
    /// </summary>
    [TestMethod]
    public void NamedFractionLanguages_DigitByDigitFallback_ReadsZeros()
        => Assert.AreEqual("one hundred point zero zero zero one", Get("EN").Convert(100.0001m));

    /// <summary>
    /// French names the denominator beyond the thousandths too (<c>0,0001</c> is <i>un dix millième</i>, the zeros
    /// are not read): every digit count up to the 28-digit decimal precision has a fraction suffix, built from the
    /// long-scale ordinals and spaced like the rest of the FR configuration (<c>dix millième</c>).
    /// </summary>
    /// <param name="language">Language key (FR and its regional children, which inherit the fractions).</param>
    /// <param name="number">Invariant decimal literal.</param>
    /// <param name="expected">Expected text.</param>
    [TestMethod]
    [DataRow("FR", "0.0001", "zéro virgule un dix millième")]
    [DataRow("FR", "100.0001", "cent virgule un dix millième")]
    [DataRow("FR", "21.0005", "vingt et un virgule cinq dix millièmes")]
    [DataRow("FR", "0.00001", "zéro virgule un cent millième")]
    [DataRow("FR", "0.000001", "zéro virgule un millionième")]
    [DataRow("FR", "0.000025", "zéro virgule vingt cinq millionièmes")]
    [DataRow("FR", "0.0000001", "zéro virgule un dix millionième")]
    [DataRow("FR", "0.00000001", "zéro virgule un cent millionième")]
    [DataRow("FR", "0.000000001", "zéro virgule un milliardième")]
    [DataRow("FR", "0.000000000001", "zéro virgule un billionième")]
    [DataRow("FR", "0.000000000000001", "zéro virgule un billiardième")]
    [DataRow("FR", "0.000000000000000001", "zéro virgule un trillionième")]
    [DataRow("FR", "0.000000000000000000001", "zéro virgule un trilliardième")]
    [DataRow("FR", "0.000000000000000000000001", "zéro virgule un quadrillionième")]
    [DataRow("FR", "0.000000000000000000000000001", "zéro virgule un quadrilliardième")]
    [DataRow("FR", "0.0000000000000000000000000001", "zéro virgule un dix quadrilliardième")]
    [DataRow("FR", "-3.1416", "moins trois virgule mille quatre cent seize dix millièmes")]
    [DataRow("FR-be", "0.0001", "zéro virgule un dix millième")]
    [DataRow("FR-ch", "0.00071", "zéro virgule septante et un cent millièmes")]
    [DataRow("FR-ca", "0.000001", "zéro virgule un millionième")]
    public void French_NamesTheDenominatorBeyondTheThousandths(string language, string number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Every French decimal digit count (1–28) has a fraction suffix, so FR never reads digits one by one.</summary>
    [TestMethod]
    public void French_EveryDecimalDigitCountHasASuffix()
    {
        var converter = Get("FR");
        for (int digits = 1; digits <= 28; digits++)
            Assert.IsTrue(converter.Fractions.ContainsKey(digits), $"FR has no fraction suffix for {digits} digits.");
    }

    /// <summary>French zero padding and power-of-ten fractions use the same denominators.</summary>
    [TestMethod]
    public void French_PaddingAndPowerOfTenFractions_UseTheNamedDenominators()
    {
        Assert.AreEqual("vingt et un virgule zéro dix millième", Get("FR").Convert(21m, 4));
        Assert.AreEqual("vingt et un virgule cinq mille dix millièmes", Get("FR").Convert(21.5m, 4));
        Assert.AreEqual("deux mille cinq dix millièmes", Get("FR").ConvertFraction(2005, 10000));
        Assert.AreEqual("un millionième", Get("FR").ConvertFraction(1, 1000000));
    }

    /// <summary>
    /// A caller <see cref="DecimalFormatOptions.DecimalSuffix"/> forces the numerator branch, also in a digit-by-digit
    /// language: the leading zeros stay carried by the numerator, unchanged by NTS-30A.
    /// </summary>
    [TestMethod]
    public void DecimalSuffix_ForcesTheUnchangedNumeratorBranch()
    {
        var options = new DecimalFormatOptions { DecimalSuffix = "cent(s)" };
        Assert.AreEqual("eenentwintig komma vijf cents", Get("NL").Convert(21.05m, -1, options));
        Assert.AreEqual("eenentwintig komma vijf cents", Get("NL").Convert(21.005m, -1, options));
        Assert.AreEqual("eenentwintig komma nul cent", Get("NL").Convert(21m, 2, options));
        Assert.AreEqual("twenty-one point five cents", Get("EN").Convert(21.05m, -1, options));
    }

    /// <summary>
    /// Digit mapping: in every converter whose one-digit decimal part is read digit by digit, a decimal 0 is
    /// <see cref="NumberToStringConverter.Zero"/> and 1–9 are the level-1 digit strings (languages chosen without a
    /// final phrase adjustment, unlike DE whose final <c>ein</c> becomes <c>eins</c>).
    /// </summary>
    [TestMethod]
    public void DigitMapping_ZeroIsZero_OtherDigitsAreLevelOneStrings()
    {
        foreach (var language in new[] { "NL", "JA", "ZH", "KO" })
        {
            var converter = Get(language);
            Assert.IsFalse(converter.Fractions.ContainsKey(1), $"{language} must read one decimal digit digit by digit.");
            string prefix = converter.Convert(7m, 0) + converter.Separator + converter.DecimalSeparator + converter.Separator;
            for (int d = 0; d <= 9; d++)
            {
                string expectedDigit = d == 0 ? converter.Zero : converter.Groups[1][d].StringValue;
                Assert.AreEqual((prefix + expectedDigit).Trim(), converter.Convert(7m + d / 10m, 1), $"{language} digit {d}");
            }
        }
    }

    /// <summary>
    /// The maximum decimal precision (28 digits) is read in full: 27 zeros then a one, and an alternating pattern.
    /// </summary>
    [TestMethod]
    public void MaximumPrecision_EveryDigitIsRead()
    {
        string text = Get("NL").Convert(0.0000000000000000000000000001m);
        Assert.AreEqual("nul komma " + string.Join(" ", Enumerable.Repeat("nul", 27)) + " een", text);

        string alternating = Get("ZH").Convert(0.1010101010101010101010101010m, 28);
        Assert.AreEqual("零点" + string.Concat(Enumerable.Repeat("一零", 14)), alternating);
    }
}
