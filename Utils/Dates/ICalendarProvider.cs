using System;
using System.Collections.Generic;

namespace Utils.Dates;

/// <summary>
/// Provides access to calendar-specific information about working and non-working days.
/// </summary>
public interface ICalendarProvider
{
    /// <summary>
    /// Gets the number of non working days between <paramref name="start"/> and <paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start date of the range.</param>
    /// <param name="end">End date of the range.</param>
    /// <returns>The count of non working days.</returns>
    int GetNonWorkingDaysCount(DateTime start, DateTime end);

    /// <summary>
    /// Retrieves all holidays occurring between <paramref name="start"/> and <paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start date of the range.</param>
    /// <param name="end">End date of the range.</param>
    /// <returns>An enumerable of holiday dates.</returns>
    IEnumerable<DateTime> GetHollydays(DateTime start, DateTime end);

    /// <summary>
    /// Retrieves all working days occurring between <paramref name="start"/> and <paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start date of the range.</param>
    /// <param name="end">End date of the range.</param>
    /// <returns>An enumerable of working day dates.</returns>
    IEnumerable<DateTime> GetWorkingDays(DateTime start, DateTime end);

    /// <summary>
    /// Gets the number of working days between <paramref name="start"/> and <paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start date of the range.</param>
    /// <param name="end">End date of the range.</param>
    /// <returns>The count of working days.</returns>
    int GetWorkingDaysCount(DateTime start, DateTime end);
}
