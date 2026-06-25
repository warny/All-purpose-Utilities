using System;
using System.Numerics;

namespace Utils.Mathematics.Fourrier;

/// <summary>
/// Utility extension methods for Fourier-related computations.
/// </summary>
public static class FourrierExtensions
{
    /// <summary>
    /// Returns the frequency represented by each bin in the first half of a Fourier transform result.
    /// </summary>
    /// <param name="transform">The transform result.</param>
    /// <param name="sampleRate">Sampling rate in hertz. Must be positive.</param>
    /// <returns>Array of <c>N/2</c> frequencies in hertz, one per bin.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="sampleRate"/> is not positive.
    /// </exception>
    public static double[] GetFrequencies(this Complex[] transform, double sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

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
    /// <param name="transform">The transform result.</param>
    /// <returns>Array of amplitudes, one per bin.</returns>
    public static double[] GetAmplitudes(this Complex[] transform)
    {
        double[] amplitudes = new double[transform.Length];
        for (int i = 0; i < transform.Length; i++)
            amplitudes[i] = transform[i].Magnitude;
        return amplitudes;
    }

    /// <summary>
    /// Returns the phase (argument) in radians of each bin in the transform result.
    /// </summary>
    /// <param name="transform">The transform result.</param>
    /// <returns>Array of phases in radians, one per bin.</returns>
    public static double[] GetPhases(this Complex[] transform)
    {
        double[] phases = new double[transform.Length];
        for (int i = 0; i < transform.Length; i++)
            phases[i] = transform[i].Phase;
        return phases;
    }
}
