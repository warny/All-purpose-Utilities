﻿using System;
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
