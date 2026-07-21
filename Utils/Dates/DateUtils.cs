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
    /// <returns>
    /// The start date of the period. The returned value always carries the same
    /// <see cref="DateTimeKind"/> as <paramref name="dateTime"/> (#56).
    /// </returns>
    private static DateTime StartOfInternal(DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek, Calendar calendar)
    {
        // All branches apply the input Kind so that the result is always consistent with the
        // caller's time-zone context (#56).  Calendar.ToDateTime always produces Unspecified,
        // so we re-specify the Kind explicitly after every calendar operation.
        DateTimeKind kind = dateTime.Kind;
        switch (period)
        {
            case PeriodTypeEnum.None:
                return dateTime;
            case PeriodTypeEnum.Day:
            case PeriodTypeEnum.WorkingDay:
                return DateTime.SpecifyKind(calendar.AddDays(dateTime, 0).Date, kind);
            case PeriodTypeEnum.Week:
                var difference = (7 + ((int)calendar.GetDayOfWeek(dateTime) - (int)firstDayOfWeek)) % 7;
                DateTime weekDate = calendar.AddDays(dateTime.Date, -difference);
                return DateTime.SpecifyKind(calendar.AddDays(weekDate, 0).Date, kind);
            case PeriodTypeEnum.Month:
                return DateTime.SpecifyKind(
                    calendar.ToDateTime(calendar.GetYear(dateTime), calendar.GetMonth(dateTime), 1, 0, 0, 0, 0),
                    kind);
            case PeriodTypeEnum.Quarter:
                var month = calendar.GetMonth(dateTime);
                var quarter = (month - 1) / 3;
                var startMonth = quarter * 3 + 1;
                return DateTime.SpecifyKind(
                    calendar.ToDateTime(calendar.GetYear(dateTime), startMonth, 1, 0, 0, 0, 0),
                    kind);
            case PeriodTypeEnum.Year:
                return DateTime.SpecifyKind(
                    calendar.ToDateTime(calendar.GetYear(dateTime), 1, 1, 0, 0, 0, 0),
                    kind);
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
    /// <returns>
    /// The end date of the period. The returned value always carries the same
    /// <see cref="DateTimeKind"/> as <paramref name="dateTime"/> (#56).
    /// </returns>
    private static DateTime EndOfInternal(DateTime dateTime, PeriodTypeEnum period, DayOfWeek firstDayOfWeek, Calendar calendar)
    {
        // All branches re-specify the input Kind on each intermediate DateTime produced by Calendar
        // methods (which always yield Unspecified) before calling AddTicks(-1) so the final value
        // carries the caller's Kind throughout (#56).
        DateTimeKind kind = dateTime.Kind;
        switch (period)
        {
            case PeriodTypeEnum.None:
                return dateTime;
            case PeriodTypeEnum.Day:
            case PeriodTypeEnum.WorkingDay:
                return DateTime.SpecifyKind(calendar.AddDays(dateTime.Date, 1), kind).AddTicks(-1);
            case PeriodTypeEnum.Week:
                // Compute the start of the week using the same formula as StartOf(Week), then
                // advance exactly 7 days so the result is always the last instant of the 7th day.
                // The previous formula used (7 - diff) % 7 which produced 0 when the date was
                // already on the first day of the week, yielding end-of-current-day instead of
                // end-of-week (#53).
                var startDiff = (7 + ((int)calendar.GetDayOfWeek(dateTime) - (int)firstDayOfWeek)) % 7;
                DateTime startOfWeek = DateTime.SpecifyKind(calendar.AddDays(dateTime.Date, -startDiff), kind);
                return startOfWeek.AddDays(7).AddTicks(-1);
            case PeriodTypeEnum.Month:
                var startOfMonth = DateTime.SpecifyKind(
                    calendar.ToDateTime(calendar.GetYear(dateTime), calendar.GetMonth(dateTime), 1, 0, 0, 0, 0),
                    kind);
                DateTime startOfNextMonth = DateTime.SpecifyKind(calendar.AddMonths(startOfMonth, 1), kind);
                return startOfNextMonth.AddTicks(-1);
            case PeriodTypeEnum.Quarter:
                var month = calendar.GetMonth(dateTime);
                var quarter = (month - 1) / 3 + 1;
                var endMonth = quarter * 3;
                var startOfQuarter = DateTime.SpecifyKind(
                    calendar.ToDateTime(calendar.GetYear(dateTime), endMonth, 1, 0, 0, 0, 0),
                    kind);
                DateTime startOfNextQuarter = DateTime.SpecifyKind(calendar.AddMonths(startOfQuarter, 1), kind);
                return startOfNextQuarter.AddTicks(-1);
            case PeriodTypeEnum.Year:
                var startOfYear = DateTime.SpecifyKind(
                    calendar.ToDateTime(calendar.GetYear(dateTime), 1, 1, 0, 0, 0, 0),
                    kind);
                DateTime startOfNextYear = DateTime.SpecifyKind(calendar.AddYears(startOfYear, 1), kind);
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
    /// Converts a Unix timestamp to a <see cref="DateTime"/> in UTC.
    /// </summary>
    /// <param name="timestamp">The Unix timestamp (seconds since 1970-01-01T00:00:00Z) to convert.</param>
    /// <returns>
    /// A <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/> representing the specified
    /// Unix timestamp. Use <see cref="DateTime.ToLocalTime"/> explicitly when local time is required.
    /// </returns>
    /// <remarks>
    /// The return value is always UTC to match <see cref="ToUnixTimeStamp"/>, which converts to
    /// UTC before computing the timestamp. A round-trip therefore preserves the UTC instant without
    /// being affected by the host time zone (#54).
    /// </remarks>
    public static DateTime FromUnixTimeStamp(long timestamp)
            => DateTime.UnixEpoch.AddSeconds(timestamp);

    /// <summary>
    /// Maximum number of calendar days searched when looking for working days. Guards against
    /// hostile or misconfigured <see cref="ICalendarProvider"/> implementations that never
    /// report a working day (#55).
    /// </summary>
    public const int WorkingDaySearchHorizonDays = 3_652; // 10 years

    /// <summary>
    /// Adds a number of working days to the specified <paramref name="date"/>.
    /// </summary>
    /// <param name="date">Base date.</param>
    /// <param name="workingDays">Number of working days to add.</param>
    /// <param name="calendarProvider">Calendar providing working day information.</param>
    /// <returns>The resulting date including additional non working days.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the calendar provider prevents forward progress for more than
    /// <see cref="WorkingDaySearchHorizonDays"/> calendar days, indicating a defective provider (#55).
    /// </exception>
    public static DateTime AddWorkingDays(this DateTime date, int workingDays, ICalendarProvider calendarProvider)
    {
        workingDays.ArgMustBeGreaterOrEqualsThan(0);
        calendarProvider.Arg().MustNotBeNull();

        var toAdd = workingDays;
        var current = date;
        int calendarDaysElapsed = 0;

        while (toAdd > 0)
        {
            var end = current.AddDays(toAdd);
            int nonWorking = calendarProvider.GetNonWorkingDaysCount(current.AddDays(1), end);

            // Guard against providers that report more non-working days than exist in the range,
            // which would cause toAdd to grow and the loop to never terminate (#55).
            int rangeCalendarDays = (int)(end.Date - current.Date).TotalDays;
            if (nonWorking < 0 || nonWorking > rangeCalendarDays)
                throw new InvalidOperationException(
                    $"ICalendarProvider.GetNonWorkingDaysCount returned an invalid value ({nonWorking}) " +
                    $"for range [{current.AddDays(1):yyyy-MM-dd}, {end:yyyy-MM-dd}] ({rangeCalendarDays} days).");

            calendarDaysElapsed += rangeCalendarDays;
            if (calendarDaysElapsed > WorkingDaySearchHorizonDays)
                throw new InvalidOperationException(
                    $"AddWorkingDays exceeded the search horizon of {WorkingDaySearchHorizonDays} calendar days. " +
                    "Verify that the ICalendarProvider reports at least one working day within any reasonable period.");

            toAdd = nonWorking;
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when no working day can be found within <see cref="WorkingDaySearchHorizonDays"/> calendar days.
    /// </exception>
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when no working day can be found within <see cref="WorkingDaySearchHorizonDays"/> calendar days
    /// before <paramref name="date"/> (#55).
    /// </exception>
    public static DateTime PreviousWorkingDay(this DateTime date, ICalendarProvider calendarProvider)
    {
        calendarProvider.Arg().MustNotBeNull();

        if (calendarProvider.GetNonWorkingDaysCount(date, date) == 0)
            return date;

        var current = date.AddDays(-1);
        int daysSearched = 0;
        while (calendarProvider.GetNonWorkingDaysCount(current, current) > 0)
        {
            if (++daysSearched > WorkingDaySearchHorizonDays)
                throw new InvalidOperationException(
                    $"PreviousWorkingDay exceeded the search horizon of {WorkingDaySearchHorizonDays} calendar days. " +
                    "Verify that the ICalendarProvider reports at least one working day within any reasonable period.");
            current = current.AddDays(-1);
        }

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
        return new DateTime(year, easterDate / 31, 1, 0, 0, 0, DateTimeKind.Unspecified).AddDays(easterDate % 31);
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
