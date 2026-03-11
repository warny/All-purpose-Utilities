namespace Utils.Range;

/// <summary>
/// Represents an inclusive range on a cyclic domain.
/// </summary>
/// <typeparam name="T">The value type of the cyclic range.</typeparam>
public class CyclicRange<T> : IRange<T>
    where T : IComparable<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CyclicRange{T}"/> class.
    /// </summary>
    /// <param name="start">The start boundary in the cycle.</param>
    /// <param name="end">The end boundary in the cycle.</param>
    /// <param name="minValue">The minimum representable value of the cycle.</param>
    /// <param name="maxValue">The maximum representable value of the cycle.</param>
    public CyclicRange(T start, T end, T minValue, T maxValue)
    {
        if (minValue.CompareTo(maxValue) > 0)
        {
            throw new ArgumentException("minValue must be less than or equal to maxValue.");
        }

        Start = start;
        End = end;
        MinValue = minValue;
        MaxValue = maxValue;
    }

    /// <inheritdoc />
    public T Start { get; }

    /// <inheritdoc />
    public T End { get; }

    /// <summary>
    /// Gets the minimum representable value in the cycle.
    /// </summary>
    public T MinValue { get; }

    /// <summary>
    /// Gets the maximum representable value in the cycle.
    /// </summary>
    public T MaxValue { get; }

    /// <inheritdoc />
    public bool ContainsStart => true;

    /// <inheritdoc />
    public bool ContainsEnd => true;

    /// <inheritdoc />
    public virtual bool Contains(T value)
    {
        if (Start.CompareTo(End) < 0)
        {
            return value.CompareTo(Start) >= 0 && value.CompareTo(End) <= 0;
        }

        if (Start.CompareTo(End) > 0)
        {
            return value.CompareTo(End) >= 0 || value.CompareTo(Start) <= 0;
        }

        return value.CompareTo(Start) == 0;
    }

    /// <inheritdoc />
    public bool Contains(IRange<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Contains(other.Start) && Contains(other.End);
    }

    /// <inheritdoc />
    public bool Overlap(IRange<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Contains(other.Start) || Contains(other.End) || other.Contains(Start) || other.Contains(End);
    }

    /// <inheritdoc />
    public IRange<T>[] Intersect(IRange<T> other)
    {
        if (other is not CyclicRange<T> otherCyclic)
        {
            return [];
        }

        if (!EqualityComparer<T>.Default.Equals(MinValue, otherCyclic.MinValue)
            || !EqualityComparer<T>.Default.Equals(MaxValue, otherCyclic.MaxValue))
        {
            return [];
        }

        var leftParts = SplitToLinearRanges();
        var rightParts = otherCyclic.SplitToLinearRanges();
        List<IRange<T>> result = [];

        foreach (var left in leftParts)
        {
            foreach (var right in rightParts)
            {
                var intersections = left.Intersect(right);
                if (intersections.Length > 0)
                {
                    result.AddRange(intersections);
                }
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Splits the cyclic range into one or two linear ranges using the configured cycle bounds.
    /// </summary>
    /// <returns>An array containing one or two linear ranges.</returns>
    internal Range<T>[] SplitToLinearRanges()
    {
        int compare = Start.CompareTo(End);
        if (compare < 0)
        {
            return [new Range<T>(Start, End)];
        }

        if (compare > 0)
        {
            return [
                new Range<T>(MinValue, End),
                new Range<T>(Start, MaxValue)
            ];
        }

        return [new Range<T>(Start, End)];
    }

    /// <inheritdoc />
    public bool IsCompatibleWith(IRange<T> other)
    {
        return other is CyclicRange<T> cyclicRange
            && EqualityComparer<T>.Default.Equals(MinValue, cyclicRange.MinValue)
            && EqualityComparer<T>.Default.Equals(MaxValue, cyclicRange.MaxValue);
    }
}
