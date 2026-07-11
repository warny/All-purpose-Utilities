using System.Numerics;

namespace Utils.Mathematics.Fourier;

/// <summary>
/// Provides standard spectral windowing functions for FFT preprocessing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coefficient range:</b> not every window is bounded to [0, 1]. Hann, Hamming, and Blackman are
/// (modulo floating-point rounding noise of at most a few ulps right at the edges, where the ideal
/// mathematical value is exactly zero) non-negative and never exceed 1. <see cref="FlatTop{T}"/>,
/// however, is defined by IEC 61672 with coefficients that are negative by design near its edges;
/// callers must not assume a shared [0, 1] range across every window in this class.
/// </para>
/// <para>
/// <b>Precision:</b> every window is computed using <see cref="double"/>-precision trigonometry
/// (<see cref="Math.Cos(double)"/>) regardless of the requested output type <c>T</c>, then converted
/// with <c>T.CreateChecked</c>. This is a deliberate choice, not an accidental precision leak: it
/// gives every output type (including <see cref="decimal"/>, which has no
/// <see cref="ITrigonometricFunctions{TSelf}"/> implementation and could not use a generic-precision
/// cosine anyway) the same double-precision-accurate standard window coefficients, rather than
/// constraining <c>T</c> to <see cref="ITrigonometricFunctions{TSelf}"/> and losing accuracy (and
/// decimal support) for low-precision output types whose own trigonometric implementation would be
/// no more accurate than double's in the first place.
/// </para>
/// </remarks>
public static class FourierWindow
{
    private const double TwoPi = 2.0 * Math.PI;
    private const double FourPi = 4.0 * Math.PI;
    private const double SixPi = 6.0 * Math.PI;

    /// <summary>
    /// Computes the Hann (Hanning) window: w[n] = 0.5 · (1 − cos(2πn/(N−1))).
    /// Good general-purpose window with −18 dB/octave roll-off.
    /// Coefficients lie in [0, 1] (touching 0 at both edges), modulo floating-point rounding noise.
    /// </summary>
    /// <typeparam name="T">Floating-point output type.</typeparam>
    /// <param name="size">Number of samples. Must be at least 2.</param>
    /// <returns>Array of <paramref name="size"/> window coefficients.</returns>
    public static T[] Hann<T>(int size) where T : struct, IFloatingPoint<T>
        => Compute<T>(size, n => 0.5 * (1.0 - Math.Cos(TwoPi * n / (size - 1))));

    /// <summary>
    /// Computes the Hamming window: w[n] = 0.54 − 0.46 · cos(2πn/(N−1)).
    /// Reduced side-lobe leakage compared to Hann; does not go to zero at the edges.
    /// Coefficients lie in [0.08, 1].
    /// </summary>
    /// <typeparam name="T">Floating-point output type.</typeparam>
    /// <param name="size">Number of samples. Must be at least 2.</param>
    /// <returns>Array of <paramref name="size"/> window coefficients.</returns>
    public static T[] Hamming<T>(int size) where T : struct, IFloatingPoint<T>
        => Compute<T>(size, n => 0.54 - 0.46 * Math.Cos(TwoPi * n / (size - 1)));

    /// <summary>
    /// Computes the Blackman window: w[n] = 0.42 − 0.5·cos(2πn/(N−1)) + 0.08·cos(4πn/(N−1)).
    /// Very low side-lobes (−58 dB); wider main lobe than Hann.
    /// Coefficients lie in [0, 1] (touching 0 at both edges) in exact arithmetic; floating-point
    /// rounding can produce a coefficient a few ulps below zero right at the edges.
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
    /// Unlike the other windows in this class, coefficients are <b>not</b> confined to [0, 1]: the
    /// flat-top shape is negative by design near its edges (down to roughly −0.2).
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
