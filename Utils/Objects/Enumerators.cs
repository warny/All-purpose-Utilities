using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Utils.Objects;

/// <summary>
/// Provides helper methods that expand textual or numeric ranges into strongly typed sequences.
/// </summary>
public static class Enumerators
{
    /// <inheritdoc cref="Enumerate{T}(string, NumberFormatInfo, string[])"/>
    public static IEnumerable<T> Enumerate<T>(string ranges)
            where T : IBinaryInteger<T>
            => Enumerate<T>(ranges, CultureInfo.CurrentCulture);

    /// <inheritdoc cref="Enumerate{T}(string, NumberFormatInfo, string[])"/>
    public static IEnumerable<T> Enumerate<T>(string ranges, CultureInfo cultureInfo)
            where T : IBinaryInteger<T>
            => Enumerate<T>(ranges, cultureInfo.NumberFormat, cultureInfo.TextInfo.ListSeparator);

    /// <inheritdoc cref="Enumerate{T}(string, NumberFormatInfo, string[])"/>
    public static IEnumerable<T> Enumerate<T>(string ranges, TextInfo textInfo)
            where T : IBinaryInteger<T>
            => Enumerate<T>(ranges, CultureInfo.CurrentCulture.NumberFormat, textInfo.ListSeparator);

    /// <inheritdoc cref="Enumerate{T}(string, NumberFormatInfo, string[])"/>
    public static IEnumerable<T> Enumerate<T>(string ranges, params string[] separators)
            where T : IBinaryInteger<T>
            => Enumerate<T>(ranges, CultureInfo.CurrentCulture.NumberFormat, separators);

    /// <summary>
    /// Parses a textual range description and yields each numeric value described by the range.
    /// </summary>
    /// <typeparam name="T">The numeric type produced by the enumeration.</typeparam>
    /// <param name="ranges">A string that may contain comma-separated values or dash-separated intervals.</param>
    /// <param name="numberFormatInfo">Culture-specific formatting information used when parsing values.</param>
    /// <param name="separators">Additional separators that delimit entries beyond the culture list separator.</param>
    /// <returns>An enumeration of all numeric values referenced by <paramref name="ranges"/>.</returns>
    public static IEnumerable<T> Enumerate<T>(string ranges, NumberFormatInfo numberFormatInfo, params string[] separators)
            where T : IBinaryInteger<T>
    {
        var matches = Regex.Matches(
                ranges,
                @"((?<singleValue>\d+)|(?<start>\d+)-(?<end>\d+))(" + string.Join("|", separators) + "|$)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (Match match in matches)
        {
            if (match.Groups["singleValue"].Success)
            {
                var value = T.Parse(match.Groups["singleValue"].Value, numberFormatInfo);
                yield return value;
            }
            else
            {
                var start = T.Parse(match.Groups["start"].Value, numberFormatInfo);
                var end = T.Parse(match.Groups["end"].Value, numberFormatInfo);

                foreach (var value in Enumerate<T>(start, end))
                {
                    yield return value;
                }
            }
        }
    }

    /// <summary>
    /// Enumerates every value between the given bounds using a default step of one unit.
    /// </summary>
    /// <typeparam name="T">The numeric type produced by the enumeration.</typeparam>
    /// <param name="start">The first value in the sequence.</param>
    /// <param name="end">The final value to include in the sequence.</param>
    /// <returns>All values starting at <paramref name="start"/> and ending at <paramref name="end"/>.</returns>
    public static IEnumerable<T> Enumerate<T>(T start, T end)
            where T : INumber<T>
            => Enumerate(start, end, T.One);

    /// <summary>
    /// Enumerates every value between the given bounds using the provided step magnitude.
    /// </summary>
    /// <typeparam name="T">The numeric type produced by the enumeration.</typeparam>
    /// <param name="start">The first value in the sequence.</param>
    /// <param name="end">The final value to include in the sequence.</param>
    /// <param name="step">The difference between two consecutive values.</param>
    /// <returns>All values starting at <paramref name="start"/> and ending at <paramref name="end"/> using <paramref name="step"/> increments.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="step"/> is less than or equal to zero.</exception>
    public static IEnumerable<T> Enumerate<T>(T start, T end, T step)
            where T : INumber<T>
    {
        if (step <= T.Zero) throw new ArgumentOutOfRangeException(nameof(step), $"{nameof(step)} must be greater than 0");

        if (start <= end)
        {
            for (var i = start; i <= end; i += step)
            {
                yield return i;
            }
        }
        else
        {
            for (var i = start; i >= end; i -= step)
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// Enumerates a fixed number of values between the start and end bounds.
    /// </summary>
    /// <typeparam name="T">The numeric type produced by the enumeration.</typeparam>
    /// <param name="start">The first value in the sequence.</param>
    /// <param name="end">The final value to include in the sequence.</param>
    /// <param name="numberOfValues">The total number of elements that should be generated.</param>
    /// <returns>An evenly spaced sequence spanning from <paramref name="start"/> to <paramref name="end"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="numberOfValues"/> is less than one.</exception>
    public static IEnumerable<T> EnumerateCount<T>(T start, T end, int numberOfValues)
            where T : INumber<T>
    {
        if (numberOfValues < 1) throw new ArgumentOutOfRangeException(nameof(numberOfValues), $"{nameof(numberOfValues)} must be strictly greater than 0");
        return Enumerate(start, end, T.Abs(start - end) / T.CreateChecked(numberOfValues - 1));
    }
}
