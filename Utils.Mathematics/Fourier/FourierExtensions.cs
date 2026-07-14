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
    /// Returns the raw spectral magnitude (<see cref="Complex.Magnitude"/>) of each bin in the transform
    /// result - <b>not</b> a physically normalized sinusoidal amplitude (see
    /// TODO-2026-07-11-pass4.md item #55). Depending on the forward transform's own normalization
    /// convention, signal length, one-sided-vs-two-sided selection, and any window's coherent gain, the
    /// value that corresponds to a signal component's actual physical amplitude generally requires
    /// further scaling by the caller (e.g. this library's own forward transform is unnormalized, so a
    /// pure sinusoid's peak bin magnitude equals <c>signal amplitude * N / 2</c>, not the amplitude
    /// itself - see <see cref="GetMagnitudes"/>'s "constant signal" example). <see cref="GetMagnitudes"/>
    /// is an identical, more accurately-named alias; this method is kept for backward compatibility.
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <returns>Array of raw magnitudes, one per bin.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    public static double[] GetAmplitudes(this Complex[] transform) => GetMagnitudes(transform);

    /// <summary>
    /// Returns the raw spectral magnitude (<see cref="Complex.Magnitude"/>) of each bin in the transform
    /// result. The more accurately-named counterpart to <see cref="GetAmplitudes"/> (see
    /// TODO-2026-07-11-pass4.md item #55): "amplitude" suggests a physically normalized sinusoidal
    /// amplitude, but this is the raw, unnormalized FFT bin magnitude - e.g. for this library's
    /// unnormalized forward transform, the FFT of a constant signal of value <c>1</c> over <c>N</c>
    /// samples has a DC-bin magnitude of <c>N</c>, not <c>1</c>.
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <returns>Array of raw magnitudes, one per bin.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    public static double[] GetMagnitudes(this Complex[] transform)
    {
        ValidateTransform(transform, nameof(transform));
        double[] magnitudes = new double[transform.Length];
        for (int i = 0; i < transform.Length; i++)
            magnitudes[i] = transform[i].Magnitude;
        return magnitudes;
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

    // -------------------------------------------------------------------------
    // One-sided / all-bin spectrum APIs (TODO-2026-07-11-pass4.md item #54)
    //
    // GetFrequencies returns N/2 bins (no Nyquist) while GetAmplitudes/GetPhases return all N bins,
    // an array-length mismatch that made it easy to zip them under incompatible conventions or
    // unknowingly discard the Nyquist component. The methods below give every convention its own,
    // explicitly-named, length-matched API instead of silently guessing what a caller wants; the three
    // original methods above are unchanged (kept for backward compatibility).
    // -------------------------------------------------------------------------

    /// <summary>
    /// Number of one-sided (non-negative frequency) bins for a transform of the given length: every
    /// index from DC up to - but not including - the Nyquist bin, plus the Nyquist bin itself when
    /// <paramref name="includeNyquist"/> is requested and actually applicable.
    /// </summary>
    /// <param name="length">Transform length (<c>N</c>).</param>
    /// <param name="includeNyquist">
    /// Whether to include the Nyquist bin (index <c>N/2</c>). Only meaningful for an even
    /// <paramref name="length"/>: an odd-length transform has no exact Nyquist bin (its highest bin is
    /// already a genuine positive frequency, not a fold-over point), so this parameter has no effect in
    /// that case.
    /// </param>
    /// <returns>
    /// <c>(N + 1) / 2</c> bins by default (<c>N/2</c> for even <c>N</c>, excluding Nyquist; <c>(N+1)/2</c>
    /// for odd <c>N</c>, which has no separate Nyquist bin to exclude), or one more when
    /// <paramref name="includeNyquist"/> is <see langword="true"/> and <paramref name="length"/> is even.
    /// </returns>
    private static int GetOneSidedBinCount(int length, bool includeNyquist)
    {
        bool hasSeparateNyquistBin = length % 2 == 0;
        int countExcludingNyquist = (length + 1) / 2;
        return countExcludingNyquist + (includeNyquist && hasSeparateNyquistBin ? 1 : 0);
    }

    /// <summary>
    /// Returns the frequency represented by every bin of a Fourier transform result, including the
    /// negative (aliased) frequencies of the upper half - matching <see cref="GetAmplitudes"/>/
    /// <see cref="GetPhases"/>'s bin count exactly, unlike <see cref="GetFrequencies"/> (see
    /// TODO-2026-07-11-pass4.md item #54).
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <param name="sampleRate">Sampling rate in hertz. Must be finite and positive.</param>
    /// <returns>
    /// Array of <c>N</c> frequencies in hertz: bin <c>i &lt; ceil(N/2)</c> is the positive frequency
    /// <c>i * sampleRate / N</c>; bin <c>i &gt;= ceil(N/2)</c> is the negative (aliased) frequency
    /// <c>(i - N) * sampleRate / N</c> - the same convention as e.g. NumPy's <c>fftfreq</c>, under which
    /// an even-length transform's Nyquist bin is reported as the negative Nyquist frequency.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sampleRate"/> is not finite or is not positive.</exception>
    public static double[] GetAllBinFrequencies(this Complex[] transform, double sampleRate)
    {
        ValidateTransform(transform, nameof(transform));
        if (!double.IsFinite(sampleRate) || sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be finite and positive.");

        int length = transform.Length;
        double step = sampleRate / length;
        int positiveCount = (length + 1) / 2;

        double[] frequencies = new double[length];
        for (int i = 0; i < length; i++)
        {
            int k = i < positiveCount ? i : i - length;
            frequencies[i] = k * step;
        }
        return frequencies;
    }

    /// <summary>
    /// Returns the frequency represented by each one-sided (non-negative frequency) bin, with an
    /// explicit, caller-controlled choice of whether the Nyquist bin is included (see
    /// TODO-2026-07-11-pass4.md item #54).
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <param name="sampleRate">Sampling rate in hertz. Must be finite and positive.</param>
    /// <param name="includeNyquist">See <see cref="GetOneSidedBinCount"/>.</param>
    /// <returns>Array of one-sided frequencies in hertz; see <see cref="GetOneSidedBinCount"/> for the exact count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sampleRate"/> is not finite or is not positive.</exception>
    public static double[] GetOneSidedFrequencies(this Complex[] transform, double sampleRate, bool includeNyquist = false)
    {
        ValidateTransform(transform, nameof(transform));
        if (!double.IsFinite(sampleRate) || sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be finite and positive.");

        int length = transform.Length;
        double step = sampleRate / length;
        int count = GetOneSidedBinCount(length, includeNyquist);

        double[] frequencies = new double[count];
        for (int i = 0; i < count; i++)
            frequencies[i] = i * step;
        return frequencies;
    }

    /// <summary>
    /// Returns the raw spectral magnitude (see <see cref="GetMagnitudes"/> - <b>not</b> a physically
    /// normalized sinusoidal amplitude) of each one-sided (non-negative frequency) bin, length-matched
    /// with <see cref="GetOneSidedFrequencies"/> for the same <paramref name="includeNyquist"/> choice
    /// (see TODO-2026-07-11-pass4.md items #54/#55). <see cref="GetOneSidedMagnitudes"/> is an
    /// identical, more accurately-named alias; this method is kept for backward compatibility.
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <param name="includeNyquist">See <see cref="GetOneSidedBinCount"/>.</param>
    /// <returns>Array of one-sided raw magnitudes; see <see cref="GetOneSidedBinCount"/> for the exact count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    public static double[] GetOneSidedAmplitudes(this Complex[] transform, bool includeNyquist = false)
        => GetOneSidedMagnitudes(transform, includeNyquist);

    /// <summary>
    /// Returns the raw spectral magnitude (<see cref="Complex.Magnitude"/>) of each one-sided
    /// (non-negative frequency) bin, length-matched with <see cref="GetOneSidedFrequencies"/> for the
    /// same <paramref name="includeNyquist"/> choice. The more accurately-named counterpart to
    /// <see cref="GetOneSidedAmplitudes"/>, mirroring the <see cref="GetMagnitudes"/>/<see cref="GetAmplitudes"/>
    /// relationship: "amplitude" suggests a physically normalized sinusoidal amplitude, but this is the
    /// raw, unnormalized FFT bin magnitude (see TODO-2026-07-11-pass4.md items #54/#55).
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <param name="includeNyquist">See <see cref="GetOneSidedBinCount"/>.</param>
    /// <returns>Array of one-sided raw magnitudes; see <see cref="GetOneSidedBinCount"/> for the exact count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    public static double[] GetOneSidedMagnitudes(this Complex[] transform, bool includeNyquist = false)
    {
        ValidateTransform(transform, nameof(transform));
        int count = GetOneSidedBinCount(transform.Length, includeNyquist);
        double[] magnitudes = new double[count];
        for (int i = 0; i < count; i++)
            magnitudes[i] = transform[i].Magnitude;
        return magnitudes;
    }

    /// <summary>
    /// Returns the phase (argument) in radians of each one-sided (non-negative frequency) bin,
    /// length-matched with <see cref="GetOneSidedFrequencies"/> for the same
    /// <paramref name="includeNyquist"/> choice (see TODO-2026-07-11-pass4.md item #54).
    /// </summary>
    /// <param name="transform">The transform result. Must be non-null and non-empty.</param>
    /// <param name="includeNyquist">See <see cref="GetOneSidedBinCount"/>.</param>
    /// <returns>Array of one-sided phases in radians; see <see cref="GetOneSidedBinCount"/> for the exact count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="transform"/> is empty.</exception>
    public static double[] GetOneSidedPhases(this Complex[] transform, bool includeNyquist = false)
    {
        ValidateTransform(transform, nameof(transform));
        int count = GetOneSidedBinCount(transform.Length, includeNyquist);
        double[] phases = new double[count];
        for (int i = 0; i < count; i++)
            phases[i] = transform[i].Phase;
        return phases;
    }
}
