using System;
using System.Numerics;

namespace Utils.Mathematics.Fourrier;

/// <summary>
/// Utility extension methods for Fourier related computations.
/// </summary>
public static class FourrierExtensions
{
    /// <summary>
    /// Gets the frequency represented by each bin of a Fourier transform result.
    /// </summary>
    /// <param name="transform">The transform result.</param>
    /// <param name="sampleRate">Sampling rate in hertz.</param>
    /// <returns>Array of frequencies in hertz corresponding to each bin.</returns>
    public static double[] GetFrequencies(this Complex[] transform, double sampleRate)
    {
        int length = transform.Length;
        double[] frequencies = new double[length >> 1];
        double step = sampleRate / length;

        for (int i = 0; i < frequencies.Length; i++)
        {
            frequencies[i] = i * step;
        }

        return frequencies;
    }
}
