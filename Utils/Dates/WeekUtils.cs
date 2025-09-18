using System;
using System.Globalization;
using Utils.Objects;
using Utils.Range;

namespace Utils.Dates;

/// <summary>
/// Provides helper methods to calculate week numbers and their associated date ranges for a given year.
/// </summary>
public static class WeekUtils
{
	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="Week">Week.</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetISOWeekDateRange(Week week)
		=> GetWeekDateRange(week.Year, week.WeekNumber, DayOfWeek.Monday, DayOfWeek.Thursday);


	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="year">The year in which the week occurs.</param>
	/// <param name="weekNumber">The week number (1-based).</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetISOWeekDateRange(int year, int weekNumber)
		=> GetWeekDateRange(year, weekNumber, DayOfWeek.Monday, DayOfWeek.Thursday);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="week">Week.</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(Week week, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> GetWeekDateRange(week.Year, week.WeekNumber, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="year">The year in which the week occurs.</param>
	/// <param name="weekNumber">The week number (1-based).</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(int year, int weekNumber, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> GetWeekDateRange(year, weekNumber, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="Week">Week.</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <param name="cultureInfo">Culture that defines the start of week day</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(Week week, CultureInfo cultureInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> GetWeekDateRange(week.Year, week.WeekNumber, cultureInfo.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="year">The year in which the week occurs.</param>
	/// <param name="weekNumber">The week number (1-based).</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <param name="cultureInfo">Culture that defines the start of week day</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(int year, int weekNumber, CultureInfo cultureInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> GetWeekDateRange(year, weekNumber, cultureInfo.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="Week">Week.</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <param name="dateTimeFormatInfo">DateTimeInfo that defines the start of week day</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(Week week, DateTimeFormatInfo dateTimeFormatInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> GetWeekDateRange(week.Year, week.WeekNumber, dateTimeFormatInfo.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="year">The year in which the week occurs.</param>
	/// <param name="weekNumber">The week number (1-based).</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <param name="dateTimeFormatInfo">DateTimeInfo that defines the start of week day</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(int year, int weekNumber, DateTimeFormatInfo dateTimeFormatInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> GetWeekDateRange(year, weekNumber, dateTimeFormatInfo.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="week">Week.</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <param name="firstDayOfWeek">The first day of the week (e.g., Monday or Sunday). Defaults to the current culture's first day of the week.</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(Week week, DayOfWeek firstDayOfWeek, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> GetWeekDateRange(week.Year, week.WeekNumber, firstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="year">The year in which the week occurs.</param>
	/// <param name="weekNumber">The week number (1-based).</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <param name="firstDayOfWeek">The first day of the week (e.g., Monday or Sunday). Defaults to the current culture's first day of the week.</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetWeekDateRange(int year, int weekNumber, DayOfWeek firstDayOfWeek, DayOfWeek pivotDay = DayOfWeek.Thursday)
	{
		weekNumber.ArgMustBeBetween(1, 53);

		// Get the first day of the year.
		var firstDayOfYear = new DateTime(year, 1, 1);

		// Calculate the offset to the pivot day
		var pivotOffset = (int)pivotDay - (int)firstDayOfYear.DayOfWeek;

		// Adjust first day of the year to the nearest pivot day (if necessary)
		DateTime firstPivotDate = firstDayOfYear.AddDays(pivotOffset > 0 ? pivotOffset : pivotOffset + 7);

		// Adjust to the first week based on the pivot day
		DateTime firstWeekStartDate = firstPivotDate.AddDays(-(firstPivotDate.DayOfWeek - firstDayOfWeek));

		// Calculate the start date of the specified week.
		DateTime startDate = firstWeekStartDate.AddDays((weekNumber - 1) * 7);

		// Calculate the end date of the specified week.
		DateTime endDate = startDate.AddDays(6);

		// If the calculated end date falls into the next year, adjust it.
		if (endDate.Year > year)
			endDate = new DateTime(year, 12, 31);

		return new(startDate, endDate);
	}

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <returns>The week number for the specified date.</returns>
	public static Week GetISOWeekOfYear(this DateTime date)
		=> date.GetWeekOfYear(DayOfWeek.Monday, DayOfWeek.Thursday);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static Week GetWeekOfYear(this DateTime date, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> date.GetWeekOfYear(CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="cultureInfo">Culture that defines the start of week day</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static Week GetWeekOfYear(this DateTime date, CultureInfo cultureInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> date.GetWeekOfYear(cultureInfo.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="dateTimeFormatInfo">DateTimeInfo that defines the start of week day</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static Week GetWeekOfYear(this DateTime date, DateTimeFormatInfo dateTimeFormatInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> date.GetWeekOfYear(dateTimeFormatInfo.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="firstDayOfWeek">The first day of the week (e.g., Monday or Sunday). Defaults to the current culture's first day of the week.</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static Week GetWeekOfYear(this DateTime date, DayOfWeek firstDayOfWeek, DayOfWeek pivotDay = DayOfWeek.Thursday)
	{
		// Get the first day of the year
		var firstDayOfYear = new DateTime(date.Year, 1, 1);

		// Calculate the offset from the first day of the year to the pivot day
		var pivotOffset = (int)pivotDay - (int)firstDayOfYear.DayOfWeek;
		DateTime firstPivotDate = firstDayOfYear.AddDays(pivotOffset >= 0 ? pivotOffset : pivotOffset + 7);

		// Adjust the first pivot date to the correct week start
		DateTime firstWeekStartDate = firstPivotDate.AddDays(-(firstPivotDate.DayOfWeek - firstDayOfWeek));

		// Calculate the difference in days between the input date and the start of the first week
		var daysDifference = (date - firstWeekStartDate).Days;

		// Calculate the week number (1-based index)
		var weekNumber = daysDifference / 7 + 1;

		// If the date falls before the first pivot date (e.g., in the previous year)
		if (date < firstWeekStartDate)
			// Handle dates in the previous year
			return date.AddDays(-daysDifference).GetWeekOfYear(firstDayOfWeek, pivotDay);

		return new(date.Year, weekNumber);
	}

}
