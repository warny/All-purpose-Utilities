using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Dates;

namespace UtilsTest.Dates;

/// <summary>
/// Tests for <see cref="DateUtils.EndOf"/> with <see cref="PeriodTypeEnum.Week"/>.
/// Covers the bug fixed by #53: when the date fell on the first day of the configured
/// week, the previous formula returned end-of-current-day instead of end-of-week.
/// </summary>
[TestClass]
public class DateUtilsEndOfWeekTests
{
    // All tests use an explicit DayOfWeek overload so they are culture-independent.

    // ------------------------------------------------------------------ helpers

    private static DateTime EndOfWeek(DateTime dt, DayOfWeek firstDay)
        => dt.EndOf(PeriodTypeEnum.Week, firstDay);

    private static DateTime StartOfWeek(DateTime dt, DayOfWeek firstDay)
        => dt.StartOf(PeriodTypeEnum.Week, firstDay);

    // ------------------------------------------------------------------
    // Core invariant: EndOf(Week) must always equal StartOf(Week) + 7 days - 1 tick
    // ------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(2024, 1, 1,  (int)DayOfWeek.Monday)]   // Monday, firstDay=Monday (first day IS first day of week)
    [DataRow(2024, 1, 2,  (int)DayOfWeek.Monday)]   // Tuesday
    [DataRow(2024, 1, 3,  (int)DayOfWeek.Monday)]   // Wednesday
    [DataRow(2024, 1, 4,  (int)DayOfWeek.Monday)]   // Thursday
    [DataRow(2024, 1, 5,  (int)DayOfWeek.Monday)]   // Friday
    [DataRow(2024, 1, 6,  (int)DayOfWeek.Monday)]   // Saturday
    [DataRow(2024, 1, 7,  (int)DayOfWeek.Monday)]   // Sunday (last day of Monday-week)
    [DataRow(2024, 1, 7,  (int)DayOfWeek.Sunday)]   // Sunday, firstDay=Sunday (first day IS first day of week)
    [DataRow(2024, 1, 8,  (int)DayOfWeek.Sunday)]   // Monday
    [DataRow(2024, 1, 13, (int)DayOfWeek.Sunday)]   // Saturday (last day of Sunday-week)
    public void EndOfWeek_IsStartOfWeekPlusSixDaysEndOfDay(int y, int m, int d, int firstDayInt)
    {
        var firstDay = (DayOfWeek)firstDayInt;
        var dt = new DateTime(y, m, d);
        var start = StartOfWeek(dt, firstDay);
        var expected = start.AddDays(7).AddTicks(-1);
        var actual = EndOfWeek(dt, firstDay);
        Assert.AreEqual(expected, actual,
            $"EndOf(Week) for {dt:yyyy-MM-dd} (firstDay={firstDay}) should be {expected} but was {actual}.");
    }

    // ------------------------------------------------------------------
    // Specific known values (regression cases for #53)
    // ------------------------------------------------------------------

    [TestMethod]
    public void EndOfWeek_WhenDateIsFirstDayOfWeek_ReturnsEndOfSeventhDay()
    {
        // Monday 2024-01-01 is the first day of the week (firstDay=Monday).
        // Bug: returned 2024-01-01 23:59:59.9999999 instead of 2024-01-07 23:59:59.9999999.
        var monday = new DateTime(2024, 1, 1);
        var result = EndOfWeek(monday, DayOfWeek.Monday);
        var expected = new DateTime(2024, 1, 7, 23, 59, 59).AddTicks(9_999_999);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void EndOfWeek_WhenDateIsSundayFirstDayOfWeek_ReturnsEndOfSaturday()
    {
        // Sunday 2024-01-07 is the first day of the week (firstDay=Sunday).
        var sunday = new DateTime(2024, 1, 7);
        var result = EndOfWeek(sunday, DayOfWeek.Sunday);
        var expected = new DateTime(2024, 1, 13, 23, 59, 59).AddTicks(9_999_999);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void EndOfWeek_MidWeekDate_ReturnsCorrectEnd()
    {
        // Wednesday 2024-01-03 in a Monday-week → end of week = Sunday 2024-01-07
        var wednesday = new DateTime(2024, 1, 3);
        var result = EndOfWeek(wednesday, DayOfWeek.Monday);
        var expected = new DateTime(2024, 1, 7, 23, 59, 59).AddTicks(9_999_999);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void EndOfWeek_LastDayOfWeek_ReturnsEndOfThatDay()
    {
        // Sunday 2024-01-07 is the last day of a Monday-week → end = 2024-01-07 23:59:59...
        var sunday = new DateTime(2024, 1, 7);
        var result = EndOfWeek(sunday, DayOfWeek.Monday);
        var expected = new DateTime(2024, 1, 7, 23, 59, 59).AddTicks(9_999_999);
        Assert.AreEqual(expected, result);
    }

    // ------------------------------------------------------------------
    // EndOf(Week) and StartOf(Week) must be consistent (same containing week)
    // ------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(2024, 4, 15, (int)DayOfWeek.Monday)]
    [DataRow(2024, 4, 16, (int)DayOfWeek.Monday)]
    [DataRow(2024, 4, 21, (int)DayOfWeek.Monday)]
    [DataRow(2024, 4, 14, (int)DayOfWeek.Sunday)]
    [DataRow(2024, 4, 20, (int)DayOfWeek.Sunday)]
    public void StartAndEndOfWeek_BelongToSameWeek(int y, int m, int d, int firstDayInt)
    {
        var firstDay = (DayOfWeek)firstDayInt;
        var dt = new DateTime(y, m, d);
        var start = StartOfWeek(dt, firstDay);
        var end = EndOfWeek(dt, firstDay);
        // The end must be exactly 7 days after start minus one tick
        Assert.AreEqual(start.AddDays(7).AddTicks(-1), end);
        // The original date must lie within [start, end]
        Assert.IsTrue(dt >= start && dt <= end,
            $"{dt:yyyy-MM-dd} should be within [{start:yyyy-MM-dd}, {end:yyyy-MM-dd}].");
    }
}
