using System;
using System.Numerics;

namespace Utils.Mathematics.Fourier;

/// <summary>
/// Utility extension methods for Fourier-related computations.
/// </summary>
public static class FourierExtensions
{
    /// <summary>
    /// Validates a transform argument shared by every extension method in this class: non-null and
    /// non-empty (see TODO-2026-07-11-pass4.md item #53). An empty transform has no bins to report and
    /// - for <see cref="GetFrequencies"/> specifically - would otherwise compute <c>sampleRate / 0</c>
    /// (an unused but silently-produced infinity) instead of surfacing the invalid input.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    private static void ValidateTransform(Complex[] transform, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(transform, parameterName);
        if (transform.Length == 0)
            throw new ArgumentException("The transform must contain at least one bin.", parameterName);
    }

    /// <summary>
    /// Returns the frequency represented by each bin in the first half of a Fourier transform result.
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <param name="sampleRate">Sampling rate in hertz. Must be finite and positive.</param>
    /// <returns>Array of <c>N/2</c> frequencies in hertz, one per bin.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="sampleRate"/> is not finite or is not positive (previously only
    /// <c>sampleRate &lt;= 0</c> was checked, silently accepting <see langword="NaN"/> - see
    /// TODO-2026-07-11-pass4.md item #53).
    /// </exception>
    public static double[] GetFrequencies(this Complex[] transform, double sampleRate)
    {
        ValidateTransform(transform, nameof(transform));
        if (!double.IsFinite(sampleRate) || sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be finite and positive.");

        int length = transform.Length;
        double[] frequencies = new double[length >> 1];
        double step = sampleRate / length;

        for (int i = 0; i < frequencies.Length; i++)
            frequencies[i] = i * step;

        return frequencies;
    }

    /// <summary>
    /// Returns the amplitude (magnitude) of each bin in the transform result.
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <returns>Array of amplitudes, one per bin.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    public static double[] GetAmplitudes(this Complex[] transform)
    {
        ValidateTransform(transform, nameof(transform));
        double[] amplitudes = new double[transform.Length];
        for (int i = 0; i < transform.Length; i++)
            amplitudes[i] = transform[i].Magnitude;
        return amplitudes;
    }

    /// <summary>
    /// Returns the phase (argument) in radians of each bin in the transform result.
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <returns>Array of phases in radians, one per bin.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    public static double[] GetPhases(this Complex[] transform)
    {
        ValidateTransform(transform, nameof(transform));
        double[] phases = new double[transform.Length];
        for (int i = 0; i < transform.Length; i++)
            phases[i] = transform[i].Phase;
        return phases;
    }
}
