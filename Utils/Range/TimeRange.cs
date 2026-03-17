using System;
using System.Collections.Generic;

namespace Utils.Range;

/// <summary>
/// Represents an inclusive cyclic range of <see cref="TimeOnly"/> values.
/// </summary>
public sealed class TimeRange : CyclicRange<TimeOnly>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeRange"/> class.
    /// </summary>
    /// <param name="start">The start time of the range.</param>
    /// <param name="end">The end time of the range.</param>
    public TimeRange(TimeOnly start, TimeOnly end)
        : base(start, end, TimeOnly.MinValue, TimeOnly.MaxValue)
    {
    }

    /// <summary>
    /// Determines whether the specified object represents the same time range boundaries.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><see langword="true"/> when the compared object is a compatible time range with identical boundaries; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj)
    {
        return obj is IRange<TimeOnly> other
            && EqualityComparer<TimeOnly>.Default.Equals(Start, other.Start)
            && EqualityComparer<TimeOnly>.Default.Equals(End, other.End);
    }

    /// <summary>
    /// Returns a hash code for the current time range.
    /// </summary>
    /// <returns>A hash code based on <see cref="IRange{TimeOnly}.Start"/> and <see cref="IRange{TimeOnly}.End"/>.</returns>
    public override int GetHashCode() => HashCode.Combine(Start, End);
}

