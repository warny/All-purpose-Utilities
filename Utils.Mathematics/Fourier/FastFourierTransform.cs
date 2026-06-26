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
    /// <param name="array">Sample array to transform in place. Its length must be a power of two.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="array"/> length is not a power of two.
    /// </exception>
    public static void Transform(Complex[] array)
    {
        if (!Mathematics.MathEx.IsPowerOfTwo(array.Length))
            throw new ArgumentException("Array length must be a power of two.", nameof(array));
        Transform(array.AsSpan());
    }

    /// <summary>
    /// Performs an in-place inverse FFT on the provided sample array.
    /// </summary>
    /// <param name="array">Frequency-domain array to transform back to the time domain. Its length must be a power of two.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="array"/> length is not a power of two.
    /// </exception>
    public static void InverseTransform(Complex[] array)
    {
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
        int n = span.Length;
        if (n < 2) return;

        int n2 = n >> 1;
        Separate(span);
        Transform(span[..n2]);
        Transform(span[n2..]);

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
    /// and odd-indexed elements occupy the second half.
    /// </summary>
    private static void Separate(Span<Complex> span)
    {
        int n2 = span.Length >> 1;
        Complex[] buffer = new Complex[n2];
        for (int i = 0; i < n2; i++)
            buffer[i] = span[(i << 1) | 1];
        for (int i = 0; i < n2; i++)
            span[i] = span[i << 1];
        for (int i = 0; i < n2; i++)
            span[i + n2] = buffer[i];
    }
}
