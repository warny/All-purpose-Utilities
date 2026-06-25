using System.Diagnostics;
using System.Numerics;
using Utils.Objects;

namespace Utils.Mathematics;

/// <summary>
/// Provides floating-point comparisons that allow for small deviations using a configurable interval.
/// Implements only <see cref="IComparer{T}"/>: fuzzy equality cannot be used for hashing.
/// </summary>
/// <remarks>
/// This comparer is intentionally non-transitive: two values that are each within <see cref="Interval"/>
/// of a middle value may not be within <see cref="Interval"/> of each other.
/// Do not use with <see cref="SortedSet{T}"/>, <see cref="SortedDictionary{TKey, TValue}"/>,
/// or any algorithm that requires a total strict order.
/// </remarks>
[DebuggerDisplay("FloatingPointComparer (±{Interval})")]
public class FloatingPointComparer<T> : IComparer<T>
    where
        T : struct, IFloatingPointIeee754<T>
{
    /// <summary>
    /// Gets the tolerance interval used to consider two numbers equal.
    /// </summary>
    public T Interval { get; }

    private static readonly Dictionary<int, FloatingPointComparer<T>> _precisionCache = [];

    /// <summary>
    /// Returns a cached <see cref="FloatingPointComparer{T}"/> for the specified decimal <paramref name="precision"/>.
    /// Repeated calls with the same precision return the same instance.
    /// </summary>
    /// <param name="precision">The number of decimal places that must match.</param>
    public static FloatingPointComparer<T> ForPrecision(int precision)
    {
        lock (_precisionCache)
        {
            if (!_precisionCache.TryGetValue(precision, out var comparer))
            {
                comparer = new FloatingPointComparer<T>(precision);
                _precisionCache[precision] = comparer;
            }
            return comparer;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FloatingPointComparer{T}"/> class using a decimal precision.
    /// </summary>
    /// <param name="precision">The number of decimal places that must match.</param>
    public FloatingPointComparer(int precision)
        : this(T.Pow(T.CreateChecked(10), T.CreateChecked(-precision)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FloatingPointComparer{T}"/> class with a custom interval.
    /// </summary>
    /// <param name="interval">The maximum allowed difference between two values.</param>
    public FloatingPointComparer(T interval)
    {
        Interval = interval;
    }

    /// <summary>
    /// Compares two values, treating them as equal when their absolute difference is at most <see cref="Interval"/>.
    /// </summary>
    /// <returns>
    /// 0 when <c>|x − y| ≤ <see cref="Interval"/></c>; otherwise the sign of <c>x − y</c>.
    /// </returns>
    /// <remarks>
    /// Because this uses a tolerance zone, the ordering is not transitive:
    /// <c>Compare(a, b) == 0</c> and <c>Compare(b, c) == 0</c> do not imply <c>Compare(a, c) == 0</c>.
    /// </remarks>
    public int Compare(T x, T y)
    {
        if (T.Abs(x - y) <= Interval)
            return 0;
        return x.CompareTo(y);
    }
}
