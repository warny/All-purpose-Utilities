using System.Diagnostics;
using System.Numerics;
using Utils.Objects;

namespace Utils.Mathematics;

/// <summary>
/// Provides floating-point comparisons that allow for small deviations using a configurable interval.
/// Implements only <see cref="IComparer{T}"/>: fuzzy equality cannot be used for hashing.
/// </summary>
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

    /// <inheritdoc />
    public int Compare(T x, T y)
    {
        if (T.Abs(x - y) <= Interval)
            return 0;
        return x.CompareTo(y);
    }
}
