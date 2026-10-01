using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of the NTS-08 clock-time configurations: they prove that the idiomatic wording
/// is produced through the rule's forced variants (not a literal that happens to match), and that
/// clock-specific forms never leak into the exact cardinal and time APIs. The linguistic wording
/// itself is specified by the language <c>.feature</c> files.
/// </summary>
[TestClass]
public class NumberToStringClockTimeLanguageTests
{
    /// <summary>
    /// Ensures case-governed clock hours equal the ordinal rendered with the rule's forced
    /// gender/case, and differ from the default ordinal, so the forcing actually reaches the
    /// ordinal pipeline (plugin or declarative rules).
    /// </summary>
    [TestMethod]
    [DataRow("CS", 1, 30, "půl ", 2, "gender=ženský,case=genitiv")]
    [DataRow("SK", 1, 30, "pol ", 2, "gender=ženský,case=genitív")]
    [DataRow("UK", 1, 15, "чверть по ", 1, "gender=жіночий,case=місцевий")]
    [DataRow("UK", 1, 30, "пів на ", 2, "gender=жіночий,case=знахідний")]
    [DataRow("UK", 1, 45, "чверть до ", 2, "gender=жіночий,case=родовий")]
    [DataRow("PL", 1, 5, "pięć po ", 1, "rodzaj=feminin,przypadek=miejscownik")]
    [DataRow("PL", 1, 30, "wpół do ", 2, "rodzaj=feminin,przypadek=dopełniacz")]
    [DataRow("RU", 1, 15, "четверть ", 2, "gender=maskulin,case=родительный")]
    [DataRow("AR", 2, 0, "ال", 2, "gender=muʾannath")]
    public void ConvertClockTime_CaseGovernedHour_UsesForcedOrdinalVariants(
        string culture, int hour, int minute, string literal, int ordinal, string forced)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);
        string expectedHour = converter.ConvertOrdinal(ordinal, forced.Split(','));

        Assert.AreEqual(literal + expectedHour, converter.ConvertClockTime(new TimeOnly(hour, minute)));
        Assert.AreNotEqual(converter.ConvertOrdinal(ordinal), expectedHour, "The forcing must select a non-default form.");
    }

    /// <summary>Ensures cardinal clock hours use the rule's forced gender rather than the default cardinal.</summary>
    [TestMethod]
    [DataRow("CS", 2, "dvě hodiny", "dva")]
    [DataRow("SK", 1, "jedna hodina", "jeden")]
    [DataRow("BG", 1, "един", "едно")]
    [DataRow("DA", 1, "et", "en")]
    [DataRow("NO", 1, "ett", "en")]
    [DataRow("ES", 1, "la una", "uno")]
    [DataRow("IT", 1, "l'una", "uno")]
    [DataRow("GL", 2, "as dúas", "dous")]
    [DataRow("RO", 2, "ora două", "doi")]
    [DataRow("RO", 12, "ora douăsprezece", "doisprezece")]
    [DataRow("EL", 3, "τρεις", "τρία")]
    [DataRow("EL", 4, "τέσσερις", "τέσσερα")]
    [DataRow("HE", 1, "אחת", "אחד")]
    public void ConvertClockTime_CardinalHour_UsesForcedGender(string culture, int hour, string expectedClock, string defaultCardinal)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.AreEqual(expectedClock, converter.ConvertClockTime(new TimeOnly(hour, 0)));
        Assert.AreEqual(defaultCardinal, converter.Convert(hour));
    }

    /// <summary>
    /// Ensures adding an idiomatic clock does not change the exact time API of languages that already
    /// had <c>TimeUnits</c>: <c>Convert(TimeOnly)</c> keeps the exact hours/minutes wording.
    /// </summary>
    [TestMethod]
    [DataRow("ES", 1, 45, "una hora cuarenta y cinco minutos")]
    [DataRow("PT", 1, 30, "uma hora trinta minutos")]
    [DataRow("GL", 2, 15, "dúas horas quince minutos")]
    public void Convert_TimeOnly_IsUnchangedByClockConfiguration(string culture, int hour, int minute, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(culture).Convert(new TimeOnly(hour, minute)));

    /// <summary>Ensures the Turkish clock hour takes the rule's forced case rather than a literal form.</summary>
    [TestMethod]
    [DataRow(6, 15, "saat {0} çeyrek geçiyor", 6, "case=accusative")]
    [DataRow(3, 45, "saat {0} çeyrek var", 4, "case=dative")]
    public void ConvertClockTime_TurkishHour_UsesForcedCase(int hour, int minute, string pattern, int displayed, string forced)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("TR");
        string expectedHour = converter.Convert(displayed, forced);

        Assert.AreEqual(string.Format(pattern, expectedHour), converter.ConvertClockTime(new TimeOnly(hour, minute)));
        Assert.AreNotEqual(converter.Convert(displayed), expectedHour);
    }

    /// <summary>
    /// Ensures clock-only lexical forms (Korean native hours, Chinese 两) never leak into the
    /// cardinal and ordinal APIs.
    /// </summary>
    [TestMethod]
    [DataRow("KO", 1, "한 시", "일", "제일")]
    [DataRow("KO", 12, "열두 시", "십이", "제십이")]
    [DataRow("ZH", 2, "两点", "二", "第二")]
    public void ClockOnlyHourForms_DoNotChangeCardinalOrOrdinal(string culture, int hour, string clock, string cardinal, string ordinal)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.AreEqual(clock, converter.ConvertClockTime(new TimeOnly(hour, 0)));
        Assert.AreEqual(cardinal, converter.Convert(hour));
        Assert.AreEqual(ordinal, converter.ConvertOrdinal(hour));
    }
}
