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
    /// <returns>
    /// The approximate value of ∫f(x)dx from a to b. If <paramref name="f"/> returns a non-finite
    /// value anywhere in the interval, that non-finite value propagates into the result (visible to
    /// the caller as NaN or an infinity) rather than being silently discarded.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="steps"/> is not positive, when <paramref name="steps"/> is the
    /// odd value <see cref="int.MaxValue"/> (which cannot be rounded up to an even count without
    /// overflowing), or when <paramref name="a"/>/<paramref name="b"/> is not finite.
    /// </exception>
    public static T Integrate<T>(Func<T, T> f, T a, T b, int steps = 1000)
        where T : struct, IFloatingPoint<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        if (steps <= 0) throw new ArgumentOutOfRangeException(nameof(steps), steps, "Steps must be positive.");
        if (!T.IsFinite(a)) throw new ArgumentOutOfRangeException(nameof(a), a, "Must be finite.");
        if (!T.IsFinite(b)) throw new ArgumentOutOfRangeException(nameof(b), b, "Must be finite.");
        if (steps % 2 != 0)
        {
            if (steps == int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(steps), steps, "An odd step count of int.MaxValue cannot be rounded up to an even value without overflowing.");
            steps++;
        }

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
    /// <param name="points">Sequence of (x, y) data points. All x values must be distinct and every coordinate finite.</param>
    /// <param name="at">The point at which to evaluate the interpolating polynomial. Must be finite.</param>
    /// <returns>The interpolated value at <paramref name="at"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no points are provided, a coordinate is not finite, or x values are not distinct.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="at"/> is not finite.</exception>
    public static T Lagrange<T>(IEnumerable<(T x, T y)> points, T at)
        where T : struct, IFloatingPoint<T>
    {
        (T x, T y)[] pts = points.ToArray();
        if (pts.Length == 0)
            throw new ArgumentException("At least one data point is required.", nameof(points));
        if (!T.IsFinite(at))
            throw new ArgumentOutOfRangeException(nameof(at), at, "Must be finite.");

        // Validate every point - including finiteness and pairwise distinctness of x - before
        // evaluating anything. A NaN x value bypasses an equality-based distinctness check (NaN is
        // never equal to anything, including another NaN), so finiteness must be checked
        // separately rather than relying on the distinctness comparison alone.
        for (int i = 0; i < pts.Length; i++)
        {
            if (!T.IsFinite(pts[i].x) || !T.IsFinite(pts[i].y))
                throw new ArgumentException("All data point coordinates must be finite.", nameof(points));
            for (int j = i + 1; j < pts.Length; j++)
            {
                if (pts[i].x == pts[j].x)
                    throw new ArgumentException("All x values must be distinct.", nameof(points));
            }
        }

        T result = T.Zero;
        for (int i = 0; i < pts.Length; i++)
        {
            T basis = T.One;
            for (int j = 0; j < pts.Length; j++)
            {
                if (j == i) continue;
                basis *= (at - pts[j].x) / (pts[i].x - pts[j].x);
            }
            result += pts[i].y * basis;
        }
        return result;
    }
}
