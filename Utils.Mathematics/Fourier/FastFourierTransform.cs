using System.Numerics;

namespace Utils.Mathematics.Fourier;

/// <summary>
/// Provides a recursive Cooley-Tukey Fast Fourier Transform implementation.
/// </summary>
public static class FastFourierTransform
{
    /// <summary>
    /// Performs an in-place FFT on the provided sample array.
    /// </summary>
    /// <param name="array">Sample array to transform in place. Its length must be a power of two (a zero-length array is rejected, not treated as a no-op).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="array"/> length is not a power of two (this includes length zero).
    /// </exception>
    public static void Transform(Complex[] array)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (!Mathematics.MathEx.IsPowerOfTwo(array.Length))
            throw new ArgumentException("Array length must be a power of two.", nameof(array));
        Transform(array.AsSpan());
    }

    /// <summary>
    /// Performs an in-place inverse FFT on the provided sample array.
    /// </summary>
    /// <param name="array">Frequency-domain array to transform back to the time domain. Its length must be a power of two (a zero-length array is rejected, not treated as a no-op).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="array"/> length is not a power of two (this includes length zero).
    /// </exception>
    public static void InverseTransform(Complex[] array)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (!Mathematics.MathEx.IsPowerOfTwo(array.Length))
            throw new ArgumentException("Array length must be a power of two.", nameof(array));

        for (int i = 0; i < array.Length; i++)
            array[i] = Complex.Conjugate(array[i]);

        Transform(array.AsSpan());

        double n = array.Length;
        for (int i = 0; i < array.Length; i++)
            array[i] = Complex.Conjugate(array[i]) / n;
    }

    private static void Transform(Span<Complex> span)
    {
        if (span.Length < 2) return;
        // Single scratch buffer sized for the largest recursion level (half the top-level length),
        // reused by every recursive call instead of each level allocating its own half-size buffer:
        // sibling recursive calls run sequentially (never concurrently), so reusing the same buffer
        // is safe and turns O(n) allocations across the whole transform into one.
        Complex[] scratch = new Complex[span.Length >> 1];
        Transform(span, scratch);
    }

    private static void Transform(Span<Complex> span, Span<Complex> scratch)
    {
        int n = span.Length;
        if (n < 2) return;

        int n2 = n >> 1;
        Separate(span, scratch[..n2]);
        Transform(span[..n2], scratch);
        Transform(span[n2..], scratch);

        for (int k = 0; k < n2; k++)
        {
            Complex even = span[k];
            Complex odd = span[k + n2];
            Complex twiddle = Complex.Exp(new Complex(0, -2 * Math.PI * k / n));
            span[k] = even + twiddle * odd;
            span[k + n2] = even - twiddle * odd;
        }
    }

    /// <summary>
    /// Rearranges <paramref name="span"/> so that even-indexed elements occupy the first half
    /// and odd-indexed elements occupy the second half, using <paramref name="buffer"/> (at least
    /// half of <paramref name="span"/>'s length) as scratch space instead of allocating.
    /// </summary>
    private static void Separate(Span<Complex> span, Span<Complex> buffer)
    {
        int n2 = span.Length >> 1;
        for (int i = 0; i < n2; i++)
            buffer[i] = span[(i << 1) | 1];
        for (int i = 0; i < n2; i++)
            span[i] = span[i << 1];
        for (int i = 0; i < n2; i++)
            span[i + n2] = buffer[i];
    }
}
