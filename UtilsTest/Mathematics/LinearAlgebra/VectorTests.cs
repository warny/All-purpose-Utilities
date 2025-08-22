using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra;

/// <summary>
/// Tests for vector operations.
/// </summary>
[TestClass]
public class VectorTests
{
    /// <summary>
    /// Ensures the dot product is computed correctly.
    /// </summary>
    [TestMethod]
    public void DotProduct_ComputesCorrectly()
    {
        var v1 = new Vector<double>(1, 2, 3);
        var v2 = new Vector<double>(4, 5, 6);
        double result = v1 * v2;
        Assert.AreEqual(32d, result, 1e-9);
    }

    /// <summary>
    /// Validates computation of a weighted barycenter.
    /// </summary>
    [TestMethod]
    public void Barycenter_ComputesWeightedAverage()
    {
        var p1 = new Vector<double>(0, 0);
        var p2 = new Vector<double>(2, 0);
        var (weight, barycenter) = p1.ComputeBarycenter((1d, p1), (3d, p2));
        Assert.AreEqual(4d, weight, 1e-9);
        Assert.AreEqual(1.5d, barycenter[0], 1e-9);
        Assert.AreEqual(0d, barycenter[1], 1e-9);
    }
}

