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

    /// <summary>
    /// Ensures that vectors copy incoming component arrays to remain immutable.
    /// </summary>
    [TestMethod]
    public void Constructor_CopiesInputComponents()
    {
        double[] source = { 1d, 2d, 3d };
        var vector = new Vector<double>(source);

        source[0] = 10d;

        Assert.AreEqual(1d, vector[0], 1e-12);
        Assert.AreEqual(2d, vector[1], 1e-12);
        Assert.AreEqual(3d, vector[2], 1e-12);
    }

    /// <summary>
    /// Verifies that binary operations do not mutate their operands.
    /// </summary>
    [TestMethod]
    public void Addition_DoesNotMutateOperands()
    {
        var left = new Vector<double>(1d, -2d, 5d);
        var right = new Vector<double>(2d, 4d, -1d);

        _ = left + right;

        Assert.AreEqual(1d, left[0], 1e-12);
        Assert.AreEqual(-2d, left[1], 1e-12);
        Assert.AreEqual(5d, left[2], 1e-12);

        Assert.AreEqual(2d, right[0], 1e-12);
        Assert.AreEqual(4d, right[1], 1e-12);
        Assert.AreEqual(-1d, right[2], 1e-12);
    }

    /// <summary>
    /// Confirms that normalization produces a distinct vector instance without altering the source.
    /// </summary>
    [TestMethod]
    public void Normalize_ReturnsNewVectorWithoutMutatingSource()
    {
        var vector = new Vector<double>(3d, 0d, 0d);

        Vector<double> normalized = vector.Normalize();

        Assert.AreNotSame(vector, normalized);
        Assert.AreEqual(3d, vector[0], 1e-12);
        Assert.AreEqual(0d, vector[1], 1e-12);
        Assert.AreEqual(0d, vector[2], 1e-12);

        Assert.AreEqual(1d, normalized.Norm, 1e-12);
    }
}

