using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Fourier;

namespace UtilsTest.Mathematics.Fourier;

[TestClass]
public class FourierWindowTests
{
    private const double Tol = 1e-10;

    [TestMethod]
    public void Hann_FirstAndLastSamplesAreZero()
    {
        double[] w = FourierWindow.Hann<double>(16);
        Assert.AreEqual(0.0, w[0], Tol);
        Assert.AreEqual(0.0, w[15], Tol);
    }

    [TestMethod]
    public void Hann_CenterSampleIsOne()
    {
        // For even N, the exact centre is between samples; test symmetry instead
        double[] w = FourierWindow.Hann<double>(5);
        Assert.AreEqual(w[1], w[3], Tol);  // symmetric
        Assert.AreEqual(1.0, w[2], Tol);   // peak at centre
    }

    [TestMethod]
    public void Hamming_ValuesInRange()
    {
        double[] w = FourierWindow.Hamming<double>(64);
        foreach (double v in w) Assert.IsTrue(v >= 0 && v <= 1, $"Out of range: {v}");
    }

    [TestMethod]
    public void Hamming_DoesNotGoToZero()
    {
        double[] w = FourierWindow.Hamming<double>(16);
        // Hamming endpoints are 0.08, not 0
        Assert.IsTrue(w[0] > 0.07 && w[0] < 0.1);
    }

    [TestMethod]
    public void Blackman_FirstAndLastSamplesNearZero()
    {
        double[] w = FourierWindow.Blackman<double>(16);
        Assert.AreEqual(0.0, w[0], 1e-6);
        Assert.AreEqual(0.0, w[15], 1e-6);
    }

    [TestMethod]
    public void FlatTop_ValuesWithinExpectedRange()
    {
        double[] w = FourierWindow.FlatTop<double>(64);
        foreach (double v in w) Assert.IsTrue(v >= -0.1 && v <= 1.01, $"Out of range: {v}");
    }

    [TestMethod]
    public void AllWindows_CorrectLength()
    {
        int n = 32;
        Assert.AreEqual(n, FourierWindow.Hann<double>(n).Length);
        Assert.AreEqual(n, FourierWindow.Hamming<double>(n).Length);
        Assert.AreEqual(n, FourierWindow.Blackman<double>(n).Length);
        Assert.AreEqual(n, FourierWindow.FlatTop<double>(n).Length);
    }

    [TestMethod]
    public void Hann_SizeTooSmall_Throws()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => FourierWindow.Hann<double>(1));

    [TestMethod]
    public void Hann_Float_Works()
    {
        float[] w = FourierWindow.Hann<float>(8);
        Assert.AreEqual(8, w.Length);
        Assert.AreEqual(0.0f, w[0], 1e-6f);
    }
}
