using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Objects;

public static class Enumerators
{
    public static IEnumerable<T> Enumerate<T>(string ranges) 
        where T : IBinaryInteger<T>
        => Enumerate<T>(ranges, CultureInfo.CurrentCulture);

    public static IEnumerable<T> Enumerate<T>(string ranges, CultureInfo cultureInfo)
        where T : IBinaryInteger<T>
        => Enumerate<T>(ranges, cultureInfo.NumberFormat, cultureInfo.TextInfo.ListSeparator);
    public static IEnumerable<T> Enumerate<T>(string ranges, TextInfo textInfo)
		where T : IBinaryInteger<T>
        => Enumerate<T>(ranges, CultureInfo.CurrentCulture.NumberFormat, textInfo.ListSeparator);

    public static IEnumerable<T> Enumerate<T>(string ranges, params string[] separators)
        where T : IBinaryInteger<T>
        => Enumerate<T>(ranges, CultureInfo.CurrentCulture.NumberFormat, separators);

	public static IEnumerable<T> Enumerate<T>(string ranges, NumberFormatInfo numberFormatInfo, params string[] separators)
		where T : IBinaryInteger<T>
	{
		var matches = Regex.Matches(ranges, @"((?<singleValue>\d+)|(?<start>\d+)-(?<end>\d+))(" + string.Join("|", separators) + "|$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
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
    /// Enumerate every <see cref="T"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <returns></returns>
    public static IEnumerable<T> Enumerate<T>(T start, T end)
        where T : INumber<T>
        => Enumerate<T>(start, end, T.One);


	/// <summary>
	/// Enumerate every <see cref="T"/> between <paramref name="start"/> and <paramref name="end"/> included.
	/// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
	/// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
	/// </summary>
	/// <param name="start">Start of enumeration</param>
	/// <param name="end">End of enumeration</param>
	/// <returns></returns>
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
    /// Enumerate every <see cref="T"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <param name="numberOfValues">Number of values to be returned</param>
    /// <returns></returns>
    public static IEnumerable<T> EnumerateCount<T>(T start, T end, int numberOfValues)
        where T : INumber<T>
    {
        if (numberOfValues < 1) throw new ArgumentOutOfRangeException(nameof(numberOfValues), $"{nameof(numberOfValues)} must be strictly greater than 0");
        return Enumerate(start, end, T.Abs(start - end) / T.CreateChecked(numberOfValues - 1));
    }

}
