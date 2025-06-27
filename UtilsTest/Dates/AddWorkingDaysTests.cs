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
        [DataRow(2025, 6, 8, 10, 2025, 6, 23)]
        [DataRow(2025, 6, 7, 10, 2025, 6, 23)]
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
}
