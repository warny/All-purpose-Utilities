using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
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
        var (weight, barycenter) = Vector<double>.ComputeBarycenter((1d, p1), (3d, p2));
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

    [TestMethod]
    public void Zero_ReturnsAllZeroComponents()
    {
        var v = Vector<double>.Zero(3);
        Assert.AreEqual(3, v.Dimension);
        Assert.IsTrue(v.All(c => c == 0d));
    }

    [TestMethod]
    public void Unit_ReturnsCorrectBasisVector()
    {
        var v = Vector<double>.Unit(1, 3);
        Assert.AreEqual(0d, v[0], 1e-12);
        Assert.AreEqual(1d, v[1], 1e-12);
        Assert.AreEqual(0d, v[2], 1e-12);
    }

    [TestMethod]
    public void Enumeration_YieldsAllComponents()
    {
        var v = new Vector<double>(1d, 2d, 3d);
        double[] items = v.ToArray();
        CollectionAssert.AreEqual(new[] { 1d, 2d, 3d }, items);
    }

    // ── CrossProduct ─────────────────────────────────────────────────────────

    [TestMethod]
    public void CrossProduct_3D_ReturnsPerpendicularVector()
    {
        var x = new Vector<double>(1d, 0d, 0d);
        var y = new Vector<double>(0d, 1d, 0d);
        var z = Vector<double>.CrossProduct(x, y);

        Assert.AreEqual(3, z.Dimension);
        Assert.AreEqual(0d, z[0], 1e-12);
        Assert.AreEqual(0d, z[1], 1e-12);
        Assert.AreEqual(1d, z[2], 1e-12);
    }

    [TestMethod]
    public void CrossProduct_ResultIsPerpendicularToBothInputs()
    {
        var a = new Vector<double>(1d, 2d, 3d);
        var b = new Vector<double>(4d, 5d, 6d);
        var c = Vector<double>.CrossProduct(a, b);

        Assert.AreEqual(0d, a * c, 1e-10, "Result should be perpendicular to a");
        Assert.AreEqual(0d, b * c, 1e-10, "Result should be perpendicular to b");
    }

    [TestMethod]
    public void CrossProduct_WrongDimension_Throws()
    {
        var v1 = new Vector<double>(1d, 0d, 0d);
        var v2 = new Vector<double>(1d, 0d);
        Assert.ThrowsException<ArgumentException>(() => Vector<double>.CrossProduct(v1, v2));
    }

    // ── ToNormalSpace / FromNormalSpace ────────────────────────────────────

    [TestMethod]
    public void ToNormalSpace_AddsHomogeneousOne()
    {
        var v = new Vector<double>(1d, 2d, 3d);
        var h = v.ToNormalSpace();

        Assert.AreEqual(4, h.Dimension);
        Assert.AreEqual(1d, h[0], 1e-12);
        Assert.AreEqual(2d, h[1], 1e-12);
        Assert.AreEqual(3d, h[2], 1e-12);
        Assert.AreEqual(1d, h[3], 1e-12);
    }

    [TestMethod]
    public void FromNormalSpace_DividesAndDropsLastComponent()
    {
        var h = new Vector<double>(2d, 4d, 6d, 2d); // w=2 → (1,2,3)
        var v = h.FromNormalSpace();

        Assert.AreEqual(3, v.Dimension);
        Assert.AreEqual(1d, v[0], 1e-12);
        Assert.AreEqual(2d, v[1], 1e-12);
        Assert.AreEqual(3d, v[2], 1e-12);
    }

    [TestMethod]
    public void ToNormalSpace_ThenFromNormalSpace_IsIdentity()
    {
        var v = new Vector<double>(3d, -1d, 5d);
        var roundtrip = v.ToNormalSpace().FromNormalSpace();

        Assert.AreEqual(v.Dimension, roundtrip.Dimension);
        for (int i = 0; i < v.Dimension; i++)
            Assert.AreEqual(v[i], roundtrip[i], 1e-12);
    }

    // ── ProjectOnto ───────────────────────────────────────────────────────

    [TestMethod]
    public void ProjectOnto_OntoAxisVector_ReturnsComponent()
    {
        var v = new Vector<double>(3d, 4d);
        var axis = new Vector<double>(1d, 0d);
        var proj = v.ProjectOnto(axis);

        Assert.AreEqual(3d, proj[0], 1e-12);
        Assert.AreEqual(0d, proj[1], 1e-12);
    }

    [TestMethod]
    public void ProjectOnto_ParallelVectors_ReturnsSameDirection()
    {
        var v = new Vector<double>(2d, 4d, 6d);
        var u = new Vector<double>(1d, 2d, 3d);
        var proj = v.ProjectOnto(u);

        // v is already parallel to u, so projection = v
        for (int i = 0; i < v.Dimension; i++)
            Assert.AreEqual(v[i], proj[i], 1e-10);
    }

    [TestMethod]
    public void ProjectOnto_PerpendicularVectors_ReturnsZero()
    {
        var v = new Vector<double>(0d, 5d);
        var u = new Vector<double>(1d, 0d);
        var proj = v.ProjectOnto(u);

        Assert.AreEqual(0d, proj[0], 1e-12);
        Assert.AreEqual(0d, proj[1], 1e-12);
    }

    [TestMethod]
    public void ProjectOnto_ZeroVector_Throws()
    {
        var v = new Vector<double>(1d, 2d);
        var zero = new Vector<double>(0d, 0d);
        Assert.ThrowsException<InvalidOperationException>(() => v.ProjectOnto(zero));
    }

    [TestMethod]
    public void ProjectOnto_DifferentDimensions_Throws()
    {
        var v = new Vector<double>(1d, 2d, 3d);
        var u = new Vector<double>(1d, 0d);
        Assert.ThrowsException<ArgumentException>(() => v.ProjectOnto(u));
    }

    // ── Equals overload fix (A) ───────────────────────────────────────────

    [TestMethod]
    public void Equals_WithMatchingArray_ReturnsTrue()
    {
        var v = new Vector<double>(1d, 2d, 3d);
        double[] arr = { 1d, 2d, 3d };
        Assert.IsTrue(v.Equals((object)arr));
    }

    // ── AngleWith ─────────────────────────────────────────────────────────

    [TestMethod]
    public void AngleWith_PerpendicularVectors_ReturnsPiOver2()
    {
        var v1 = new Vector<double>(1d, 0d);
        var v2 = new Vector<double>(0d, 1d);
        Assert.AreEqual(Math.PI / 2, v1.AngleWith(v2), 1e-9);
    }

    [TestMethod]
    public void AngleWith_ParallelVectors_ReturnsZero()
    {
        var v1 = new Vector<double>(1d, 2d, 3d);
        var v2 = new Vector<double>(2d, 4d, 6d);
        Assert.AreEqual(0.0, v1.AngleWith(v2), 1e-9);
    }

    [TestMethod]
    public void AngleWith_OppositeVectors_ReturnsPi()
    {
        var v1 = new Vector<double>(1d, 0d, 0d);
        var v2 = new Vector<double>(-1d, 0d, 0d);
        Assert.AreEqual(Math.PI, v1.AngleWith(v2), 1e-9);
    }

    [TestMethod]
    public void AngleWith_Known45Degrees_ReturnsQuarterPi()
    {
        var v1 = new Vector<double>(1d, 0d);
        var v2 = new Vector<double>(1d, 1d);
        Assert.AreEqual(Math.PI / 4, v1.AngleWith(v2), 1e-9);
    }

    [TestMethod]
    public void AngleWith_DifferentDimensions_Throws()
    {
        var v1 = new Vector<double>(1d, 0d);
        var v2 = new Vector<double>(1d, 0d, 0d);
        Assert.ThrowsException<ArgumentException>(() => v1.AngleWith(v2));
    }

    [TestMethod]
    public void AngleWith_ZeroVector_Throws()
    {
        var v1 = new Vector<double>(1d, 0d);
        var v2 = new Vector<double>(0d, 0d);
        Assert.ThrowsException<InvalidOperationException>(() => v1.AngleWith(v2));
    }
}

