using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Utils.Dates;

namespace UtilsTest.Objects;

[TestClass]
public class DateFormulaTests
{
    private sealed class WeekEndCalendarProvider : ICalendarProvider
    {
        private static readonly DayOfWeek[] _weekEnds = [DayOfWeek.Saturday, DayOfWeek.Sunday];

        public int GetNonWorkingDaysCount(DateTime start, DateTime end)
                        => GetHollydays(start, end).Count();

        public IEnumerable<DateTime> GetHollydays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (System.Array.IndexOf(_weekEnds, d.DayOfWeek) >= 0)
                    yield return d;
        }

        public IEnumerable<DateTime> GetWorkingDays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (System.Array.IndexOf(_weekEnds, d.DayOfWeek) < 0)
                    yield return d;
        }

        public int GetWorkingDaysCount(DateTime start, DateTime end)
                        => GetWorkingDays(start, end).Count();
    }
    [TestMethod]
    public void BasicFrenchFormulas()
    {
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("FM+1J", new CultureInfo("fr-FR")));
        Assert.AreEqual(new DateTime(2022, 12, 1), date.Calculate("DA-1M", new CultureInfo("fr-FR")));
    }

    [TestMethod]
    public void WeekDayAdjustments()
    {
        var date = new DateTime(2023, 10, 15);
        Assert.AreEqual(new DateTime(2023, 11, 6), date.Calculate("FM+1J+Lu", new CultureInfo("fr-FR")));
        Assert.AreEqual(new DateTime(2023, 10, 30), date.Calculate("FM+1JLu", new CultureInfo("fr-FR")));
    }

    /// <summary>
    /// Verifies that adjusting to the same day of week moves forward by one week when <see cref="T:after"/> is true.
    /// </summary>
    [TestMethod]
    public void AdjustToDayOfWeekAdvancesWhenOnSameDay()
    {
        var method = typeof(DateFormula).GetMethod("AdjustToDayOfWeek", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Missing AdjustToDayOfWeek method.");

        var result = (DateTime)method.Invoke(null, [new DateTime(2024, 4, 8), DayOfWeek.Monday, true])!;

        Assert.AreEqual(new DateTime(2024, 4, 15), result);
    }

    /// <summary>
    /// Ensures that adjusting backwards from an already aligned day returns the previous week day.
    /// </summary>
    [TestMethod]
    public void AdjustToDayOfWeekMovesBackwardWhenOnSameDay()
    {
        var method = typeof(DateFormula).GetMethod("AdjustToDayOfWeek", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Missing AdjustToDayOfWeek method.");

        var result = (DateTime)method.Invoke(null, [new DateTime(2024, 4, 8), DayOfWeek.Monday, false])!;

        Assert.AreEqual(new DateTime(2024, 4, 1), result);
    }

    [TestMethod]
    public void EnglishFormula()
    {
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("EM+1D", new CultureInfo("en-US")));
    }

    [TestMethod]
    public void GermanFormula()
    {
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("EM+1T", new CultureInfo("de-DE")));
    }

    /// <summary>
    /// Clones <paramref name="name"/> with an explicit calendar, so that a test does not depend on the default calendar the
    /// platform's globalization data gives the culture (ar-SA: Umm al-Qura on Windows, Gregorian on the Linux runner).
    /// </summary>
    /// <param name="name">The culture name.</param>
    /// <param name="calendar">The calendar to select; it must be one of the culture's optional calendars.</param>
    /// <returns>A writable culture using <paramref name="calendar"/>.</returns>
    private static CultureInfo WithCalendar(string name, Calendar calendar)
    {
        var culture = (CultureInfo)new CultureInfo(name).Clone();
        culture.DateTimeFormat.Calendar = calendar;
        return culture;
    }

    /// <summary>
    /// "NS+1Y" is the end of the month plus one day. 2023-03-15 is in Sha'ban 1444, which ends on 2023-03-22 in the
    /// Umm al-Qura calendar (the managed .NET table, not the platform's data).
    /// </summary>
    [TestMethod]
    public void ArabicFormulaHijri()
    {
        var culture = WithCalendar("ar-SA", new UmAlQuraCalendar());
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 3, 23), date.Calculate("NS+1Y", culture));
    }

    /// <summary>The same formula on the Gregorian calendar of the same culture ends March.</summary>
    [TestMethod]
    public void ArabicFormulaGregorian()
    {
        var culture = WithCalendar("ar-SA", new GregorianCalendar());
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("NS+1Y", culture));
    }

    /// <summary>Two cultures with the same name but different calendars do not share a cached formula.</summary>
    [TestMethod]
    public void CachedFormula_DistinguishesTheCalendar()
    {
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 3, 23), date.Calculate("NS+1Y", WithCalendar("ar-SA", new UmAlQuraCalendar())));
        Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("NS+1Y", WithCalendar("ar-SA", new GregorianCalendar())));
        Assert.AreEqual(new DateTime(2023, 3, 23), date.Calculate("NS+1Y", WithCalendar("ar-SA", new UmAlQuraCalendar())));
    }

    /// <summary>Gives the start of the month containing <paramref name="date"/> in <paramref name="calendar"/>.</summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="date">The date.</param>
    /// <returns>The first day of the calendar month.</returns>
    private static DateTime StartOfMonth(Calendar calendar, DateTime date)
        => calendar.ToDateTime(calendar.GetYear(date), calendar.GetMonth(date), 1, 0, 0, 0, 0);

    /// <summary>
    /// Two <see cref="HijriCalendar"/> instances of the same type but a different <see cref="HijriCalendar.HijriAdjustment"/>
    /// give different dates and must not share a cached formula.
    /// </summary>
    [TestMethod]
    public void CachedFormula_DistinguishesTheHijriAdjustment()
    {
        var date = new DateTime(2023, 3, 15);
        DateTime expected0 = StartOfMonth(new HijriCalendar { HijriAdjustment = 0 }, date);
        DateTime expected2 = StartOfMonth(new HijriCalendar { HijriAdjustment = 2 }, date);
        Assert.AreNotEqual(expected0, expected2);

        Assert.AreEqual(expected0, date.Calculate("BS", WithCalendar("ar-SA", new HijriCalendar { HijriAdjustment = 0 })));
        Assert.AreEqual(expected2, date.Calculate("BS", WithCalendar("ar-SA", new HijriCalendar { HijriAdjustment = 2 })));
        Assert.AreEqual(expected0, date.Calculate("BS", WithCalendar("ar-SA", new HijriCalendar { HijriAdjustment = 0 })));
    }

    /// <summary>
    /// A cached formula is a snapshot of the culture when it was compiled: changing the culture's calendar afterwards
    /// neither alters the cached entry nor reaches another culture with the original settings.
    /// </summary>
    [TestMethod]
    public void CachedFormula_IsASnapshotOfTheCulture()
    {
        var date = new DateTime(2023, 3, 15);
        DateTime expected0 = StartOfMonth(new HijriCalendar { HijriAdjustment = 0 }, date);
        DateTime expected1 = StartOfMonth(new HijriCalendar { HijriAdjustment = 1 }, date);

        var hijri = new HijriCalendar { HijriAdjustment = 0 };
        var mutated = WithCalendar("ar-SA", hijri);
        Assert.AreEqual(expected0, date.Calculate("BS", mutated));

        hijri.HijriAdjustment = 1;
        Assert.AreEqual(expected1, date.Calculate("BS", mutated));
        Assert.AreEqual(expected0, date.Calculate("BS", WithCalendar("ar-SA", new HijriCalendar { HijriAdjustment = 0 })));
    }

    /// <summary>A compiled delegate keeps the calendar it was compiled with.</summary>
    [TestMethod]
    public void CompiledFormula_IsASnapshotOfTheCulture()
    {
        var date = new DateTime(2023, 3, 15);
        var hijri = new HijriCalendar { HijriAdjustment = 0 };
        var culture = WithCalendar("ar-SA", hijri);
        Func<DateTime, DateTime> startOfMonth = DateFormula.Compile("BS+1S", culture);
        DateTime before = startOfMonth(date);

        hijri.HijriAdjustment = 2;
        Assert.AreEqual(before, startOfMonth(date));
    }

    /// <summary>The calendar arithmetic of a step (+1S, one month) follows the selected calendar too.</summary>
    [TestMethod]
    public void ArabicFormula_AddMonth_FollowsTheSelectedCalendar()
    {
        var date = new DateTime(2023, 3, 15);
        // Start of Sha'ban 1444 (2023-02-21) plus one Umm al-Qura month: 1 Ramadan 1444.
        Assert.AreEqual(new DateTime(2023, 3, 23), date.Calculate("BS+1S", WithCalendar("ar-SA", new UmAlQuraCalendar())));
        Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("BS+1S", WithCalendar("ar-SA", new GregorianCalendar())));
    }

    /// <summary><see cref="DateUtils.StartOf(DateTime, PeriodTypeEnum, CultureInfo)"/> reads the culture's selected calendar.</summary>
    [TestMethod]
    public void StartAndEndOf_UseTheSelectedCalendar()
    {
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 2, 21), date.StartOf(PeriodTypeEnum.Month, WithCalendar("ar-SA", new UmAlQuraCalendar())));
        Assert.AreEqual(new DateTime(2023, 3, 1), date.StartOf(PeriodTypeEnum.Month, WithCalendar("ar-SA", new GregorianCalendar())));
        Assert.AreEqual(new DateTime(2023, 3, 22), date.EndOf(PeriodTypeEnum.Month, WithCalendar("ar-SA", new UmAlQuraCalendar())).Date);
        Assert.AreEqual(new DateTime(2023, 3, 31), date.EndOf(PeriodTypeEnum.Month, WithCalendar("ar-SA", new GregorianCalendar())).Date);
    }

    [TestMethod]
    public void ChineseFormula()
    {
        var date = new DateTime(2023, 3, 15);
        Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("EM+1D", new CultureInfo("zh-CN")));
    }

    [TestMethod]
    public void CompiledFormula()
    {
        var func = DateFormula.Compile("FM+1J", new CultureInfo("fr-FR"));
        var result = func(new DateTime(2023, 3, 15));
        Assert.AreEqual(new DateTime(2023, 4, 1), result);
    }

    [TestMethod]
    public void CalculateUsesCache()
    {
        var culture = new CultureInfo("fr-FR");
        var expected = DateFormula.Compile("FM+1J", culture)(new DateTime(2023, 3, 15));
        for (int i = 0; i < 3; i++)
        {
            var result = new DateTime(2023, 3, 15).Calculate("FM+1J", culture);
            Assert.AreEqual(expected, result);
        }
    }

    [TestMethod]
    public void FormulaWorkingDaysUsesCalendar()
    {
        // "FS+3O" from Friday 2024-04-05 (fr-FR, first-day-of-week = Monday):
        //   "FS"  = EndOf(Week)  = Sunday 2024-04-07  (Mon Apr 1 + 7 days - 1 tick, .Date = Apr 7)
        //   "+3O" = AddWorkingDays(Apr 7, 3) = Mon(1)=Apr 8, Tue(2)=Apr 9, Wed(3)=Apr 10
        // The test previously expected Apr 11, which matched the old (buggy) EndOf(Week) formula
        // that returned Monday Apr 8 instead of Sunday Apr 7 (#53). After the fix the correct
        // result is Apr 10.
        var culture = new CultureInfo("fr-FR");
        ICalendarProvider provider = new WeekEndCalendarProvider();
        var date = new DateTime(2024, 4, 5); // Friday
        var result = date.Calculate("FS+3O", culture, provider);
        Assert.AreEqual(new DateTime(2024, 4, 10), result);
    }

    [TestMethod]
    public void FormulaWorkingDaysRequiresCalendar()
    {
        var culture = new CultureInfo("fr-FR");
        var date = new DateTime(2024, 4, 5);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = date.Calculate("FS+1O", culture));
    }

    [TestMethod]
    public void NextAndPreviousWorkingDay()
    {
        ICalendarProvider provider = new WeekEndCalendarProvider();
        Assert.AreEqual(new DateTime(2024, 4, 5), new DateTime(2024, 4, 5).NextWorkingDay(provider));
        Assert.AreEqual(new DateTime(2024, 4, 5), new DateTime(2024, 4, 5).PreviousWorkingDay(provider));
        Assert.AreEqual(new DateTime(2024, 4, 8), new DateTime(2024, 4, 6).NextWorkingDay(provider));
        Assert.AreEqual(new DateTime(2024, 4, 5), new DateTime(2024, 4, 6).PreviousWorkingDay(provider));
    }

    [TestMethod]
    public void FormulaWorkingDayAdjust()
    {
        // "FS" from Saturday 2024-04-06 (fr-FR, first-day-of-week = Monday):
        //   EndOf(Week) = Sunday 2024-04-07
        // "+O" = NextWorkingDay(Apr 7) = Monday Apr 8  (still correct)
        // "-O" = PreviousWorkingDay(Apr 7) = Friday Apr 5  (was Apr 8 under old buggy EndOf #53)
        var culture = new CultureInfo("fr-FR");
        ICalendarProvider provider = new WeekEndCalendarProvider();
        var date = new DateTime(2024, 4, 6); // Saturday
        var result = date.Calculate("FS+O", culture, provider);
        Assert.AreEqual(new DateTime(2024, 4, 8), result);
        result = date.Calculate("FS-O", culture, provider);
        Assert.AreEqual(new DateTime(2024, 4, 5), result);
    }

    [TestMethod]
    public void FormulaWorkingDayAdjustRequiresCalendar()
    {
        var culture = new CultureInfo("fr-FR");
        var date = new DateTime(2024, 4, 6);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = date.Calculate("FS+O", culture));
    }

    [TestMethod]
    public void StartOfDayIsInvalid()
    {
        var culture = new CultureInfo("fr-FR");
        Assert.ThrowsExactly<ArgumentException>(() => _ = new DateTime(2024, 4, 6).Calculate("DJ", culture));
    }

    [TestMethod]
    public void StartOfWorkingDayIsInvalid()
    {
        var culture = new CultureInfo("fr-FR");
        ICalendarProvider provider = new WeekEndCalendarProvider();
        Assert.ThrowsExactly<ArgumentException>(() => _ = new DateTime(2024, 4, 6).Calculate("DO", culture, provider));
    }

    [TestMethod]
    public void EndOfDayIsInvalid()
    {
        var culture = new CultureInfo("fr-FR");
        Assert.ThrowsExactly<ArgumentException>(() => _ = new DateTime(2024, 4, 6).Calculate("FJ", culture));
    }

    [TestMethod]
    public void EndOfWorkingDayIsInvalid()
    {
        var culture = new CultureInfo("fr-FR");
        ICalendarProvider provider = new WeekEndCalendarProvider();
        Assert.ThrowsExactly<ArgumentException>(() => _ = new DateTime(2024, 4, 6).Calculate("FO", culture, provider));
    }
}
