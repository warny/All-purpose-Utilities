using System.Numerics;

namespace Utils.Mathematics.Fourier;

/// <summary>
/// Provides standard spectral windowing functions for FFT preprocessing.
/// Each method returns an array of weights in [0, 1].
/// </summary>
public static class FourierWindow
{
    private const double TwoPi = 2.0 * Math.PI;
    private const double FourPi = 4.0 * Math.PI;
    private const double SixPi = 6.0 * Math.PI;

    /// <summary>
    /// Computes the Hann (Hanning) window: w[n] = 0.5 · (1 − cos(2πn/(N−1))).
    /// Good general-purpose window with −18 dB/octave roll-off.
    /// </summary>
    /// <typeparam name="T">Floating-point output type.</typeparam>
    /// <param name="size">Number of samples. Must be at least 2.</param>
    /// <returns>Array of <paramref name="size"/> window coefficients.</returns>
    public static T[] Hann<T>(int size) where T : struct, IFloatingPoint<T>
        => Compute<T>(size, n => 0.5 * (1.0 - Math.Cos(TwoPi * n / (size - 1))));

    /// <summary>
    /// Computes the Hamming window: w[n] = 0.54 − 0.46 · cos(2πn/(N−1)).
    /// Reduced side-lobe leakage compared to Hann; does not go to zero at the edges.
    /// </summary>
    /// <typeparam name="T">Floating-point output type.</typeparam>
    /// <param name="size">Number of samples. Must be at least 2.</param>
    /// <returns>Array of <paramref name="size"/> window coefficients.</returns>
    public static T[] Hamming<T>(int size) where T : struct, IFloatingPoint<T>
        => Compute<T>(size, n => 0.54 - 0.46 * Math.Cos(TwoPi * n / (size - 1)));

    /// <summary>
    /// Computes the Blackman window: w[n] = 0.42 − 0.5·cos(2πn/(N−1)) + 0.08·cos(4πn/(N−1)).
    /// Very low side-lobes (−58 dB); wider main lobe than Hann.
    /// </summary>
    /// <typeparam name="T">Floating-point output type.</typeparam>
    /// <param name="size">Number of samples. Must be at least 2.</param>
    /// <returns>Array of <paramref name="size"/> window coefficients.</returns>
    public static T[] Blackman<T>(int size) where T : struct, IFloatingPoint<T>
        => Compute<T>(size, n =>
            0.42
            - 0.5  * Math.Cos(TwoPi * n / (size - 1))
            + 0.08 * Math.Cos(FourPi * n / (size - 1)));

    /// <summary>
    /// Computes the Flat Top window (IEC 61672 coefficients):
    /// w[n] = 0.21557895 − 0.41663158·cos(2πn/N) + 0.277263158·cos(4πn/N)
    ///        − 0.083578947·cos(6πn/N) + 0.006947368·cos(8πn/N).
    /// Excellent amplitude accuracy; broadest main lobe.
    /// </summary>
    /// <typeparam name="T">Floating-point output type.</typeparam>
    /// <param name="size">Number of samples. Must be at least 2.</param>
    /// <returns>Array of <paramref name="size"/> window coefficients.</returns>
    public static T[] FlatTop<T>(int size) where T : struct, IFloatingPoint<T>
    {
        double n1 = size - 1;
        return Compute<T>(size, n =>
            0.21557895
            - 0.41663158  * Math.Cos(TwoPi * n / n1)
            + 0.277263158 * Math.Cos(FourPi * n / n1)
            - 0.083578947 * Math.Cos(SixPi  * n / n1)
            + 0.006947368 * Math.Cos(8.0 * Math.PI * n / n1));
    }

    private static T[] Compute<T>(int size, Func<double, double> formula)
        where T : struct, IFloatingPoint<T>
    {
        if (size < 2) throw new ArgumentOutOfRangeException(nameof(size), "Window size must be at least 2.");
        T[] window = new T[size];
        for (int n = 0; n < size; n++)
            window[n] = T.CreateChecked(formula(n));
        return window;
    }
}
