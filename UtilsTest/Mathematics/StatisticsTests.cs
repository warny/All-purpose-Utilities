using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class StatisticsTests
{
    private const double Tol = 1e-10;

    [TestMethod]
    public void Mean_SimpleSequence()
        => Assert.AreEqual(3.0, Statistics.Mean<double>([1, 2, 3, 4, 5]), Tol);

    [TestMethod]
    public void Mean_SingleElement_ReturnsThatElement()
        => Assert.AreEqual(7.0, Statistics.Mean<double>([7.0]), Tol);

    [TestMethod]
    public void Mean_EmptySequence_Throws()
        => Assert.ThrowsException<ArgumentException>(() => Statistics.Mean<double>([]));

    [TestMethod]
    public void Variance_KnownSample()
    {
        // [2,4,4,4,5,5,7,9]: mean=5, Σ(xᵢ-x̄)²=32 → sample variance = 32/7
        double v = Statistics.Variance<double>([2, 4, 4, 4, 5, 5, 7, 9]);
        Assert.AreEqual(32.0 / 7.0, v, 1e-9);
    }

    [TestMethod]
    public void Variance_TwoIdenticalElements_IsZero()
        => Assert.AreEqual(0.0, Statistics.Variance<double>([3, 3]), Tol);

    [TestMethod]
    public void Variance_SingleElement_Throws()
        => Assert.ThrowsException<ArgumentException>(() => Statistics.Variance<double>([1.0]));

    [TestMethod]
    public void StdDev_IsSquareRootOfVariance()
    {
        double[] data = [2, 4, 4, 4, 5, 5, 7, 9];
        Assert.AreEqual(Math.Sqrt(32.0 / 7.0), Statistics.StdDev<double>(data), 1e-9);
    }

    [TestMethod]
    public void Covariance_PerfectlyCorrelated_EqualsVariance()
    {
        double[] data = [1, 2, 3, 4, 5];
        double cov = Statistics.Covariance<double>(data, data);
        double var = Statistics.Variance<double>(data);
        Assert.AreEqual(var, cov, Tol);
    }

    [TestMethod]
    public void Covariance_DifferentLengths_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => Statistics.Covariance<double>([1, 2, 3], [1, 2]));

    [TestMethod]
    public void Correlation_PerfectlyCorrelated_IsOne()
    {
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [2, 4, 6, 8, 10];
        Assert.AreEqual(1.0, Statistics.Correlation<double>(x, y), Tol);
    }

    [TestMethod]
    public void Correlation_PerfectlyAntiCorrelated_IsMinusOne()
    {
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [-1, -2, -3, -4, -5];
        Assert.AreEqual(-1.0, Statistics.Correlation<double>(x, y), Tol);
    }

    [TestMethod]
    public void Median_OddLength_ReturnsMiddle()
    {
        double[] data = [3, 1, 4, 1, 5];
        Assert.AreEqual(3.0, Statistics.Median<double>(data), Tol);
    }

    [TestMethod]
    public void Median_EvenLength_ReturnsLowerMiddle()
    {
        // Documented contract: for an even-length sequence, return the LOWER of the two middle
        // values. [1,3,5,7] sorted has middles 3 and 5; the lower one is 3.
        double[] data = [1, 3, 5, 7];
        Assert.AreEqual(3.0, Statistics.Median<double>(data), Tol);
    }

    [TestMethod]
    public void Median_TwoElements_ReturnsLowerMiddle()
    {
        double[] data = [10, 4];
        Assert.AreEqual(4.0, Statistics.Median<double>(data), Tol);
    }

    [TestMethod]
    public void Median_SixElements_ReturnsLowerMiddle()
    {
        double[] data = [9, 1, 5, 3, 7, 11];
        // Sorted: 1,3,5,7,9,11 -> middles are 5 and 7, lower is 5.
        Assert.AreEqual(5.0, Statistics.Median<double>(data), Tol);
    }

    [TestMethod]
    public void Median_EmptySequence_Throws()
        => Assert.ThrowsException<ArgumentException>(() => Statistics.Median<double>([]));
}
