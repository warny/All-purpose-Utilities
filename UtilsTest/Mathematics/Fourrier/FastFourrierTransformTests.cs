using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.Mathematics.Fourrier;

namespace UtilsTest.Mathematics.Fourrier;

[TestClass]
public class FastFourrierTransformTests
{
    [TestMethod]
    public void ConstantSignalTransform()
    {
        Complex[] samples = [1, 1, 1, 1];
        FastFourrierTransform fft = new();
        fft.Transform(samples);

        Complex[] expected =
        [
            new(3.0, 0.0),
            new(2.0, -1.0),
            new(1.0, 0.0),
            new(2.0, 1.0)
        ];

        for (int index = 0; index < samples.Length; index++)
        {
            Assert.AreEqual(expected[index].Real, samples[index].Real, 1e-6);
            Assert.AreEqual(expected[index].Imaginary, samples[index].Imaginary, 1e-6);
        }
    }

    [TestMethod]
    public void SineWaveTransform()
    {
        int n = 8;
        Complex[] samples = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            samples[i] = Complex.Sin(2 * Math.PI * i / n);
        }

        FastFourrierTransform fft = new();
        fft.Transform(samples);

        Complex[] expected =
        [
            new(2.7071067811865475, 1.0),
            new(-2.5, -0.5000000000000003),
            new(1.9999999999999998, -0.29289321881345254),
            new(-0.4999999999999999, 0.5000000000000004),
            new(1.2928932188134525, 1.0),
            new(-3.5, 0.4999999999999996),
            new(1.9999999999999998, -1.7071067811865475),
            new(-1.5, -0.4999999999999997)
        ];

        for (int index = 0; index < samples.Length; index++)
        {
            Assert.AreEqual(expected[index].Real, samples[index].Real, 1e-6);
            Assert.AreEqual(expected[index].Imaginary, samples[index].Imaginary, 1e-6);
        }
    }

    [TestMethod]
    public void FrequenciesExtraction()
    {
        int n = 8;
        Complex[] samples = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            samples[i] = Complex.Sin(2 * Math.PI * i / n);
        }

        FastFourrierTransform fft = new();
        fft.Transform(samples);

        double[] frequencies = samples.GetFrequencies(8);

        double[] expected = [0,1,2,3];
        CollectionAssert.AreEqual(expected, frequencies);
    }
}
