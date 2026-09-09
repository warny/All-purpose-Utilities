using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for: item 21 (Convert TimeSpan/TimeOnly/DateOnly/DateTime),
/// item 22 (intraGroupConnector — VN "linh"), item 23 (HR, HU new languages).
/// </summary>
[TestClass]
public class NumberToStringConverterTimeAndNewLangTests
{

    // ─── Item 21a — Convert(TimeSpan) ──────────────────────────────────────

    [TestMethod]
    public void Convert_TimeSpan_EN_HoursMinutesSeconds()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.IsTrue(en.SupportsTimeConversion);
        Assert.AreEqual("two hours thirty minutes five seconds",
            en.Convert(new TimeSpan(2, 30, 5)));
    }

    [TestMethod]
    public void Convert_TimeSpan_EN_HoursOnly()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual("one hour", en.Convert(new TimeSpan(1, 0, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_EN_MinutesSeconds()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual("thirty minutes ten seconds", en.Convert(new TimeSpan(0, 30, 10)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_HoursMinutes()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.IsTrue(fr.SupportsTimeConversion);
        Assert.AreEqual("deux heures trente minutes", fr.Convert(new TimeSpan(2, 30, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_FR_OneHour()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // "heure" is feminine: pass gender variant to get "une" instead of "un"
        Assert.AreEqual("une heure", fr.Convert(new TimeSpan(1, 0, 0), "gender=feminin"));
    }

    [TestMethod]
    public void Convert_TimeSpan_DE_HoursMinutesSeconds()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        Assert.IsTrue(de.SupportsTimeConversion);
        Assert.AreEqual("zwei Stunden dreißig Minuten fünf Sekunden",
            de.Convert(new TimeSpan(2, 30, 5)));
    }

    [TestMethod]
    public void Convert_TimeSpan_DE_OneHour_Count1Form()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        // count1form="eine" prevents "eins Stunde" — should produce "eine Stunde"
        Assert.AreEqual("eine Stunde", de.Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── Item 21b — Convert(TimeOnly) ──────────────────────────────────────

    [TestMethod]
    public void Convert_TimeOnly_EN_Basic()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        // 14:30 → "fourteen hours thirty minutes"
        Assert.AreEqual("fourteen hours thirty minutes",
            en.Convert(new TimeOnly(14, 30, 0)));
    }

    [TestMethod]
    public void Convert_TimeOnly_FR_Basic()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // 14:30 → "quatorze heures trente minutes"
        Assert.AreEqual("quatorze heures trente minutes",
            fr.Convert(new TimeOnly(14, 30, 0)));
    }

    // ─── Item 21c — Convert(DateOnly) ──────────────────────────────────────

    [TestMethod]
    public void Convert_DateOnly_EN_Basic()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.IsTrue(en.SupportsDateConversion);
        // July 2 → "July second, twenty twenty-six" (year via ConvertYear)
        string result = en.Convert(new DateOnly(2026, 7, 2));
        Assert.IsTrue(result.StartsWith("July second,"), $"Actual: {result}");
    }

    [TestMethod]
    public void Convert_DateOnly_EN_FirstOfMonth()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        // July 1 → "July first, ..."
        string result = en.Convert(new DateOnly(2026, 7, 1));
        Assert.IsTrue(result.StartsWith("July first,"), $"Actual: {result}");
    }

    [TestMethod]
    public void Convert_DateOnly_FR_Basic()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.IsTrue(fr.SupportsDateConversion);
        // 2 juillet 2026 → "deux juillet deux mille vingt-six"
        string result = fr.Convert(new DateOnly(2026, 7, 2));
        Assert.IsTrue(result.StartsWith("deux juillet"), $"Actual: {result}");
    }

    [TestMethod]
    public void Convert_DateOnly_FR_FirstOfMonth()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // firstCardinalDay="premier" applies to {cardinal-day} when day == 1
        string result = fr.Convert(new DateOnly(2026, 7, 1));
        Assert.IsTrue(result.StartsWith("premier juillet"), $"Actual: {result}");
    }

    [TestMethod]
    public void Convert_DateOnly_DE_Basic()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        Assert.IsTrue(de.SupportsDateConversion);
        // pattern="{cardinal-day}. {month} {year}", firstDay="ersten"
        // day 2: cardinal "zwei" → "zwei. Juli ..."
        string result = de.Convert(new DateOnly(2026, 7, 2));
        Assert.IsTrue(result.StartsWith("zwei. Juli"), $"Actual: {result}");
    }

    [TestMethod]
    public void Convert_DateOnly_DE_FirstDay_CardinalDay()
    {
        var de = NumberToStringConverter.GetConverter("DE");
        // firstCardinalDay="ersten" applies to {cardinal-day} when day == 1
        string result = de.Convert(new DateOnly(2026, 7, 1));
        Assert.IsTrue(result.StartsWith("ersten. Juli"), $"Actual: {result}");
    }

    // ─── Item 21d — Convert(DateTime) ──────────────────────────────────────

    [TestMethod]
    public void Convert_DateTime_EN_Basic()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var dt = new DateTime(2026, 7, 2, 14, 30, 5);
        string result = en.Convert(dt);
        // Should contain date part and time part
        Assert.IsTrue(result.Contains("July"), $"Actual: {result}");
        Assert.IsTrue(result.Contains("fourteen hours"), $"Actual: {result}");
    }

    // ─── SupportsTimeConversion / SupportsDateConversion feature flags ──────

    [TestMethod]
    public void SupportsTimeConversion_EN_FR_DE_True()
    {
        Assert.IsTrue(NumberToStringConverter.GetConverter("EN").SupportsTimeConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("FR").SupportsTimeConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("DE").SupportsTimeConversion);
    }

    [TestMethod]
    public void SupportsTimeConversion_HR_HU_False()
    {
        Assert.IsFalse(NumberToStringConverter.GetConverter("HR").SupportsTimeConversion);
        Assert.IsFalse(NumberToStringConverter.GetConverter("HU").SupportsTimeConversion);
    }

    [TestMethod]
    public void SupportsDateConversion_EN_FR_DE_True()
    {
        Assert.IsTrue(NumberToStringConverter.GetConverter("EN").SupportsDateConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("FR").SupportsDateConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("DE").SupportsDateConversion);
    }

    [TestMethod]
    public void NotSupported_TimeConversion_Throws()
    {
        var hr = NumberToStringConverter.GetConverter("HR");
        Assert.IsFalse(hr.SupportsTimeConversion);
        Assert.ThrowsExactly<NotSupportedException>(() => hr.Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── EN-GB (British English, derived from EN via baseOn) ──────────────────

    [TestMethod]
    public void Convert_EN_GB_BasicNumbers_SameAsEN()
    {
        // Numbers must be identical to EN since EN-GB inherits all number rules.
        var en = NumberToStringConverter.GetConverter("EN");
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        Assert.AreEqual(en.Convert(0),        gb.Convert(0));
        Assert.AreEqual(en.Convert(1),        gb.Convert(1));
        Assert.AreEqual(en.Convert(42),       gb.Convert(42));
        Assert.AreEqual(en.Convert(1_000),    gb.Convert(1_000));
        Assert.AreEqual(en.Convert(1_000_000), gb.Convert(1_000_000));
    }

    [TestMethod]
    public void Convert_EN_GB_DateFormat_DayBeforeMonth()
    {
        // British format: {ordinal-day} {month} {year} → "second July ..."
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        Assert.IsTrue(gb.SupportsDateConversion);
        string result = gb.Convert(new DateOnly(2026, 7, 2));
        Assert.IsTrue(result.StartsWith("second July"),
            $"British date must start with ordinal-day then month; got: '{result}'");
    }

    [TestMethod]
    public void Convert_EN_GB_DateFormat_FirstOfMonth()
    {
        // firstDay override must still apply in British format.
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        string result = gb.Convert(new DateOnly(2026, 7, 1));
        Assert.IsTrue(result.StartsWith("first July"),
            $"British date for day 1 must use 'first'; got: '{result}'");
    }

    [TestMethod]
    public void Convert_EN_US_DateFormat_MonthBeforeDay()
    {
        // American format: {month} {ordinal-day}, {year} — unchanged.
        var us = NumberToStringConverter.GetConverter("EN-us");
        string result = us.Convert(new DateOnly(2026, 7, 2));
        Assert.IsTrue(result.StartsWith("July second"),
            $"American date must start with month then ordinal-day; got: '{result}'");
    }

    [TestMethod]
    public void GetConverter_EN_uk_UsesBritishDateFormat()
    {
        // EN-uk culture alias moved to EN-GB; must use British date format.
        var uk  = NumberToStringConverter.GetConverter("EN-uk");
        string result = uk.Convert(new DateOnly(2026, 7, 2));
        Assert.IsTrue(result.StartsWith("second July"),
            $"EN-uk must use British date format; got: '{result}'");
    }

    [TestMethod]
    public void Convert_EN_GB_Ordinals_SameAsEN()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        Assert.IsTrue(gb.SupportsOrdinals);
        Assert.AreEqual(en.ConvertOrdinal(1),  gb.ConvertOrdinal(1));
        Assert.AreEqual(en.ConvertOrdinal(2),  gb.ConvertOrdinal(2));
        Assert.AreEqual(en.ConvertOrdinal(21), gb.ConvertOrdinal(21));
    }

    [TestMethod]
    public void Convert_EN_GB_ScaleNames_InheritedFromScaleShort()
    {
        // EN-GB inherits EN → SCALE-SHORT chain; scale names must match EN exactly.
        var en = NumberToStringConverter.GetConverter("EN");
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        Assert.AreEqual(en.Convert(1_000_000_000L),     gb.Convert(1_000_000_000L));
        Assert.AreEqual(en.Convert(1_000_000_000_000L), gb.Convert(1_000_000_000_000L));
    }
}
