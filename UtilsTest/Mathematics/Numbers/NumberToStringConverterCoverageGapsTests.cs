using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for items 35-38 of TODO.md: closing cross-language coverage gaps for features
/// previously tested almost exclusively on EN/FR (double/float, DecimalFormatOptions,
/// TimeSpan/DateOnly/DateTime, BigInteger significant digits, ConvertYear negative + variants).
/// </summary>
[TestClass]
public class NumberToStringConverterCoverageGapsTests
{
    // ─── Item 35 — Convert(double/float) + DecimalFormatOptions on DE/ES/IT ─

    [TestMethod]
    public void Convert_Double_DE_WithGenderVariant()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        Assert.AreEqual("eine komma fünf", de.Convert(1.5, "gender=feminin"));
    }

    [TestMethod]
    public void Convert_Double_ES_Basic()
    {
        var es = NumberToStringConverter.GetConverter("ES");
        Assert.AreEqual("uno coma cinco", es.Convert(1.5));
    }

    [TestMethod]
    public void Convert_Double_IT_Basic()
    {
        var it = NumberToStringConverter.GetConverter("IT");
        Assert.AreEqual("uno virgola cinque", it.Convert(1.5));
    }

    [TestMethod]
    public void DecimalFormatOptions_DE_SeparatorAndSuffix_Pluralized()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        var opts = new DecimalFormatOptions { DecimalSeparator = "Euro(s)", DecimalSuffix = "Cent(s)" };
        Assert.AreEqual("einundzwanzig Euros fünfzig Cents", de.Convert(21.50m, 2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_ES_SeparatorAndSuffix_Pluralized()
    {
        var es = NumberToStringConverter.GetConverter("ES");
        var opts = new DecimalFormatOptions { DecimalSeparator = "euro(s)", DecimalSuffix = "centimo(s)" };
        Assert.AreEqual("veintiuno euros cincuenta centimos", es.Convert(21.50m, 2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_IT_SeparatorAndSuffix_Pluralized()
    {
        var it = NumberToStringConverter.GetConverter("IT");
        var opts = new DecimalFormatOptions { DecimalSeparator = "euro(s)", DecimalSuffix = "centesimo(s)" };
        Assert.AreEqual("venti uno euros cinquanta centesimos", it.Convert(21.50m, 2, opts));
    }

    // ─── Item 36 — Convert(TimeSpan/DateOnly/DateTime) not tested for DE ────

    [TestMethod]
    public void Convert_DateTime_DE_ContainsDateAndTimeParts()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        var dt = new System.DateTime(2026, 7, 2, 14, 30, 5);
        string result = de.Convert(dt);
        Assert.IsTrue(result.Contains("Juli"), $"Actual: {result}");
        Assert.IsTrue(result.Contains("Stunden"), $"Actual: {result}");
    }

    [TestMethod]
    public void Convert_DateOnly_DE_FirstDay_OrdinalDay()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        // firstDay="ersten" also applies to {ordinal-day} when day == 1 (not just {cardinal-day})
        string result = de.Convert(new System.DateOnly(2026, 7, 1));
        Assert.IsTrue(result.Contains("ersten"), $"Actual: {result}");
    }

    // ─── Item 37 — Convert(BigInteger, significantDigits) per language ──────

    [TestMethod]
    public void Convert_BigInteger_SignificantDigits_DE()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        Assert.AreEqual("einhundert Millionen", de.Convert((BigInteger)123456789, 1));
    }

    [TestMethod]
    public void Convert_BigInteger_SignificantDigits_ES_WithGenderVariant()
    {
        var es = NumberToStringConverter.GetConverter("ES");
        // Rounding combined with a gender variant on a large number
        Assert.AreEqual("doscientas millones", es.Convert((BigInteger)223456789, 1, "gender=femenino"));
    }

    // ─── Item 38 — ConvertYear negative (beforeChristSuffix) combined with variants ─
    //
    // No shipped language XML declares beforeChristSuffix (it exists only as an engine/options
    // feature, exercised in NumberToStringConverterBatchTests via a hand-built converter).
    // These tests combine it with a gender variant, which was never exercised together before.

    [TestMethod]
    public void ConvertYear_Negative_WithGenderVariant_BeforeChristSuffix()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var options = new NumberToStringConverterOptions(fr)
        {
            YearFormat = new YearFormatOptions(null, null, null, BeforeChristSuffix: "av. J.-C.")
        };
        var converter = new NumberToStringConverter(options);

        // Year 1 BC: the numeral itself ("un"/"une") is gender-sensitive.
        Assert.AreEqual("un av. J.-C.",  converter.ConvertYear(-1));
        Assert.AreEqual("une av. J.-C.", converter.ConvertYear(-1, "gender=feminin"));
        // Positive years are unaffected by the suffix.
        Assert.AreEqual("un", converter.ConvertYear(1));
    }
}
