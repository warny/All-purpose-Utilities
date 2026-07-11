using System.Numerics;

namespace Utils.Mathematics;

/// <summary>
/// Descriptive statistics computed over sequences of floating-point values.
/// </summary>
public static class Statistics
{
    /// <summary>
    /// Returns the arithmetic mean of the sequence.
    /// Uses Welford's online algorithm for numerical stability.
    /// </summary>
    /// <typeparam name="T">Floating-point element type.</typeparam>
    /// <param name="values">Input sequence. Must contain at least one element.</param>
    /// <returns>The mean value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> is empty.</exception>
    public static T Mean<T>(IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>
    {
        T mean = T.Zero;
        int count = 0;
        foreach (var v in values)
        {
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
    /// <param name="values">Input sequence. Must contain at least two elements.</param>
    /// <returns>The sample variance.</returns>
    /// <exception cref="ArgumentException">Thrown when fewer than two elements are provided.</exception>
    public static T Variance<T>(IEnumerable<T> values)
        where T : struct, IFloatingPoint<T>
    {
        T mean = T.Zero;
        T m2 = T.Zero;
        int count = 0;
        foreach (var v in values)
        {
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
    /// <param name="x">First sequence.</param>
    /// <param name="y">Second sequence.</param>
    /// <returns>The sample covariance.</returns>
    /// <exception cref="ArgumentException">Thrown when sequences have different lengths or fewer than two elements.</exception>
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
    /// <typeparam name="T">Floating-point element type.</typeparam>
    /// <param name="x">First sequence.</param>
    /// <param name="y">Second sequence.</param>
    /// <returns>The correlation coefficient in [−1, 1].</returns>
    /// <exception cref="InvalidOperationException">Thrown when either sequence has zero variance.</exception>
    public static T Correlation<T>(IEnumerable<T> x, IEnumerable<T> y)
        where T : struct, IFloatingPoint<T>, IRootFunctions<T>
    {
        // Materialise both sequences once to allow multiple passes
        T[] xa = x.ToArray();
        T[] ya = y.ToArray();
        T cov = Covariance<T>(xa, ya);
        T sx = StdDev<T>(xa);
        T sy = StdDev<T>(ya);
        if (sx == T.Zero || sy == T.Zero)
            throw new InvalidOperationException("Correlation is undefined when a sequence has zero variance.");
        return cov / (sx * sy);
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
