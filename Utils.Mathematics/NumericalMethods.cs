using System.Numerics;

namespace Utils.Mathematics;

/// <summary>
/// Numerical integration and interpolation utilities.
/// </summary>
public static class NumericalMethods
{
    // -------------------------------------------------------------------------
    // Numerical integration (Simpson's 1/3 rule)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Numerically integrates <paramref name="f"/> over [<paramref name="a"/>, <paramref name="b"/>]
    /// using the composite Simpson's 1/3 rule.
    /// </summary>
    /// <typeparam name="T">Floating-point scalar type.</typeparam>
    /// <param name="f">Integrand function. Must be defined and finite on [a, b].</param>
    /// <param name="a">Lower bound of integration.</param>
    /// <param name="b">Upper bound of integration.</param>
    /// <param name="steps">
    /// Number of sub-intervals. Must be positive and even; an odd value is rounded up by one.
    /// Higher values increase accuracy at the cost of more function evaluations.
    /// </param>
    /// <returns>The approximate value of ∫f(x)dx from a to b.</returns>
    public static T Integrate<T>(Func<T, T> f, T a, T b, int steps = 1000)
        where T : struct, IFloatingPoint<T>
    {
        if (steps <= 0) throw new ArgumentOutOfRangeException(nameof(steps), "Steps must be positive.");
        if (steps % 2 != 0) steps++;

        T n = T.CreateChecked(steps);
        T h = (b - a) / n;

        T sum = f(a) + f(b);
        for (int i = 1; i < steps; i++)
        {
            T x = a + T.CreateChecked(i) * h;
            sum += T.CreateChecked(i % 2 == 0 ? 2 : 4) * f(x);
        }

        return h / T.CreateChecked(3) * sum;
    }

    // -------------------------------------------------------------------------
    // Lagrange interpolation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates the Lagrange interpolating polynomial at <paramref name="at"/>,
    /// given a set of data points.
    /// </summary>
    /// <remarks>
    /// The result is exact at each data point and provides a polynomial approximation elsewhere.
    /// For large point sets, consider alternative methods as Lagrange interpolation can exhibit
    /// Runge's phenomenon near the boundaries.
    /// </remarks>
    /// <typeparam name="T">Floating-point scalar type.</typeparam>
    /// <param name="points">Sequence of (x, y) data points. All x values must be distinct.</param>
    /// <param name="at">The point at which to evaluate the interpolating polynomial.</param>
    /// <returns>The interpolated value at <paramref name="at"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when no points are provided or x values are not distinct.</exception>
    public static T Lagrange<T>(IEnumerable<(T x, T y)> points, T at)
        where T : struct, IFloatingPoint<T>
    {
        (T x, T y)[] pts = points.ToArray();
        if (pts.Length == 0)
            throw new ArgumentException("At least one data point is required.", nameof(points));

        T result = T.Zero;
        for (int i = 0; i < pts.Length; i++)
        {
            T basis = T.One;
            for (int j = 0; j < pts.Length; j++)
            {
                if (j == i) continue;
                T denom = pts[i].x - pts[j].x;
                if (denom == T.Zero)
                    throw new ArgumentException("All x values must be distinct.", nameof(points));
                basis *= (at - pts[j].x) / denom;
            }
            result += pts[i].y * basis;
        }
        return result;
    }
}
