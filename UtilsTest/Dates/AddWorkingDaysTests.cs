using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Dates;

namespace UtilsTest.Dates;

/// <summary>
/// Tests for <see cref="DateUtils.AddWorkingDays"/>.
/// </summary>
[TestClass]
public class AddWorkingDaysTests
{
    private sealed class WeekEndCalendarProvider : ICalendarProvider
    {
        private static readonly DayOfWeek[] _weekEnds = [DayOfWeek.Saturday, DayOfWeek.Sunday];
        private static readonly HashSet<DateTime> _holidays = [];

        public int GetNonWorkingDaysCount(DateTime start, DateTime end)
                => GetHollydays(start, end).Count();

        public IEnumerable<DateTime> GetHollydays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (System.Array.IndexOf(_weekEnds, d.DayOfWeek) >= 0 || _holidays.Contains(d.Date))
                    yield return d;
        }

        public IEnumerable<DateTime> GetWorkingDays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (System.Array.IndexOf(_weekEnds, d.DayOfWeek) < 0 && !_holidays.Contains(d.Date))
                    yield return d;
        }

        public int GetWorkingDaysCount(DateTime start, DateTime end)
                => GetWorkingDays(start, end).Count();
    }

    /// <summary>
    /// A provider that classifies every day as non-working (hostile provider for #55 testing).
    /// </summary>
    private sealed class AllNonWorkingCalendarProvider : ICalendarProvider
    {
        public int GetNonWorkingDaysCount(DateTime start, DateTime end)
                => (int)(end.Date - start.Date).TotalDays + 1;

        public IEnumerable<DateTime> GetHollydays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                yield return d;
        }

        public IEnumerable<DateTime> GetWorkingDays(DateTime start, DateTime end)
                => [];

        public int GetWorkingDaysCount(DateTime start, DateTime end) => 0;
    }

    /// <summary>
    /// A provider that always returns a non-working count larger than the queried range.
    /// </summary>
    private sealed class LyingCalendarProvider : ICalendarProvider
    {
        public int GetNonWorkingDaysCount(DateTime start, DateTime end)
                => (int)(end.Date - start.Date).TotalDays + 100; // always lies

        public IEnumerable<DateTime> GetHollydays(DateTime start, DateTime end) => [];
        public IEnumerable<DateTime> GetWorkingDays(DateTime start, DateTime end) => [];
        public int GetWorkingDaysCount(DateTime start, DateTime end) => 0;
    }

    [TestMethod]
    public void AddWorkingDaysSkipsWeekEnds()
    {
        ICalendarProvider provider = new WeekEndCalendarProvider();
        var start = new DateTime(2024, 4, 5); // Friday
        var result = start.AddWorkingDays(3, provider);
        Assert.AreEqual(new DateTime(2024, 4, 10), result);
    }

    [TestMethod]
    public void AddWorkingDaysHandlesLongerRanges()
    {
        ICalendarProvider provider = new WeekEndCalendarProvider();
        var start = new DateTime(2024, 4, 5); // Friday
        var result = start.AddWorkingDays(7, provider);
        Assert.AreEqual(new DateTime(2024, 4, 16), result);
    }

    [DataTestMethod]
    [DataRow(2025, 6, 23, 2, 2025, 6, 25)]
    [DataRow(2025, 6, 20, 2, 2025, 6, 24)]
    [DataRow(2025, 6, 21, 2, 2025, 6, 24)]
    [DataRow(2025, 6, 22, 2, 2025, 6, 24)]
    [DataRow(2025, 6, 19, 2, 2025, 6, 23)]
    [DataRow(2025, 6, 9, 5, 2025, 6, 16)]
    [DataRow(2025, 6, 6, 6, 2025, 6, 16)]
    [DataRow(2025, 6, 9, 10, 2025, 6, 23)]
    [DataRow(2025, 6, 8, 10, 2025, 6, 20)]
    [DataRow(2025, 6, 7, 10, 2025, 6, 20)]
    public void AddWorkingDaysReferenceCases(
            int y, int m, int d,
            int working,
            int ey, int em, int ed)
    {
        ICalendarProvider provider = new WeekEndCalendarProvider();
        var start = new DateTime(y, m, d);
        var expected = new DateTime(ey, em, ed);
        var result = start.AddWorkingDays(working, provider);
        Assert.AreEqual(expected, result);
    }

    // ------------------------------------------------------------------ #55 termination safety

    [TestMethod]
    public void AddWorkingDays_WithAllNonWorkingProvider_ThrowsInvalidOperation()
    {
        // A provider that calls every day non-working must be detected and rejected before
        // the loop runs forever (#55).
        var start = new DateTime(2024, 1, 1);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => start.AddWorkingDays(1, new AllNonWorkingCalendarProvider()));
    }

    [TestMethod]
    public void AddWorkingDays_WithLyingProvider_ThrowsInvalidOperation()
    {
        // A provider that reports more non-working days than calendar days in the range must
        // be detected immediately (#55).
        var start = new DateTime(2024, 1, 1);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => start.AddWorkingDays(1, new LyingCalendarProvider()));
    }

    [TestMethod]
    public void PreviousWorkingDay_WithAllNonWorkingProvider_ThrowsInvalidOperation()
    {
        // PreviousWorkingDay must not run indefinitely when no prior working day exists (#55).
        var date = new DateTime(2024, 1, 1);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => date.PreviousWorkingDay(new AllNonWorkingCalendarProvider()));
    }

    [TestMethod]
    public void AddWorkingDays_ZeroWorkingDays_ReturnsSameDate()
    {
        // Zero working days added must return the base date immediately.
        var start = new DateTime(2024, 1, 1);
        var result = start.AddWorkingDays(0, new WeekEndCalendarProvider());
        Assert.AreEqual(start, result);
    }
}
