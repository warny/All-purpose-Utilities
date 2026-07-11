using System.Numerics;

namespace Utils.Mathematics;

/// <summary>
/// Descriptive statistics computed over sequences of floating-point values.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-finite value policy:</b> every operation here that constrains <c>T</c> to
/// <see cref="IFloatingPoint{TSelf}"/> validates each input value with <c>T.IsFinite(value)</c>
/// as it is enumerated and rejects the sequence with an <see cref="ArgumentException"/> on the first
/// NaN or infinite value found, rather than letting it propagate into a NaN/infinite (and
/// implementation-dependent, since propagation depends on where the value falls relative to sort
/// order or accumulation order) result. This is a single consistent policy applied uniformly across
/// <see cref="Mean{T}"/>, <see cref="Variance{T}"/>, <see cref="Covariance{T}"/>, and
/// <see cref="Correlation{T}"/>.
/// </para>
/// <para>
/// <see cref="Median{T}"/> is generic over any <see cref="IComparable{T}"/>, not just floating-point
/// types, so it cannot apply the same finiteness check; its behavior for a type whose
/// <see cref="IComparable{T}.CompareTo"/> does not define a total order (floating-point NaN under the
/// default comparer being the standard example) is therefore whatever <see cref="Array.Sort{T}(T[])"/>
/// does with such values, which is unspecified. Validate finiteness before calling if that matters.
/// </para>
/// </remarks>
public static class Statistics
{
    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="value"/> is NaN or infinite. See
    /// the non-finite value policy documented on <see cref="Statistics"/>.
    /// </summary>
    private static void ValidateFinite<T>(T value, string paramName)
        where T : struct, IFloatingPoint<T>
    {
        if (!T.IsFinite(value))
            throw new ArgumentException($"Sequence contains a non-finite value ({value}).", paramName);
    }

    /// <summary>
    /// Returns the arithmetic mean of the sequence.
    /// Uses Welford's online algorithm for numerical stability.
    /// </summary>
    /// <typeparam name="T">Floating-point element type.</typeparam>
    /// <param name="values">Input sequence. Must contain at least one element, all finite.</param>
    /// <returns>The mean value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> is empty or contains a non-finite value.</exception>
    public static T Mean<T>(IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>
    {
        T mean = T.Zero;
        int count = 0;
        foreach (var v in values)
        {
            ValidateFinite(v, nameof(values));
            count++;
            mean += (v - mean) / T.CreateChecked(count);
        }
        if (count == 0)
            throw new ArgumentException("Sequence must contain at least one element.", nameof(values));
        return mean;
    }

    /// <summary>
    /// Returns the sample variance (divided by n−1) of the sequence.
    /// Uses Welford's online algorithm for numerical stability.
    /// </summary>
    /// <typeparam name="T">Floating-point element type.</typeparam>
    /// <param name="values">Input sequence. Must contain at least two elements, all finite.</param>
    /// <returns>The sample variance.</returns>
    /// <exception cref="ArgumentException">Thrown when fewer than two elements are provided, or a value is non-finite.</exception>
    public static T Variance<T>(IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>
    {
        T mean = T.Zero;
        T m2 = T.Zero;
        int count = 0;
        foreach (var v in values)
        {
            ValidateFinite(v, nameof(values));
            count++;
            T delta = v - mean;
            mean += delta / T.CreateChecked(count);
            m2 += delta * (v - mean);
        }
        if (count < 2)
            throw new ArgumentException("Variance requires at least two elements.", nameof(values));
        return m2 / T.CreateChecked(count - 1);
    }

    /// <summary>
    /// Returns the sample standard deviation of the sequence.
    /// </summary>
    /// <typeparam name="T">Floating-point element type.</typeparam>
    /// <param name="values">Input sequence. Must contain at least two elements.</param>
    /// <returns>The standard deviation.</returns>
    public static T StdDev<T>(IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
        => T.Sqrt(Variance<T>(values));

    /// <summary>
    /// Returns the sample covariance between two sequences of equal length.
    /// </summary>
    /// <typeparam name="T">Floating-point element type.</typeparam>
    /// <param name="x">First sequence, all values finite.</param>
    /// <param name="y">Second sequence, all values finite.</param>
    /// <returns>The sample covariance.</returns>
    /// <exception cref="ArgumentException">Thrown when sequences have different lengths, fewer than two elements, or a value is non-finite.</exception>
    public static T Covariance<T>(IEnumerable<T> x, IEnumerable<T> y)
        where T : struct, IFloatingPoint<T>
    {
        T meanX = T.Zero, meanY = T.Zero, cov = T.Zero;
        int count = 0;
        using var ex = x.GetEnumerator();
        using var ey = y.GetEnumerator();
        while (ex.MoveNext())
        {
            if (!ey.MoveNext())
                throw new ArgumentException("Sequences must have the same length.", nameof(y));
            ValidateFinite(ex.Current, nameof(x));
            ValidateFinite(ey.Current, nameof(y));
            count++;
            T cx = T.CreateChecked(count);
            T dx = ex.Current - meanX;
            meanX += dx / cx;
            T dy = ey.Current - meanY;
            meanY += dy / cx;
            cov += dx * (ey.Current - meanY);
        }
        if (ey.MoveNext())
            throw new ArgumentException("Sequences must have the same length.", nameof(y));
        if (count < 2)
            throw new ArgumentException("Covariance requires at least two elements.", nameof(x));
        return cov / T.CreateChecked(count - 1);
    }

    /// <summary>
    /// Returns the Pearson correlation coefficient between two sequences of equal length.
    /// </summary>
    /// <remarks>
    /// Computes both variances and the covariance in a single online pass (rather than materializing
    /// both sequences and separately calling <see cref="Covariance{T}"/> and <see cref="StdDev{T}"/>,
    /// each of which would re-enumerate and independently recompute its own mean).
    /// </remarks>
    /// <typeparam name="T">Floating-point element type.</typeparam>
    /// <param name="x">First sequence, all values finite.</param>
    /// <param name="y">Second sequence, all values finite.</param>
    /// <returns>The correlation coefficient in [−1, 1].</returns>
    /// <exception cref="ArgumentException">Thrown when the sequences have different lengths, fewer than two elements, or a value is non-finite.</exception>
    /// <exception cref="InvalidOperationException">Thrown when either sequence has zero variance.</exception>
    public static T Correlation<T>(IEnumerable<T> x, IEnumerable<T> y)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
    {
        T meanX = T.Zero, meanY = T.Zero;
        T m2X = T.Zero, m2Y = T.Zero, cov = T.Zero;
        int count = 0;
        using var ex = x.GetEnumerator();
        using var ey = y.GetEnumerator();
        while (ex.MoveNext())
        {
            if (!ey.MoveNext())
                throw new ArgumentException("Sequences must have the same length.", nameof(y));
            ValidateFinite(ex.Current, nameof(x));
            ValidateFinite(ey.Current, nameof(y));
            count++;
            T cn = T.CreateChecked(count);
            T dx = ex.Current - meanX;
            meanX += dx / cn;
            T dy = ey.Current - meanY;
            meanY += dy / cn;
            m2X += dx * (ex.Current - meanX);
            m2Y += dy * (ey.Current - meanY);
            cov += dx * (ey.Current - meanY);
        }
        if (ey.MoveNext())
            throw new ArgumentException("Sequences must have the same length.", nameof(y));
        if (count < 2)
            throw new ArgumentException("Correlation requires at least two elements.", nameof(x));

        T sx = T.Sqrt(m2X / T.CreateChecked(count - 1));
        T sy = T.Sqrt(m2Y / T.CreateChecked(count - 1));
        if (sx == T.Zero || sy == T.Zero)
            throw new InvalidOperationException("Correlation is undefined when a sequence has zero variance.");
        return (cov / T.CreateChecked(count - 1)) / (sx * sy);
    }

    /// <summary>
    /// Returns the median of the sequence.
    /// For an even number of elements, returns the lower of the two middle values.
    /// </summary>
    /// <typeparam name="T">Comparable element type.</typeparam>
    /// <param name="values">Input sequence. Must contain at least one element.</param>
    /// <returns>The median value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> is empty.</exception>
    public static T Median<T>(IEnumerable<T> values)
        where T : struct, IComparable<T>
    {
        T[] sorted = values.ToArray();
        if (sorted.Length == 0)
            throw new ArgumentException("Sequence must contain at least one element.", nameof(values));
        Array.Sort(sorted);
        return sorted[(sorted.Length - 1) / 2];
    }
}
