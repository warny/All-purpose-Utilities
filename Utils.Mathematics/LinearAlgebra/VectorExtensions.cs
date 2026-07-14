using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Extension methods for <see cref="Vector{T}"/> that require trigonometric capabilities.
/// </summary>
public static class VectorExtensions
{
    /// <summary>
    /// Returns the angle in radians between this vector and <paramref name="other"/>.
    /// </summary>
    /// <typeparam name="T">Numeric component type.</typeparam>
    /// <param name="self">This vector.</param>
    /// <param name="other">The other vector. Must have the same dimension.</param>
    /// <returns>The angle in radians in the range [0, π].</returns>
    /// <exception cref="ArgumentException">Thrown when the vectors have different dimensions.</exception>
    /// <exception cref="InvalidOperationException">Thrown when either vector is a zero vector.</exception>
    public static T AngleWith<T>(this Vector<T> self, Vector<T> other)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>, ITrigonometricFunctions<T>
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(other);
        if (self.Dimension != other.Dimension)
            throw new ArgumentException("Vectors must have the same dimension.", nameof(other));

        // Normalize both vectors before computing the dot product: avoids norm-product overflow
        // (two individually finite norms can still overflow when multiplied - see
        // TODO-2026-07-11-pass6.md item #70) and naturally rejects zero/near-zero vectors via
        // Normalize()'s scale-aware tolerance policy (item #71).
        Vector<T> selfUnit = self.Normalize();
        Vector<T> otherUnit = other.Normalize();
        T cosAngle = selfUnit * otherUnit;

        // A non-finite cosine after normalization means an input component was NaN or infinite;
        // clamping it would silently return a boundary angle instead of diagnosing the bad input
        // (see TODO-2026-07-11-pass6.md item #76). Reserve clamping only for small documented
        // floating-point drift inside [-1, 1].
        if (!T.IsFinite(cosAngle))
            throw new InvalidOperationException("The angle computation produced a non-finite cosine; check that all vector components are finite.");
        cosAngle = T.Clamp(cosAngle, -T.One, T.One);
        return T.Acos(cosAngle);
    }
}
