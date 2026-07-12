using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.Mathematics.Fourier;

namespace UtilsTest.Mathematics.Fourier;

[TestClass]
public class FastFourierTransformTests
{
    private const double Delta = 1e-6;

    // ── Core transform ────────────────────────────────────────────────────────

    [TestMethod]
    public void ConstantSignalTransform()
    {
        // FFT of [1,1,1,1]: only bin 0 (DC) is non-zero → [4, 0, 0, 0]
        Complex[] samples = [1, 1, 1, 1];
        FastFourierTransform.Transform(samples);

        Assert.AreEqual(4.0, samples[0].Real,      Delta);
        Assert.AreEqual(0.0, samples[0].Imaginary, Delta);
        for (int k = 1; k < samples.Length; k++)
        {
            Assert.AreEqual(0.0, samples[k].Real,      Delta, $"bin {k} real");
            Assert.AreEqual(0.0, samples[k].Imaginary, Delta, $"bin {k} imag");
        }
    }

    [TestMethod]
    public void SineWaveTransform()
    {
        // 8-sample sine wave at 1 Hz: x[k] = sin(2πk/8)
        // Expected FFT: X[1] = -4j, X[7] = +4j, all others zero
        int n = 8;
        Complex[] samples = new Complex[n];
        for (int i = 0; i < n; i++)
            samples[i] = Math.Sin(2 * Math.PI * i / n);

        FastFourierTransform.Transform(samples);

        Assert.AreEqual(0.0, samples[0].Real,      Delta);
        Assert.AreEqual(0.0, samples[0].Imaginary, Delta);
        Assert.AreEqual(0.0, samples[1].Real,      Delta);
        Assert.AreEqual(-4.0, samples[1].Imaginary, Delta);
        for (int k = 2; k <= 6; k++)
        {
            Assert.AreEqual(0.0, samples[k].Real,      Delta, $"bin {k} real");
            Assert.AreEqual(0.0, samples[k].Imaginary, Delta, $"bin {k} imag");
        }
        Assert.AreEqual(0.0, samples[7].Real,      Delta);
        Assert.AreEqual(4.0, samples[7].Imaginary, Delta);
    }

    [TestMethod]
    public void Transform_NonPowerOfTwo_Throws()
    {
        var samples = new Complex[3];
        Assert.ThrowsException<ArgumentException>(() => FastFourierTransform.Transform(samples));
    }

    [TestMethod]
    public void Transform_Null_ThrowsArgumentNullException()
    {
        // Regression: previously dereferenced array.Length before any guard ran, producing an
        // incidental NullReferenceException instead of a documented ArgumentNullException.
        Assert.ThrowsException<ArgumentNullException>(() => FastFourierTransform.Transform(null!));
    }

    [TestMethod]
    public void Transform_ZeroLength_Throws()
    {
        // Zero is not a power of two under this library's IsPowerOfTwo (0 > 0 is false), so a
        // zero-length array is rejected rather than silently treated as a no-op.
        Assert.ThrowsException<ArgumentException>(() => FastFourierTransform.Transform(System.Array.Empty<Complex>()));
    }

    [TestMethod]
    public void Transform_SingleElement_IsUnchanged()
    {
        // Length 1 is a power of two (2^0); the transform of a single sample is itself.
        Complex[] samples = [Complex.Zero + 3];
        FastFourierTransform.Transform(samples);
        Assert.AreEqual(3.0, samples[0].Real, Delta);
        Assert.AreEqual(0.0, samples[0].Imaginary, Delta);
    }

    [TestMethod]
    public void Transform_16Samples_RoundTripsThroughInverse()
    {
        // Regression coverage for the scratch-buffer reuse fix: exercises more than one recursion
        // level (16 = 2^4) to ensure every level's Separate call gets a correctly-sized slice of the
        // single shared buffer rather than reading/writing past what it's entitled to.
        int n = 16;
        Complex[] original = new Complex[n];
        for (int i = 0; i < n; i++) original[i] = new Complex(i, -i);
        Complex[] samples = (Complex[])original.Clone();

        FastFourierTransform.Transform(samples);
        FastFourierTransform.InverseTransform(samples);

        for (int i = 0; i < n; i++)
        {
            Assert.AreEqual(original[i].Real, samples[i].Real, Delta, $"real[{i}]");
            Assert.AreEqual(original[i].Imaginary, samples[i].Imaginary, Delta, $"imag[{i}]");
        }
    }

    // ── Extensions ────────────────────────────────────────────────────────────

    [TestMethod]
    public void FrequenciesExtraction()
    {
        // 8 samples at 8 Hz → bins 0..3 correspond to 0, 1, 2, 3 Hz
        int n = 8;
        Complex[] samples = new Complex[n];
        for (int i = 0; i < n; i++)
            samples[i] = Math.Sin(2 * Math.PI * i / n);

        FastFourierTransform.Transform(samples);

        double[] frequencies = samples.GetFrequencies(8);
        CollectionAssert.AreEqual(new double[] { 0, 1, 2, 3 }, frequencies);
    }

    [TestMethod]
    public void GetFrequencies_NegativeSampleRate_Throws()
    {
        Complex[] samples = [1, 1, 1, 1];
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => samples.GetFrequencies(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => samples.GetFrequencies(0));
    }

    // ── Null/empty/non-finite validation (TODO-pass4 item #53) ─────────────────

    [TestMethod]
    public void GetFrequencies_NaNSampleRate_Throws()
    {
        // Previously only "sampleRate <= 0" was checked, silently accepting NaN (NaN <= 0 is false).
        Complex[] samples = [1, 1, 1, 1];
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => samples.GetFrequencies(double.NaN));
    }

    [TestMethod]
    public void GetFrequencies_PositiveInfinitySampleRate_Throws()
    {
        Complex[] samples = [1, 1, 1, 1];
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => samples.GetFrequencies(double.PositiveInfinity));
    }

    [TestMethod]
    public void GetFrequencies_NullTransform_Throws()
    {
        Complex[] transform = null!;
        Assert.ThrowsException<ArgumentNullException>(() => transform.GetFrequencies(8));
    }

    [TestMethod]
    public void GetFrequencies_EmptyTransform_Throws()
    {
        // Previously computed sampleRate / 0 (an unused infinity) and silently returned an empty array.
        Complex[] transform = [];
        Assert.ThrowsException<ArgumentException>(() => transform.GetFrequencies(8));
    }

    [TestMethod]
    public void GetAmplitudes_NullTransform_Throws()
    {
        Complex[] transform = null!;
        Assert.ThrowsException<ArgumentNullException>(() => transform.GetAmplitudes());
    }

    [TestMethod]
    public void GetAmplitudes_EmptyTransform_Throws()
    {
        Complex[] transform = [];
        Assert.ThrowsException<ArgumentException>(() => transform.GetAmplitudes());
    }

    [TestMethod]
    public void GetPhases_NullTransform_Throws()
    {
        Complex[] transform = null!;
        Assert.ThrowsException<ArgumentNullException>(() => transform.GetPhases());
    }

    [TestMethod]
    public void GetPhases_EmptyTransform_Throws()
    {
        Complex[] transform = [];
        Assert.ThrowsException<ArgumentException>(() => transform.GetPhases());
    }

    // ── One-sided / all-bin spectrum APIs (TODO-pass4 item #54) ────────────────

    [TestMethod]
    public void GetAllBinFrequencies_EightBinTransform_MatchesFftfreqConvention()
    {
        // N=8, sampleRate=8 → step=1. Positive bins 0..3, negative (aliased) bins -4..-1 for 4..7.
        Complex[] transform = new Complex[8];
        double[] frequencies = transform.GetAllBinFrequencies(8);
        CollectionAssert.AreEqual(new double[] { 0, 1, 2, 3, -4, -3, -2, -1 }, frequencies);
    }

    [TestMethod]
    public void GetAllBinFrequencies_MatchesAmplitudesAndPhasesBinCount()
    {
        Complex[] transform = new Complex[8];
        Assert.AreEqual(transform.GetAmplitudes().Length, transform.GetAllBinFrequencies(8).Length);
        Assert.AreEqual(transform.GetPhases().Length, transform.GetAllBinFrequencies(8).Length);
    }

    [TestMethod]
    public void GetOneSidedFrequencies_EvenLengthDefault_ExcludesNyquistAndMatchesLegacyGetFrequencies()
    {
        Complex[] transform = new Complex[8];
        CollectionAssert.AreEqual(transform.GetFrequencies(8), transform.GetOneSidedFrequencies(8));
    }

    [TestMethod]
    public void GetOneSidedFrequencies_EvenLengthIncludeNyquist_AddsNyquistBin()
    {
        // N=8, sampleRate=8 → Nyquist bin (index 4) is frequency 4.
        Complex[] transform = new Complex[8];
        double[] frequencies = transform.GetOneSidedFrequencies(8, includeNyquist: true);
        CollectionAssert.AreEqual(new double[] { 0, 1, 2, 3, 4 }, frequencies);
    }

    [TestMethod]
    public void GetOneSidedFrequencies_OddLength_IncludeNyquistHasNoEffect()
    {
        // N=7 has no separate Nyquist bin: both calls return the same 4 bins.
        Complex[] transform = new Complex[7];
        double[] withoutNyquist = transform.GetOneSidedFrequencies(7);
        double[] withNyquist = transform.GetOneSidedFrequencies(7, includeNyquist: true);
        CollectionAssert.AreEqual(new double[] { 0, 1, 2, 3 }, withoutNyquist);
        CollectionAssert.AreEqual(withoutNyquist, withNyquist);
    }

    [TestMethod]
    public void GetOneSidedAmplitudesAndPhases_MatchOneSidedFrequenciesBinCount()
    {
        int n = 8;
        Complex[] samples = new Complex[n];
        for (int i = 0; i < n; i++)
            samples[i] = Math.Sin(2 * Math.PI * i / n);
        FastFourierTransform.Transform(samples);

        Assert.AreEqual(samples.GetOneSidedFrequencies(n).Length, samples.GetOneSidedAmplitudes().Length);
        Assert.AreEqual(samples.GetOneSidedFrequencies(n).Length, samples.GetOneSidedPhases().Length);
        Assert.AreEqual(
            samples.GetOneSidedFrequencies(n, includeNyquist: true).Length,
            samples.GetOneSidedAmplitudes(includeNyquist: true).Length);
    }

    [TestMethod]
    public void GetOneSidedAmplitudes_SineWave_PeakAtFrequency1()
    {
        int n = 8;
        Complex[] samples = new Complex[n];
        for (int i = 0; i < n; i++)
            samples[i] = Math.Sin(2 * Math.PI * i / n);
        FastFourierTransform.Transform(samples);

        double[] amplitudes = samples.GetOneSidedAmplitudes();
        Assert.AreEqual(4, amplitudes.Length);
        Assert.AreEqual(4.0, amplitudes[1], Delta);
        Assert.AreEqual(0.0, amplitudes[0], Delta);
    }

    [TestMethod]
    public void GetAllBinFrequencies_NullTransform_Throws()
    {
        Complex[] transform = null!;
        Assert.ThrowsException<ArgumentNullException>(() => transform.GetAllBinFrequencies(8));
    }

    [TestMethod]
    public void GetOneSidedFrequencies_EmptyTransform_Throws()
    {
        Complex[] transform = [];
        Assert.ThrowsException<ArgumentException>(() => transform.GetOneSidedFrequencies(8));
    }

    [TestMethod]
    public void GetAmplitudes_ConstantSignal_DcBinIsN()
    {
        // FFT of [1,1,1,1] → amplitudes = [4, 0, 0, 0]
        Complex[] samples = [1, 1, 1, 1];
        FastFourierTransform.Transform(samples);

        double[] amplitudes = samples.GetAmplitudes();
        Assert.AreEqual(4.0, amplitudes[0], Delta);
        for (int k = 1; k < amplitudes.Length; k++)
            Assert.AreEqual(0.0, amplitudes[k], Delta, $"bin {k}");
    }

    [TestMethod]
    public void GetPhases_ConstantSignal_AllZero()
    {
        Complex[] samples = [1, 1, 1, 1];
        FastFourierTransform.Transform(samples);

        double[] phases = samples.GetPhases();
        // DC bin is 4+0j → phase = 0. Bins 1..3 are 0+0j → phase = 0 by convention.
        foreach (var p in phases)
            Assert.AreEqual(0.0, p, Delta);
    }

    [TestMethod]
    public void GetAmplitudes_SineWave_PeakAtFrequency1()
    {
        int n = 8;
        Complex[] samples = new Complex[n];
        for (int i = 0; i < n; i++)
            samples[i] = Math.Sin(2 * Math.PI * i / n);

        FastFourierTransform.Transform(samples);
        double[] amplitudes = samples.GetAmplitudes();

        Assert.AreEqual(4.0, amplitudes[1], Delta);
        Assert.AreEqual(4.0, amplitudes[7], Delta);
        Assert.AreEqual(0.0, amplitudes[0], Delta);
        for (int k = 2; k <= 6; k++)
            Assert.AreEqual(0.0, amplitudes[k], Delta, $"bin {k}");
    }

    // ── InverseTransform ──────────────────────────────────────────────────────

    [TestMethod]
    public void InverseTransform_AfterTransform_RecoverOriginal()
    {
        // Round-trip: IFFT(FFT(x)) == x
        Complex[] original = [1, 2, 3, 4];
        Complex[] samples = [1, 2, 3, 4];

        FastFourierTransform.Transform(samples);
        FastFourierTransform.InverseTransform(samples);

        for (int i = 0; i < samples.Length; i++)
        {
            Assert.AreEqual(original[i].Real,      samples[i].Real,      Delta, $"real[{i}]");
            Assert.AreEqual(original[i].Imaginary, samples[i].Imaginary, Delta, $"imag[{i}]");
        }
    }

    [TestMethod]
    public void InverseTransform_OfConstantSpectrum_ReturnsImpulse()
    {
        // IFFT of [1, 1, 1, 1] = [1, 0, 0, 0] (impulse at index 0)
        Complex[] spectrum = [1, 1, 1, 1];
        FastFourierTransform.InverseTransform(spectrum);

        Assert.AreEqual(1.0, spectrum[0].Real,      Delta);
        Assert.AreEqual(0.0, spectrum[0].Imaginary, Delta);
        for (int k = 1; k < spectrum.Length; k++)
        {
            Assert.AreEqual(0.0, spectrum[k].Real,      Delta, $"real[{k}]");
            Assert.AreEqual(0.0, spectrum[k].Imaginary, Delta, $"imag[{k}]");
        }
    }

    [TestMethod]
    public void InverseTransform_NonPowerOfTwo_Throws()
    {
        var spectrum = new Complex[3];
        Assert.ThrowsException<ArgumentException>(() => FastFourierTransform.InverseTransform(spectrum));
    }

    [TestMethod]
    public void InverseTransform_Null_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => FastFourierTransform.InverseTransform(null!));
    }

    [TestMethod]
    public void InverseTransform_ZeroLength_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => FastFourierTransform.InverseTransform(System.Array.Empty<Complex>()));
    }
}
