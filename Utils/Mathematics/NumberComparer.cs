using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Utils.Objects;

namespace Utils.Mathematics;

/// <summary>
/// Provides floating-point comparisons that allow for small deviations using a configurable interval.
/// </summary>
[DebuggerDisplay("FloatingPointComparer (±{Interval})")]
public class FloatingPointComparer<T> : IComparer<T>, IEqualityComparer<T>
    where
        T : struct, IFloatingPointIeee754<T>
{
    /// <summary>
    /// Gets the tolerance interval used to consider two numbers equal.
    /// </summary>
    public T Interval { get; }

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
    public int Compare(T x, T y) => x.CompareTo(y);

    /// <inheritdoc />
    public bool Equals(T x, T y) => x.Between(y - Interval, y + Interval);

    /// <inheritdoc />
    // Fuzzy equality makes a collision-free hash impossible; returning a constant satisfies
    // the IEqualityComparer contract (equal objects must share a hash code) at the cost of
    // degrading hash-based collections to O(n) lookup.
    public int GetHashCode([DisallowNull] T obj) => 0;
}
