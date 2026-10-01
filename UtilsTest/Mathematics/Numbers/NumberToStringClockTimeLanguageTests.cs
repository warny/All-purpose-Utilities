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
    public void ConvertClockTime_CardinalHour_UsesForcedGender(string culture, int hour, string expectedClock, string defaultCardinal)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.AreEqual(expectedClock, converter.ConvertClockTime(new TimeOnly(hour, 0)));
        Assert.AreEqual(defaultCardinal, converter.Convert(hour));
    }
}
