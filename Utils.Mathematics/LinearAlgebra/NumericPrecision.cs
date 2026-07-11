using System.Numerics;

namespace Utils.Mathematics.LinearAlgebra;

/// <summary>
/// Shared generic-precision helpers used by <see cref="Matrix{T}"/> and <see cref="Vector{T}"/> to
/// derive numerical tolerances from the scalar type itself, instead of a hard-coded literal (such as
/// <c>1e-10</c>) that has no scale-independent meaning across arbitrary <see cref="IFloatingPoint{TSelf}"/>
/// precision and can silently underflow to zero for low-precision types such as <see cref="Half"/>.
/// </summary>
internal static class NumericPrecision
{
    /// <summary>
    /// Computes <typeparamref name="T"/>'s own machine epsilon: the smallest positive value such
    /// that <c>1 + eps != 1</c> in <typeparamref name="T"/>'s own arithmetic, found generically by
    /// successive halving using only <see cref="IFloatingPoint{TSelf}"/> operations (no per-type
    /// branching required).
    /// </summary>
    public static T MachineEpsilon<T>() where T : struct, IFloatingPoint<T>
    {
        T two = T.One + T.One;
        T eps = T.One;
        while (T.One + eps / two != T.One)
            eps /= two;
        return eps;
    }
}
