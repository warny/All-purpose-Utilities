using System;
using System.Collections.Generic;
using System.Globalization;

namespace Utils.Objects;

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
		=> StartOf(dateTime, period, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

	/// <summary>
	/// Gets the start date of the specified period that contains the given date using the provided culture info.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the start date for.</param>
	/// <param name="cultureInfo">The culture info to determine the first day of the week.</param>
	/// <returns>The start date of the specified period.</returns>
	public static DateTime StartOf(this DateTime dateTime, PeriodTypeEnum period, CultureInfo cultureInfo)
		=> StartOf(dateTime, period, cultureInfo.DateTimeFormat.FirstDayOfWeek);

	/// <summary>
	/// Gets the start date of the specified period that contains the given date using the provided DateTimeFormatInfo.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the start date for.</param>
	/// <param name="dateTimeFormatInfo">The DateTimeFormatInfo to determine the first day of the week.</param>
	/// <returns>The start date of the specified period.</returns>
	public static DateTime StartOf(this DateTime dateTime, PeriodTypeEnum period, DateTimeFormatInfo dateTimeFormatInfo)
		=> StartOf(dateTime, period, dateTimeFormatInfo.FirstDayOfWeek);

	/// <summary>
	/// Gets the start date of the specified period that contains the given date using the provided first day of the week.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the start date for.</param>
	/// <param name="firstDayOfWeek">The first day of the week.</param>
	/// <returns>The start date of the specified period.</returns>
	public static DateTime StartOf(this DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek)
	{
		switch (period)
		{
			case PeriodTypeEnum.None:
				return dateTime;
			case PeriodTypeEnum.Day:
				return dateTime.Date;
			case PeriodTypeEnum.Week:
				int difference = (7 + (dateTime.DayOfWeek - firstDayOfWeek)) % 7;
				return dateTime.Date.AddDays(-difference);
			case PeriodTypeEnum.Month:
				return new DateTime(dateTime.Year, dateTime.Month, 1);
			case PeriodTypeEnum.Quarter:
				int quarterNumber = (dateTime.Month - 1) / 3 + 1;
				int startMonth = (quarterNumber - 1) * 3 + 1;
				return new DateTime(dateTime.Year, startMonth, 1);
			case PeriodTypeEnum.Year:
				return new DateTime(dateTime.Year, 1, 1);
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
		=> EndOf(dateTime, period, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

	/// <summary>
	/// Gets the end date of the specified period that contains the given date using the provided culture info.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the end date for.</param>
	/// <param name="cultureInfo">The culture info to determine the first day of the week.</param>
	/// <returns>The end date of the specified period.</returns>
	public static DateTime EndOf(this DateTime dateTime, PeriodTypeEnum period, CultureInfo cultureInfo)
		=> EndOf(dateTime, period, cultureInfo.DateTimeFormat.FirstDayOfWeek);

	/// <summary>
	/// Gets the end date of the specified period that contains the given date using the provided DateTimeFormatInfo.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the end date for.</param>
	/// <param name="dateTimeFormatInfo">The DateTimeFormatInfo to determine the first day of the week.</param>
	/// <returns>The end date of the specified period.</returns>
	public static DateTime EndOf(this DateTime dateTime, PeriodTypeEnum period, DateTimeFormatInfo dateTimeFormatInfo)
		=> EndOf(dateTime, period, dateTimeFormatInfo.FirstDayOfWeek);

	/// <summary>
	/// Gets the end date of the specified period that contains the given date using the provided first day of the week.
	/// </summary>
	/// <param name="dateTime">The reference date.</param>
	/// <param name="period">The period type to calculate the end date for.</param>
	/// <param name="firstDayOfWeek">The first day of the week.</param>
	/// <returns>The end date of the specified period.</returns>
	public static DateTime EndOf(this DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek)
	{
		switch (period)
		{
			case PeriodTypeEnum.None:
				return dateTime;
			case PeriodTypeEnum.Day:
				return dateTime.Date.AddDays(1).AddTicks(-1);
			case PeriodTypeEnum.Week:
				int difference = (7 - (dateTime.DayOfWeek - firstDayOfWeek)) % 7;
				return dateTime.Date.AddDays(difference).AddDays(1).AddTicks(-1);
			case PeriodTypeEnum.Month:
				DateTime startOfMonth = new DateTime(dateTime.Year, dateTime.Month, 1);
				DateTime startOfNextMonth = startOfMonth.AddMonths(1);
				return startOfNextMonth.AddTicks(-1);
			case PeriodTypeEnum.Quarter:
				int quarterNumber = (dateTime.Month - 1) / 3 + 1;
				int endMonth = quarterNumber * 3;
				DateTime startOfQuarter = new DateTime(dateTime.Year, endMonth, 1);
				DateTime startOfNextQuarter = startOfQuarter.AddMonths(1);
				return startOfNextQuarter.AddTicks(-1);
			case PeriodTypeEnum.Year:
				DateTime startOfYear = new DateTime(dateTime.Year, 1, 1);
				DateTime startOfNextYear = startOfYear.AddYears(1);
				return startOfNextYear.AddTicks(-1);
			default:
				throw new ArgumentOutOfRangeException(nameof(period), period, null);
		}
	}

	/// <summary>
	/// Represents the Unix Epoch date and time (January 1, 1970, UTC).
	/// </summary>
	public static DateTime UnixEpoch { get; } = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	/// <summary>
	/// Converts the specified DateTime to a Unix timestamp.
	/// </summary>
	/// <param name="dateTime">The DateTime to convert.</param>
	/// <returns>The Unix timestamp representing the specified DateTime.</returns>
	public static long ToUnixTimeStamp(this DateTime dateTime)
		=> (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalSeconds;

	/// <summary>
	/// Converts a Unix timestamp to a DateTime.
	/// </summary>
	/// <param name="timestamp">The Unix timestamp to convert.</param>
	/// <returns>A DateTime representing the specified Unix timestamp.</returns>
	public static DateTime FromUnixTimeStamp(long timestamp)
		=> UnixEpoch.AddSeconds(timestamp).ToLocalTime();

	/// <summary>
	/// Calculates the date of Easter Sunday for the specified year using the Anonymous Gregorian algorithm.
	/// </summary>
	/// <param name="year">The year to calculate Easter for.</param>
	/// <returns>The date of Easter Sunday for the specified year.</returns>
	public static DateTime ComputeEaster(int year)
	{
		year.ArgMustBeGreaterThan(0);

		//calcul du cycle de méton
		int metonCycle = year % 19;

		//calcul du siècle et du rang de l'année dans le siècle
		int century = year / 100;
		int yearRank = year % 100;
		//calcul siècle bissextile
		int century_s = century / 4;
		int century_t = century % 4;
		//calcul année bissextile
		int leapYear_b = yearRank / 4;
		int leapYear_d = yearRank % 4;

		//calcul du cycle de proemptose
		int proemptoseCycle = (century + 8) / 25;
		int proemptose = (century - proemptoseCycle + 1) / 3;

		//calcul épacte
		int epacte = (19 * metonCycle + century - century_s - proemptose + 15) % 30;


		//calcul lettre dominicale
		int sundayLetter = (2 * century_t + 2 * leapYear_b - epacte - leapYear_d + 32) % 7;

		//correction
		int correction = (metonCycle + 11 * epacte + 22 * sundayLetter) / 451;

		int easterDate = (epacte + sundayLetter - 7 * correction + 114);

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

		for (int year = startYear; year <= endYear; year++)
		{
			yield return ComputeEaster(year);
		}
	}

	/// <summary>
	/// Computes the date range (start and end date) for the specified week of the specified year, using a specified pivot day.
	/// </summary>
	/// <param name="year">The year in which the week occurs.</param>
	/// <param name="weekNumber">The week number (1-based).</param>
	/// <param name="pivotDay">The pivot day that defines the week. Defaults to Thursday for ISO weeks.</param>
	/// <returns>The date range of the specified week.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the week number is out of range.</exception>
	public static Range<DateTime> GetISOWeekDateRange(int year, int weekNumber)
		=> GetWeekDateRange(year, weekNumber, DayOfWeek.Monday, DayOfWeek.Thursday);


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
		DateTime firstDayOfYear = new DateTime(year, 1, 1);

		// Calculate the offset to the pivot day
		int pivotOffset = (int)pivotDay - (int)firstDayOfYear.DayOfWeek;

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
		{
			endDate = new DateTime(year, 12, 31);
		}

		return new (startDate, endDate);
	}

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static (int Year, int WeekOfYear) GetISOWeekOfYear(this DateTime date)
		=> date.GetWeekOfYear(DayOfWeek.Monday, DayOfWeek.Thursday);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static (int Year, int WeekOfYear) GetWeekOfYear(this DateTime date, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> date.GetWeekOfYear(CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="cultureInfo">Culture that defines the start of week day</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static (int Year, int WeekOfYear) GetWeekOfYear(this DateTime date, CultureInfo cultureInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> date.GetWeekOfYear(cultureInfo.DateTimeFormat.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="dateTimeFormatInfo">DateTimeInfo that defines the start of week day</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static (int Year, int WeekOfYear) GetWeekOfYear(this DateTime date, DateTimeFormatInfo dateTimeFormatInfo, DayOfWeek pivotDay = DayOfWeek.Thursday)
		=> date.GetWeekOfYear(dateTimeFormatInfo.FirstDayOfWeek, pivotDay);

	/// <summary>
	/// Computes the week number for a given date, considering the specified first day of the week and pivot day.
	/// </summary>
	/// <param name="date">The date for which to calculate the week number.</param>
	/// <param name="firstDayOfWeek">The first day of the week (e.g., Monday or Sunday). Defaults to the current culture's first day of the week.</param>
	/// <param name="pivotDay">The pivot day that defines the week (e.g., Thursday for ISO weeks). Defaults to Thursday.</param>
	/// <returns>The week number for the specified date.</returns>
	public static (int Year, int WeekOfYear) GetWeekOfYear(this DateTime date, DayOfWeek firstDayOfWeek, DayOfWeek pivotDay = DayOfWeek.Thursday)
	{
		// Get the first day of the year
		DateTime firstDayOfYear = new DateTime(date.Year, 1, 1);

		// Calculate the offset from the first day of the year to the pivot day
		int pivotOffset = (int)pivotDay - (int)firstDayOfYear.DayOfWeek;
		DateTime firstPivotDate = firstDayOfYear.AddDays(pivotOffset >= 0 ? pivotOffset : pivotOffset + 7);

		// Adjust the first pivot date to the correct week start
		DateTime firstWeekStartDate = firstPivotDate.AddDays(-(int)(firstPivotDate.DayOfWeek - firstDayOfWeek));

		// Calculate the difference in days between the input date and the start of the first week
		int daysDifference = (date - firstWeekStartDate).Days;

		// Calculate the week number (1-based index)
		int weekNumber = (daysDifference / 7) + 1;

		// If the date falls before the first pivot date (e.g., in the previous year)
		if (date < firstWeekStartDate)
		{
			// Handle dates in the previous year
			return GetWeekOfYear(date.AddDays(-daysDifference), firstDayOfWeek, pivotDay);
		}

		return (date.Year, weekNumber);
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
