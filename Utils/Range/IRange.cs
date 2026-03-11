namespace Utils.Range;

/// <summary>
/// Defines a range of ordered values with configurable boundary inclusiveness.
/// </summary>
/// <typeparam name="T">The value type of the range boundaries.</typeparam>
public interface IRange<T>
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the start boundary of the range.
    /// </summary>
    T Start { get; }

    /// <summary>
    /// Gets the end boundary of the range.
    /// </summary>
    T End { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Start"/> belongs to the range.
    /// </summary>
    bool ContainsStart { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="End"/> belongs to the range.
    /// </summary>
    bool ContainsEnd { get; }

    /// <summary>
    /// Determines whether the supplied value belongs to the current range.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when the value is in range; otherwise, <see langword="false"/>.</returns>
    bool Contains(T value);

    /// <summary>
    /// Determines whether this range entirely contains another range.
    /// </summary>
    /// <param name="other">The range to test.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> is fully contained; otherwise, <see langword="false"/>.</returns>
    bool Contains(IRange<T> other);

    /// <summary>
    /// Determines whether this range overlaps with another range.
    /// </summary>
    /// <param name="other">The range to test.</param>
    /// <returns><see langword="true"/> when ranges overlap; otherwise, <see langword="false"/>.</returns>
    bool Overlap(IRange<T> other);

    /// <summary>
    /// Computes one or more intersections between this range and another range.
    /// </summary>
    /// <param name="other">The range to intersect with.</param>
    /// <returns>An array containing all intersection ranges. The array is empty when ranges are disjoint.</returns>
    IRange<T>[] Intersect(IRange<T> other);

    /// <summary>
    /// Determines whether the current range model is compatible with another range for aggregation in the same collection.
    /// </summary>
    /// <param name="other">The range to compare with.</param>
    /// <returns><see langword="true"/> when both ranges can coexist in the same <c>Ranges&lt;T&gt;</c>; otherwise, <see langword="false"/>.</returns>
    bool IsCompatibleWith(IRange<T> other);
}
