namespace Utils.Range;

/// <summary>
/// Represents an inclusive range on a cyclic domain.
/// </summary>
/// <typeparam name="T">The value type of the cyclic range.</typeparam>
/// <remarks>
/// <para>
/// A <see cref="CyclicRange{T}"/> where <see cref="IRange{T}.Start"/> equals
/// <see cref="IRange{T}.End"/> represents a <b>singleton</b> — a range containing exactly one
/// value.  To represent the <b>full cycle</b> (all values from
/// <see cref="MinValue"/> to <see cref="MaxValue"/>), use the <see cref="FullCycle"/> factory
/// method instead (#21).
/// </para>
/// <para>
/// Both <see cref="IRange{T}.Start"/> and <see cref="IRange{T}.End"/> must lie within
/// [<see cref="MinValue"/>, <see cref="MaxValue"/>] (#20).
/// </para>
/// </remarks>
public class CyclicRange<T> : IRange<T>
    where T : IComparable<T>
{
    private readonly bool _isFullCycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="CyclicRange{T}"/> class.
    /// </summary>
    /// <param name="start">The start boundary in the cycle. Must be within [<paramref name="minValue"/>, <paramref name="maxValue"/>].</param>
    /// <param name="end">The end boundary in the cycle. Must be within [<paramref name="minValue"/>, <paramref name="maxValue"/>].</param>
    /// <param name="minValue">The minimum representable value of the cycle.</param>
    /// <param name="maxValue">The maximum representable value of the cycle.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the arguments is <see langword="null"/> (for reference types).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="minValue"/> exceeds <paramref name="maxValue"/>, or when
    /// <paramref name="start"/> or <paramref name="end"/> lies outside
    /// [<paramref name="minValue"/>, <paramref name="maxValue"/>] (#20).
    /// </exception>
    public CyclicRange(T start, T end, T minValue, T maxValue)
        : this(start, end, minValue, maxValue, isFullCycle: false)
    {
    }

    /// <summary>
    /// Private constructor that also accepts the full-cycle flag.
    /// </summary>
    private CyclicRange(T start, T end, T minValue, T maxValue, bool isFullCycle)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(minValue);
        ArgumentNullException.ThrowIfNull(maxValue);

        if (minValue.CompareTo(maxValue) > 0)
            throw new ArgumentException("minValue must be less than or equal to maxValue.");

        if (start.CompareTo(minValue) < 0 || start.CompareTo(maxValue) > 0)
            throw new ArgumentException(
                $"start ({start}) must be within the cycle bounds [{minValue}, {maxValue}].", nameof(start));

        if (end.CompareTo(minValue) < 0 || end.CompareTo(maxValue) > 0)
            throw new ArgumentException(
                $"end ({end}) must be within the cycle bounds [{minValue}, {maxValue}].", nameof(end));

        Start = start;
        End = end;
        MinValue = minValue;
        MaxValue = maxValue;
        _isFullCycle = isFullCycle;
    }

    /// <summary>
    /// Creates a <see cref="CyclicRange{T}"/> that covers the complete cycle from
    /// <paramref name="minValue"/> to <paramref name="maxValue"/>, inclusive (#21).
    /// </summary>
    /// <param name="minValue">The minimum representable value of the cycle.</param>
    /// <param name="maxValue">The maximum representable value of the cycle.</param>
    /// <returns>A cyclic range spanning the full domain.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="minValue"/> exceeds <paramref name="maxValue"/>.
    /// </exception>
    public static CyclicRange<T> FullCycle(T minValue, T maxValue)
        => new(minValue, minValue, minValue, maxValue, isFullCycle: true);

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
        if (_isFullCycle)
            return value.CompareTo(MinValue) >= 0 && value.CompareTo(MaxValue) <= 0;

        if (Start.CompareTo(End) < 0)
        {
            return value.CompareTo(Start) >= 0 && value.CompareTo(End) <= 0;
        }

        if (Start.CompareTo(End) > 0)
        {
            return value.CompareTo(Start) >= 0 || value.CompareTo(End) <= 0;
        }

        // Start == End: singleton
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

        if (!IsCompatibleWith(otherCyclic))
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
        if (_isFullCycle)
        {
            return [new Range<T>(MinValue, MaxValue)];
        }

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
    /// <remarks>
    /// Two cyclic ranges are compatible when they share the same cycle bounds, determined by
    /// <see cref="IComparable{T}.CompareTo"/> so that the ordering relation used for containment
    /// and intersection is consistent with the compatibility test (#22).
    /// </remarks>
    public bool IsCompatibleWith(IRange<T> other)
    {
        return other is CyclicRange<T> cyclicRange
            && MinValue.CompareTo(cyclicRange.MinValue) == 0
            && MaxValue.CompareTo(cyclicRange.MaxValue) == 0;
    }
}
