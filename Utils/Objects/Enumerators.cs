using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Objects;

public static class Enumerators
{
    public static IEnumerable<int> Enumerate(string ranges) =>
        Enumerate(ranges,
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator,
            System.Globalization.CultureInfo.InvariantCulture.TextInfo.ListSeparator);

    public static IEnumerable<int> Enumerate(string ranges, System.Globalization.CultureInfo cultureInfo) => Enumerate(ranges, cultureInfo.TextInfo.ListSeparator);
    public static IEnumerable<int> Enumerate(string ranges, System.Globalization.TextInfo textInfo) => Enumerate(ranges, textInfo.ListSeparator);

    public static IEnumerable<int> Enumerate(string ranges, params string[] separators)
    {
        var matches = Regex.Matches(ranges, @"((?<singleValue>\d+)|(?<start>\d+)-(?<end>\d+))(" + string.Join("|", separators) + "|$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        foreach (Match match in matches)
        {
            if (match.Groups["singleValue"].Success)
            {
                var value = int.Parse(match.Groups["singleValue"].Value);
                yield return value;
            }
            else
            {
                var start = int.Parse(match.Groups["start"].Value);
                var end = int.Parse(match.Groups["end"].Value);
                foreach (var value in Enumerate(start, end))
                {
                    yield return value;
                }
            }
        }
    }

    /// <summary>
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <returns></returns>
    public static IEnumerable<byte> Enumerate(byte start, byte end, byte step = 1)
        => Enumerate((long)start, (long)end, (long)step).Select(v => (byte)v);

    /// <summary>
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <returns></returns>
    public static IEnumerable<short> Enumerate(short start, short end, short step = 1)
        => Enumerate((long)start, (long)end, (long)step).Select(v => (short)v);

    /// <summary>
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <returns></returns>
    public static IEnumerable<int> Enumerate(int start, int end, int step = 1)
        => Enumerate((long)start, (long)end, (long)step).Select(v => (int)v);

    /// <summary>
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <returns></returns>
    public static IEnumerable<long> Enumerate(long start, long end, long step = 1)
    {
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step), $"{nameof(step)} must be greater than 0");
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
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <returns></returns>
    public static IEnumerable<float> Enumerate(float start, float end, float step = 1f)
    {
        step.ArgMustBeGreaterThan(0);
        if (start <= end)
        {
            for (var i = start; i < end; i += step)
            {
                yield return i;
            }
            yield return end;
        }
        else
        {
            for (var i = start; i > end; i -= step)
            {
                yield return i;
            }
            yield return end;
        }
    }

    /// <summary>
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <param name="step">step between values</param>
    /// <returns></returns>
    public static IEnumerable<double> Enumerate(double start, double end, double step = 1f)
    {
        step.ArgMustBeGreaterThan(0);
        if (start <= end)
        {
            for (var i = start; i < end; i += step)
            {
                yield return i;
            }
            yield return end;
        }
        else
        {
            for (var i = start; i > end; i -= step)
            {
                yield return i;
            }
            yield return end;
        }
    }

    /// <summary>
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <param name="numberOfValues">Number of values to be returned</param>
    /// <returns></returns>
    public static IEnumerable<float> EnumerateCount(float start, float end, int numberOfValues)
    {
        numberOfValues.ArgMustBeGreaterThan(1);
        return Enumerate(start, end, Math.Abs(start - end) / (numberOfValues - 1));
    }

    /// <summary>
    /// Enumerate every <see cref="System.Int32"/> between <paramref name="start"/> and <paramref name="end"/> included.
    /// The enumeration is ascending if <paramref name="start"/> &lt;<paramref name="end"/>.
    /// The enumeration is descending if <paramref name="start"/> &gt;<paramref name="end"/>.
    /// </summary>
    /// <param name="start">Start of enumeration</param>
    /// <param name="end">End of enumeration</param>
    /// <param name="numberOfValues">Number of values to be returned</param>
    /// <returns></returns>
    public static IEnumerable<double> EnumerateCount(double start, double end, int numberOfValues)
    {
        if (numberOfValues < 1) throw new ArgumentOutOfRangeException(nameof(numberOfValues), $"{nameof(numberOfValues)} must be strictly greater than 0");
        return Enumerate(start, end, Math.Abs(start - end) / (numberOfValues - 1));
    }

}
