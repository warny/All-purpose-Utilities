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
        if (self.Dimension != other.Dimension)
            throw new ArgumentException("Vectors must have the same dimension.", nameof(other));

        T normProduct = self.Norm * other.Norm;
        if (normProduct == T.Zero)
            throw new InvalidOperationException("Cannot compute the angle with a zero vector.");

        T cosAngle = (self * other) / normProduct;
        // Guard against floating-point drift outside [-1, 1].
        cosAngle = T.Clamp(cosAngle, -T.One, T.One);
        return T.Acos(cosAngle);
    }
}
