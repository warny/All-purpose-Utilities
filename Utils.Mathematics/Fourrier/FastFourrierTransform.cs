using System.Numerics;

namespace Utils.Mathematics.Fourrier;

/// <summary>
/// Provides a recursive Cooley-Tukey Fast Fourier Transform implementation.
/// </summary>
public class FastFourrierTransform
{
    /// <summary>
    /// Separates even and odd elements so the even values occupy the first half of the array range and the odd values occupy the second half.
    /// </summary>
    /// <param name="array">Array to reorder.</param>
    /// <param name="start">Inclusive start index.</param>
    /// <param name="end">Exclusive end index.</param>
    private void Separate(Complex[] array, int start, int end)
    {
        int n = end - start;
        var n2 = n >> 1;
        Complex[] buffer = new Complex[n2];
        for (int i = 0; i < n2; i++)
        {
            buffer[i] = array[(i << 1) | 1];
        }

        for (int i = 0; i < n2; i++)
        {
            array[i] = array[i << 1];
        }

        for (int i = 0; i < n2; i++)
        {
            array[i + n2] = buffer[i];
        }
    }

    /// <summary>
    /// Performs an in-place FFT on the entire sample array.
    /// </summary>
    /// <param name="array">Array to transform.</param>
    public void Transform(Complex[] array)
    {
        Transform(array, 0, array.Length);
    }

    // N must be a power-of-2, or bad things will happen.
    // Currently no check for this condition.
    //
    // N input samples in X[] are FFT'd and results left in X[].
    // Because of Nyquist theorem, N samples means
    // only first N/2 FFT results in X[] are the answer.
    // (upper half of X[] is a reflection with no new information).
    /// <summary>
    /// Performs an in-place FFT on a range of the provided array.
    /// </summary>
    /// <param name="array">Array to transform.</param>
    /// <param name="start">Inclusive start index.</param>
    /// <param name="end">Exclusive end index.</param>
    public void Transform(Complex[] array, int start, int end)
    {
        int n = end - start;
        if (n < 2)
        {
            return;
        }

        int n2 = n >> 1;

        Separate(array, start, end);
        Transform(array, start, start + n2);
        Transform(array, start + n2, end);

        for (int k = 0; k < n2; k++)
        {
            Complex evenComponent = array[k];
            Complex oddComponent = array[k + n2];
            Complex twiddleFactor = Complex.Exp(new Complex(0, -2 * Math.PI * k / n));
            array[k] = evenComponent + twiddleFactor * oddComponent;
            array[k + n2] = evenComponent - twiddleFactor * oddComponent;
        }
    }
}
