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
        /// Verifies that adjusting to the same day of week moves forward by one week when <paramref name="after"/> is true.
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

	[TestMethod]
	public void ArabicFormulaHijri()
	{
		var culture = new CultureInfo("ar-SA");
		var date = new DateTime(2023, 3, 15);
		Assert.AreEqual(new DateTime(2023, 3, 23), date.Calculate("NS+1Y", culture));
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
                var culture = new CultureInfo("fr-FR");
                ICalendarProvider provider = new WeekEndCalendarProvider();
                var date = new DateTime(2024, 4, 5); // Friday
                var result = date.Calculate("FS+3O", culture, provider);
                Assert.AreEqual(new DateTime(2024, 4, 11), result);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void FormulaWorkingDaysRequiresCalendar()
        {
                var culture = new CultureInfo("fr-FR");
                var date = new DateTime(2024, 4, 5);
                _ = date.Calculate("FS+1O", culture);
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
                var culture = new CultureInfo("fr-FR");
                ICalendarProvider provider = new WeekEndCalendarProvider();
                var date = new DateTime(2024, 4, 6); // Saturday
                var result = date.Calculate("FS+O", culture, provider);
                Assert.AreEqual(new DateTime(2024, 4, 8), result);
                result = date.Calculate("FS-O", culture, provider);
                Assert.AreEqual(new DateTime(2024, 4, 8), result);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void FormulaWorkingDayAdjustRequiresCalendar()
        {
                var culture = new CultureInfo("fr-FR");
                var date = new DateTime(2024, 4, 6);
                _ = date.Calculate("FS+O", culture);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void StartOfDayIsInvalid()
        {
                var culture = new CultureInfo("fr-FR");
                _ = new DateTime(2024, 4, 6).Calculate("DJ", culture);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void StartOfWorkingDayIsInvalid()
        {
                var culture = new CultureInfo("fr-FR");
                ICalendarProvider provider = new WeekEndCalendarProvider();
                _ = new DateTime(2024, 4, 6).Calculate("DO", culture, provider);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EndOfDayIsInvalid()
        {
                var culture = new CultureInfo("fr-FR");
                _ = new DateTime(2024, 4, 6).Calculate("FJ", culture);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EndOfWorkingDayIsInvalid()
        {
                var culture = new CultureInfo("fr-FR");
                ICalendarProvider provider = new WeekEndCalendarProvider();
                _ = new DateTime(2024, 4, 6).Calculate("FO", culture, provider);
        }
}
