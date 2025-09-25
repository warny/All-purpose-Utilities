using System;
using System.Collections.Generic;
using System.Globalization;
using Utils.Objects;

namespace Utils.Dates;

/// <summary>
/// Provides utility methods for date and time calculations and manipulations.
/// </summary>
public static class DateUtils
{
	/// <summary>
	/// Gets the start date of the specified period that contains the given date.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the start date for.</param>
	/// <returns>The start date of the specified period.</returns>
	public static DateTime StartOf(this DateTime dateTime, PeriodTypeEnum period)
			=> dateTime.StartOf(period, CultureInfo.CurrentCulture);

	/// <summary>
	/// Gets the start date of the specified period that contains the given date using the provided culture info.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the start date for.</param>
	/// <param name="cultureInfo">The culture info to determine the first day of the week.</param>
	/// <returns>The start date of the specified period.</returns>
	public static DateTime StartOf(this DateTime dateTime, PeriodTypeEnum period, CultureInfo cultureInfo)
			=> StartOfInternal(dateTime, period, cultureInfo.DateTimeFormat.FirstDayOfWeek, cultureInfo.Calendar);

	/// <summary>
	/// Gets the start date of the specified period that contains the given date using the provided DateTimeFormatInfo.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the start date for.</param>
	/// <param name="dateTimeFormatInfo">The DateTimeFormatInfo to determine the first day of the week.</param>
	/// <returns>The start date of the specified period.</returns>
	public static DateTime StartOf(this DateTime dateTime, PeriodTypeEnum period, DateTimeFormatInfo dateTimeFormatInfo)
			=> StartOfInternal(dateTime, period, dateTimeFormatInfo.FirstDayOfWeek, dateTimeFormatInfo.Calendar);

	/// <summary>
	/// Gets the start date of the specified period that contains the given date using the provided first day of the week.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the start date for.</param>
	/// <param name="firstDayOfWeek">The first day of the week.</param>
	/// <returns>The start date of the specified period.</returns>
	public static DateTime StartOf(this DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek)
			=> StartOfInternal(dateTime, period, firstDayOfWeek, CultureInfo.CurrentCulture.Calendar);

	/// <summary>
	/// Calculates the start date of a period using the provided calendar.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type.</param>
	/// <param name="firstDayOfWeek">First day of the week.</param>
	/// <param name="calendar">Calendar used for computations.</param>
	/// <returns>The start date of the period.</returns>
	private static DateTime StartOfInternal(DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek, Calendar calendar)
	{
		switch (period)
		{
			case PeriodTypeEnum.None:
				return dateTime;
			case PeriodTypeEnum.Day:
			case PeriodTypeEnum.WorkingDay:
				return calendar.AddDays(dateTime, 0).Date;
			case PeriodTypeEnum.Week:
				var difference = (7 + ((int)calendar.GetDayOfWeek(dateTime) - (int)firstDayOfWeek)) % 7;
				DateTime weekDate = calendar.AddDays(dateTime.Date, -difference);
				return calendar.AddDays(weekDate, 0).Date;
			case PeriodTypeEnum.Month:
				return calendar.ToDateTime(calendar.GetYear(dateTime), calendar.GetMonth(dateTime), 1, 0, 0, 0, 0);
			case PeriodTypeEnum.Quarter:
				var month = calendar.GetMonth(dateTime);
				var quarter = (month - 1) / 3;
				var startMonth = quarter * 3 + 1;
				return calendar.ToDateTime(calendar.GetYear(dateTime), startMonth, 1, 0, 0, 0, 0);
			case PeriodTypeEnum.Year:
				return calendar.ToDateTime(calendar.GetYear(dateTime), 1, 1, 0, 0, 0, 0);
			default:
				throw new ArgumentOutOfRangeException(nameof(period), period, null);
		}
	}

	/// <summary>
	/// Gets the end date of the specified period that contains the given date.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the end date for.</param>
	/// <returns>The end date of the specified period.</returns>
	public static DateTime EndOf(this DateTime dateTime, PeriodTypeEnum period)
			=> dateTime.EndOf(period, CultureInfo.CurrentCulture);

	/// <summary>
	/// Gets the end date of the specified period that contains the given date using the provided culture info.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the end date for.</param>
	/// <param name="cultureInfo">The culture info to determine the first day of the week.</param>
	/// <returns>The end date of the specified period.</returns>
	public static DateTime EndOf(this DateTime dateTime, PeriodTypeEnum period, CultureInfo cultureInfo)
			=> EndOfInternal(dateTime, period, cultureInfo.DateTimeFormat.FirstDayOfWeek, cultureInfo.Calendar);

	/// <summary>
	/// Gets the end date of the specified period that contains the given date using the provided DateTimeFormatInfo.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the end date for.</param>
	/// <param name="dateTimeFormatInfo">The DateTimeFormatInfo to determine the first day of the week.</param>
	/// <returns>The end date of the specified period.</returns>
	public static DateTime EndOf(this DateTime dateTime, PeriodTypeEnum period, DateTimeFormatInfo dateTimeFormatInfo)
			=> EndOfInternal(dateTime, period, dateTimeFormatInfo.FirstDayOfWeek, dateTimeFormatInfo.Calendar);

	/// <summary>
	/// Gets the end date of the specified period that contains the given date using the provided first day of the week.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the end date for.</param>
	/// <param name="firstDayOfWeek">The first day of the week.</param>
	/// <returns>The end date of the specified period.</returns>
	public static DateTime EndOf(this DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek)
			=> EndOfInternal(dateTime, period, firstDayOfWeek, CultureInfo.CurrentCulture.Calendar);

	/// <summary>
	/// Calculates the end date of a period using the provided calendar.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type.</param>
	/// <param name="firstDayOfWeek">First day of the week.</param>
	/// <param name="calendar">Calendar used for computations.</param>
	/// <returns>The end date of the period.</returns>
	private static DateTime EndOfInternal(DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek, Calendar calendar)
	{
		switch (period)
		{
			case PeriodTypeEnum.None:
				return dateTime;
			case PeriodTypeEnum.Day:
			case PeriodTypeEnum.WorkingDay:
				return calendar.AddDays(dateTime.Date, 1).AddTicks(-1);
			case PeriodTypeEnum.Week:
				var difference = (7 - ((int)calendar.GetDayOfWeek(dateTime) - (int)firstDayOfWeek)) % 7;
				DateTime endWeek = calendar.AddDays(dateTime.Date, difference);
				return calendar.AddDays(endWeek, 1).AddTicks(-1);
			case PeriodTypeEnum.Month:
				var startOfMonth = calendar.ToDateTime(calendar.GetYear(dateTime), calendar.GetMonth(dateTime), 1, 0, 0, 0, 0);
				DateTime startOfNextMonth = calendar.AddMonths(startOfMonth, 1);
				return startOfNextMonth.AddTicks(-1);
			case PeriodTypeEnum.Quarter:
				var month = calendar.GetMonth(dateTime);
				var quarter = (month - 1) / 3 + 1;
				var endMonth = quarter * 3;
				var startOfQuarter = calendar.ToDateTime(calendar.GetYear(dateTime), endMonth, 1, 0, 0, 0, 0);
				DateTime startOfNextQuarter = calendar.AddMonths(startOfQuarter, 1);
				return startOfNextQuarter.AddTicks(-1);
			case PeriodTypeEnum.Year:
				var startOfYear = calendar.ToDateTime(calendar.GetYear(dateTime), 1, 1, 0, 0, 0, 0);
				DateTime startOfNextYear = calendar.AddYears(startOfYear, 1);
				return startOfNextYear.AddTicks(-1);
			default:
				throw new ArgumentOutOfRangeException(nameof(period), period, null);
		}
	}

	/// <summary>
	/// Converts the specified DateTime to a Unix timestamp.
	/// </summary>
	/// <param name="dateTime">The DateTime to convert.</param>
	/// <returns>The Unix timestamp representing the specified DateTime.</returns>
	public static long ToUnixTimeStamp(this DateTime dateTime)
		=> (long)(dateTime.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;

	/// <summary>
	/// Converts a Unix timestamp to a DateTime.
	/// </summary>
	/// <param name="timestamp">The Unix timestamp to convert.</param>
	/// <returns>A DateTime representing the specified Unix timestamp.</returns>
	public static DateTime FromUnixTimeStamp(long timestamp)
			=> DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();

	/// <summary>
	/// Adds a number of working days to the specified <paramref name="date"/>.
	/// </summary>
	/// <param name="date">Base date.</param>
	/// <param name="workingDays">Number of working days to add.</param>
	/// <param name="calendarProvider">Calendar providing working day information.</param>
	/// <returns>The resulting date including additional non working days.</returns>
	public static DateTime AddWorkingDays(this DateTime date, int workingDays, ICalendarProvider calendarProvider)
	{
		workingDays.ArgMustBeGreaterOrEqualsThan(0);
		calendarProvider.Arg().MustNotBeNull();

		var toAdd = workingDays;
		var current = date;

		while (toAdd > 0)
		{
			var end = current.AddDays(toAdd);
			toAdd = calendarProvider.GetNonWorkingDaysCount(current.AddDays(1), end);
			current = end;
		}

		return current;
	}

	/// <summary>
	/// Gets the next working day starting at the provided <paramref name="date"/>.
	/// </summary>
	/// <param name="date">Base date.</param>
	/// <param name="calendarProvider">Calendar providing working day information.</param>
	/// <returns>The first working day on or after <paramref name="date"/>.</returns>
	public static DateTime NextWorkingDay(this DateTime date, ICalendarProvider calendarProvider)
	{
		calendarProvider.Arg().MustNotBeNull();

		if (calendarProvider.GetNonWorkingDaysCount(date, date) == 0)
			return date;

		return date.AddWorkingDays(1, calendarProvider);
	}

	/// <summary>
	/// Gets the previous working day ending at the provided <paramref name="date"/>.
	/// </summary>
	/// <param name="date">Base date.</param>
	/// <param name="calendarProvider">Calendar providing working day information.</param>
	/// <returns>The first working day on or before <paramref name="date"/>.</returns>
	public static DateTime PreviousWorkingDay(this DateTime date, ICalendarProvider calendarProvider)
	{
		calendarProvider.Arg().MustNotBeNull();

		if (calendarProvider.GetNonWorkingDaysCount(date, date) == 0)
			return date;

		var current = date.AddDays(-1);
		while (calendarProvider.GetNonWorkingDaysCount(current, current) > 0)
			current = current.AddDays(-1);

		return current;
	}

	/// <summary>
	/// Calculates the date of Easter Sunday for the specified year using the Anonymous Gregorian algorithm.
	/// </summary>
	/// <param name="year">The year to calculate Easter for.</param>
	/// <returns>The date of Easter Sunday for the specified year.</returns>
	public static DateTime ComputeEaster(int year)
	{
		year.ArgMustBeGreaterThan(0);

		//calcul du cycle de méton
		var metonCycle = year % 19;

		//calcul du siècle et du rang de l'année dans le siècle
		var century = year / 100;
		var yearRank = year % 100;
		//calcul siècle bissextile
		var century_s = century / 4;
		var century_t = century % 4;
		//calcul année bissextile
		var leapYear_b = yearRank / 4;
		var leapYear_d = yearRank % 4;

		//calcul du cycle de proemptose
		var proemptoseCycle = (century + 8) / 25;
		var proemptose = (century - proemptoseCycle + 1) / 3;

		//calcul épacte
		var epacte = (19 * metonCycle + century - century_s - proemptose + 15) % 30;


		//calcul lettre dominicale
		var sundayLetter = (2 * century_t + 2 * leapYear_b - epacte - leapYear_d + 32) % 7;

		//correction
		var correction = (metonCycle + 11 * epacte + 22 * sundayLetter) / 451;

		var easterDate = epacte + sundayLetter - 7 * correction + 114;

		//calcul de la date de pâque
		return new DateTime(year, easterDate / 31, 1).AddDays(easterDate % 31);
	}

	/// <summary>
	/// Calculates the dates of Easter Sunday for a range of years.
	/// </summary>
	/// <param name="startYear">The start year of the range.</param>
	/// <param name="endYear">The end year of the range.</param>
	/// <returns>An enumerable of DateTime objects representing Easter Sunday for each year in the range.</returns>
	public static IEnumerable<DateTime> ComputeEasterRange(int startYear, int endYear)
	{
		if (startYear > endYear)
			throw new ArgumentException("Start year must be less than or equal to end year.");

		for (var year = startYear; year <= endYear; year++)
		{
			yield return ComputeEaster(year);
		}
	}
}



/// <summary>
/// Defines different types of time periods.
/// </summary>
public enum PeriodTypeEnum
{
	/// <summary>
	/// No specific period.
	/// </summary>
	None = 0,

	/// <summary>
	/// Represents a single day.
	/// </summary>
	Day,

	/// <summary>
	/// Represents a working day.
	/// </summary>
	WorkingDay,

	/// <summary>
	/// Represents a week.
	/// </summary>
	Week,

	/// <summary>
	/// Represents a month.
	/// </summary>
	Month,

	/// <summary>
	/// Represents a quarter (three months).
	/// </summary>
	Quarter,

	/// <summary>
	/// Represents a year.
	/// </summary>
	Year
}
