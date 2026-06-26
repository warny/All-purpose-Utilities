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
}
