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
    // ─── Item 22 — VN intraGroupConnector ──────────────────────────────────

    [TestMethod]
    public void Convert_VN_IntraGroupConnector_101_To_109()
    {
        var vn = NumberToStringConverter.GetConverter("VN");
        Assert.AreEqual("một trăm linh một",  vn.Convert(101));
        Assert.AreEqual("một trăm linh hai",  vn.Convert(102));
        Assert.AreEqual("một trăm linh năm",  vn.Convert(105));
        Assert.AreEqual("một trăm linh chín", vn.Convert(109));
    }

    [TestMethod]
    public void Convert_VN_NoConnector_Above_Threshold()
    {
        var vn = NumberToStringConverter.GetConverter("VN");
        // 110 = mười (tens digit = 1) → no linh since remainder=10 >= threshold=10
        Assert.AreEqual("một trăm mười",           vn.Convert(110));
        Assert.AreEqual("một trăm hai mươi mốt",   vn.Convert(121));
        // 200 has no remainder — no connector
        Assert.AreEqual("hai trăm",                vn.Convert(200));
    }

    [TestMethod]
    public void Convert_VN_1To9_NoConnector()
    {
        var vn = NumberToStringConverter.GetConverter("VN");
        // Small numbers (no hundreds) must NOT get the connector
        Assert.AreEqual("một",  vn.Convert(1));
        Assert.AreEqual("năm",  vn.Convert(5));
        Assert.AreEqual("chín", vn.Convert(9));
    }

    [TestMethod]
    public void Convert_VN_201_To_209_WithConnector()
    {
        var vn = NumberToStringConverter.GetConverter("VN");
        Assert.AreEqual("hai trăm linh một", vn.Convert(201));
        Assert.AreEqual("hai trăm linh chín", vn.Convert(209));
    }

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

    // ─── Item 23 — HR (Croatian) ───────────────────────────────────────────

    [TestMethod]
    public void Convert_HR_BasicNumbers()
    {
        var hr = NumberToStringConverter.GetConverter("HR");
        Assert.AreEqual("nula",    hr.Convert(0));
        Assert.AreEqual("jedan",   hr.Convert(1));
        Assert.AreEqual("dva",     hr.Convert(2));
        Assert.AreEqual("deset",   hr.Convert(10));
        Assert.AreEqual("jedanaest", hr.Convert(11));
        Assert.AreEqual("dvanaest",  hr.Convert(12));
        Assert.AreEqual("devetnaest", hr.Convert(19));
        Assert.AreEqual("dvadeset",  hr.Convert(20));
        Assert.AreEqual("dvadeset jedan", hr.Convert(21));
        Assert.AreEqual("sto",     hr.Convert(100));
        Assert.AreEqual("sto jedan", hr.Convert(101));
        Assert.AreEqual("dvjesto", hr.Convert(200));
    }

    [TestMethod]
    public void Convert_HR_Thousands()
    {
        var hr = NumberToStringConverter.GetConverter("HR");
        Assert.AreEqual("tisuća",          hr.Convert(1000));
        Assert.AreEqual("dva tisuća",      hr.Convert(2000));
        Assert.AreEqual("deset tisuća",    hr.Convert(10_000));
        Assert.AreEqual("jedan milijun",   hr.Convert(1_000_000));
        Assert.AreEqual("dva milijun",     hr.Convert(2_000_000));
        Assert.AreEqual("jedan milijarda", hr.Convert(1_000_000_000));
    }

    [TestMethod]
    public void Convert_HR_LargeScale()
    {
        var hr = NumberToStringConverter.GetConverter("HR");
        Assert.AreEqual("jedan bilijun",   hr.Convert(1_000_000_000_000L));
    }

    // ─── Item 23 — HU (Hungarian) ─────────────────────────────────────────

    [TestMethod]
    public void Convert_HU_BasicNumbers()
    {
        var hu = NumberToStringConverter.GetConverter("HU");
        Assert.AreEqual("nulla",    hu.Convert(0));
        Assert.AreEqual("egy",      hu.Convert(1));
        Assert.AreEqual("kettő",    hu.Convert(2));   // exception: standalone
        Assert.AreEqual("három",    hu.Convert(3));
        Assert.AreEqual("tíz",      hu.Convert(10));
        Assert.AreEqual("tizenegy", hu.Convert(11));
        Assert.AreEqual("tizenkét", hu.Convert(12));
        Assert.AreEqual("húsz",     hu.Convert(20));
        Assert.AreEqual("huszonegy", hu.Convert(21));
        Assert.AreEqual("harminc",  hu.Convert(30));
        Assert.AreEqual("harmincegy", hu.Convert(31));
    }

    [TestMethod]
    public void Convert_HU_Hundreds()
    {
        var hu = NumberToStringConverter.GetConverter("HU");
        Assert.AreEqual("száz",        hu.Convert(100));   // "egyszáz" → "száz"
        Assert.AreEqual("százegy",     hu.Convert(101));
        Assert.AreEqual("kétszáz",     hu.Convert(200));
        Assert.AreEqual("háromszáz",   hu.Convert(300));
        Assert.AreEqual("négyszáz",    hu.Convert(400));
        Assert.AreEqual("kétszázhuszonegy", hu.Convert(221));
    }

    [TestMethod]
    public void Convert_HU_Thousands()
    {
        var hu = NumberToStringConverter.GetConverter("HU");
        Assert.AreEqual("ezer",       hu.Convert(1000));    // "egyezer" → "ezer"
        Assert.AreEqual("kétezer",    hu.Convert(2000));    // "két" + "ezer"
        Assert.AreEqual("tízezer",    hu.Convert(10_000));
        Assert.AreEqual("millió",     hu.Convert(1_000_000));   // "egymillió" → "millió"
        Assert.AreEqual("kétmillió",  hu.Convert(2_000_000));   // "két" + "millió"
        Assert.AreEqual("milliárd",   hu.Convert(1_000_000_000L));
    }

    [TestMethod]
    public void Convert_HU_LargeScale()
    {
        var hu = NumberToStringConverter.GetConverter("HU");
        // Scale 4: bi+ll+ió = billió
        Assert.AreEqual("billió", hu.Convert(1_000_000_000_000L));
    }

    [TestMethod]
    public void ConvertOrdinal_HU_Exceptions()
    {
        var hu = NumberToStringConverter.GetConverter("HU");
        Assert.IsTrue(hu.SupportsOrdinals);
        Assert.AreEqual("első",     hu.ConvertOrdinal(1));
        Assert.AreEqual("második",  hu.ConvertOrdinal(2));
        Assert.AreEqual("harmadik", hu.ConvertOrdinal(3));
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
        Assert.ThrowsException<NotSupportedException>(() => hr.Convert(new TimeSpan(1, 0, 0)));
    }
}
