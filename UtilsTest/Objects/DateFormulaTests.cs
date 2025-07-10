using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                var result = date.Calculate("DO+3O", culture, provider);
                Assert.AreEqual(new DateTime(2024, 4, 10), result);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void FormulaWorkingDaysRequiresCalendar()
        {
                var culture = new CultureInfo("fr-FR");
                var date = new DateTime(2024, 4, 5);
                _ = date.Calculate("DO+1O", culture);
        }
}
