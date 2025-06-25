using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;
using Utils.Utils.Dates;

namespace UtilsTest.Objects;

[TestClass]
public class DateUtilitiesTests
{
	[TestMethod]
	public void ComputeEasterTest()
	{
		var knownEastern = new DateTime[] {
			new (2006, 4, 16),
			new (2018, 4, 1),
			new (2019, 4, 21),
			new (2020, 4, 12),
			new (2021, 4, 4),
			new (2022, 4, 17),
			new (2023, 4, 9),
			new (2024, 3, 31)
		};

		foreach (var knownEaster in knownEastern)
		{
			var easter = DateUtils.ComputeEaster(knownEaster.Year);
			Assert.AreEqual(knownEaster, easter);
		}
	}

	[TestMethod]
	public void GetWeekDateRangeTest()
	{
		var tests = new ((int Year, int Week, DayOfWeek startOfWeek, DayOfWeek pivot) Parameters, Range<DateTime> Result)[] {
			((2024, 1, DayOfWeek.Monday, DayOfWeek.Thursday), new (new (2024, 1, 1), new (2024, 1, 7))),
			((2024, 1, DayOfWeek.Sunday, DayOfWeek.Thursday), new (new (2023, 12, 31), new (2024, 1, 6))),
			((2024, 1, DayOfWeek.Sunday, DayOfWeek.Sunday), new (new (2024, 1, 7), new (2024, 1, 13))),
			((2024, 16, DayOfWeek.Monday, DayOfWeek.Thursday), new (new (2024, 4, 15), new (2024, 4, 21))),
			((2024, 16, DayOfWeek.Sunday, DayOfWeek.Thursday), new (new (2024, 4, 14), new (2024, 4, 20))),
			((2024, 16, DayOfWeek.Sunday, DayOfWeek.Sunday), new (new (2024, 4, 21), new (2024, 4, 27)))
		};

		foreach (var (parameters, expected) in tests) {
			var result = WeekUtils.GetWeekDateRange(parameters.Year, parameters.Week, parameters.startOfWeek, parameters.pivot);
			Assert.AreEqual(expected, result);
		}
	}
}
