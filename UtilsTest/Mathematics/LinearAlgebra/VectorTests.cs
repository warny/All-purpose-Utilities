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

    // ── Barycenter zero/non-finite total weight (TODO-pass4 item #44) ─────────

    /// <summary>
    /// All-zero weights sum to an exactly-zero total weight, which cannot normalize the accumulated
    /// (also zero) coordinates; this must be rejected instead of silently dividing 0/0.
    /// </summary>
    [TestMethod]
    public void ComputeBarycenter_AllZeroWeights_Throws()
    {
        var p1 = new Vector<double>(0d, 0d);
        var p2 = new Vector<double>(2d, 0d);
        Assert.ThrowsException<ArgumentException>(() => Vector<double>.ComputeBarycenter((0d, p1), (0d, p2)));
    }

    /// <summary>
    /// Opposite weights that sum to exactly zero must be rejected the same way as all-zero weights,
    /// even though the individual weights are non-zero.
    /// </summary>
    [TestMethod]
    public void ComputeBarycenter_CancellingWeights_Throws()
    {
        var p1 = new Vector<double>(0d, 0d);
        var p2 = new Vector<double>(2d, 0d);
        Assert.ThrowsException<ArgumentException>(() => Vector<double>.ComputeBarycenter((1d, p1), (-1d, p2)));
    }

    /// <summary>
    /// A <see cref="double.NaN"/> weight must be rejected explicitly instead of poisoning the total
    /// weight and every accumulated coordinate with NaN.
    /// </summary>
    [TestMethod]
    public void ComputeBarycenter_NaNWeight_Throws()
    {
        var p1 = new Vector<double>(0d, 0d);
        var p2 = new Vector<double>(2d, 0d);
        Assert.ThrowsException<ArgumentException>(() => Vector<double>.ComputeBarycenter((1d, p1), (double.NaN, p2)));
    }

    /// <summary>
    /// An infinite weight must be rejected explicitly instead of producing an infinite or NaN
    /// barycenter.
    /// </summary>
    [TestMethod]
    public void ComputeBarycenter_InfiniteWeight_Throws()
    {
        var p1 = new Vector<double>(0d, 0d);
        var p2 = new Vector<double>(2d, 0d);
        Assert.ThrowsException<ArgumentException>(() => Vector<double>.ComputeBarycenter((1d, p1), (double.PositiveInfinity, p2)));
    }

    /// <summary>
    /// Negative and positive weights that do not cancel out to zero remain a valid, well-defined
    /// (unequal, "external division") barycenter and must not be rejected by the zero/non-finite guard.
    /// </summary>
    [TestMethod]
    public void ComputeBarycenter_NegativeAndPositiveWeightsSummingNonZero_StillWorks()
    {
        var p1 = new Vector<double>(0d, 0d);
        var p2 = new Vector<double>(2d, 0d);
        var (weight, barycenter) = Vector<double>.ComputeBarycenter((3d, p1), (-1d, p2));
        Assert.AreEqual(2d, weight, 1e-9);
        Assert.AreEqual(-1d, barycenter[0], 1e-9);
        Assert.AreEqual(0d, barycenter[1], 1e-9);
    }

    // ── Barycenter selector/input validation (TODO-pass4 item #45) ────────────

    /// <summary>A <see langword="null"/> weight selector must be rejected explicitly.</summary>
    [TestMethod]
    public void ComputeBarycenter_NullWeightSelector_Throws()
    {
        var p1 = new Vector<double>(0d, 0d);
        Assert.ThrowsException<ArgumentNullException>(
            () => Vector<double>.ComputeBarycenter<(double weight, Vector<double> vector)>(null, wp => wp.vector, [(1d, p1)]));
    }

    /// <summary>A <see langword="null"/> vector selector must be rejected explicitly.</summary>
    [TestMethod]
    public void ComputeBarycenter_NullVectorSelector_Throws()
    {
        var p1 = new Vector<double>(0d, 0d);
        Assert.ThrowsException<ArgumentNullException>(
            () => Vector<double>.ComputeBarycenter<(double weight, Vector<double> vector)>(wp => wp.weight, null, [(1d, p1)]));
    }

    /// <summary>A <see langword="null"/> source enumerable must be rejected explicitly.</summary>
    [TestMethod]
    public void ComputeBarycenter_NullWeightedPoints_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => Vector<double>.ComputeBarycenter<(double weight, Vector<double> vector)>(wp => wp.weight, wp => wp.vector, null));
    }

    /// <summary>
    /// A vector selector that returns <see langword="null"/> for some element must be rejected instead of
    /// failing later with an incidental <see cref="NullReferenceException"/> when the dimension is read.
    /// </summary>
    [TestMethod]
    public void ComputeBarycenter_NullSelectedVector_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => Vector<double>.ComputeBarycenter<double>(w => w, w => null, [1d, 2d]));
    }

    /// <summary>
    /// Selected vectors of mismatched dimension must be rejected with <see cref="ArgumentException"/>,
    /// consistent with other public <see cref="Vector{T}"/> APIs, instead of the previous
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    [TestMethod]
    public void ComputeBarycenter_MismatchedDimensions_ThrowsArgumentException()
    {
        var p1 = new Vector<double>(0d, 0d);
        var p2 = new Vector<double>(1d, 1d, 1d);
        Assert.ThrowsException<ArgumentException>(() => Vector<double>.ComputeBarycenter((1d, p1), (1d, p2)));
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
    public void Normalize_ZeroVector_Throws()
    {
        // Regression: Normalize() used to divide by Norm unconditionally (this / Norm), producing
        // NaN/infinity components for the zero vector instead of the explicit failure ProjectOnto
        // already uses for its own zero-vector case.
        var vector = new Vector<double>(0d, 0d, 0d);
        Assert.ThrowsException<InvalidOperationException>(() => vector.Normalize());
    }

    [TestMethod]
    public void Normalize_NearZeroButNonzeroNorm_ThrowsByDefault()
    {
        // Regression: an earlier fix only rejected an exactly-zero norm. A vector whose norm is
        // technically nonzero but negligible relative to its own components' scale (e.g. every
        // component around 1e-150) must be rejected too, rather than producing a "normalized" result
        // that is only an artifact of floating-point noise.
        var vector = new Vector<double>(1e-150, 1e-150, 1e-150);
        Assert.ThrowsException<InvalidOperationException>(() => vector.Normalize());
    }

    [TestMethod]
    public void Normalize_ExplicitZeroTolerance_AcceptsNearZeroButNonzeroNorm()
    {
        // The explicit tolerance override lets a caller opt back into the old exact-zero-only check.
        var vector = new Vector<double>(1e-150, 1e-150, 1e-150);
        Vector<double> normalized = vector.Normalize(tolerance: 0d);
        Assert.AreEqual(1d, normalized.Norm, 1e-9);
    }

    [TestMethod]
    public void Normalize_InvalidTolerance_Throws()
    {
        var vector = new Vector<double>(1d, 0d, 0d);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => vector.Normalize(tolerance: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => vector.Normalize(tolerance: -1d));
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

    /// <summary>
    /// Item 41: the generalized cross product must remain correct (perpendicular to every input) in
    /// dimensions higher than 3, where the reimplementation via <see cref="Matrix{T}.Determinant"/>
    /// cofactors is exercised beyond the trivial 2x2 minors of the 3D case.
    /// </summary>
    [TestMethod]
    public void CrossProduct_4D_IsPerpendicularToAllThreeInputs()
    {
        var a = new Vector<double>(1d, 0d, 0d, 0d);
        var b = new Vector<double>(0d, 1d, 0d, 0d);
        var c = new Vector<double>(0d, 0d, 1d, 0d);

        var result = Vector<double>.CrossProduct(a, b, c);

        Assert.AreEqual(4, result.Dimension);
        Assert.AreEqual(0d, a * result, 1e-9, "Result should be perpendicular to a");
        Assert.AreEqual(0d, b * result, 1e-9, "Result should be perpendicular to b");
        Assert.AreEqual(0d, c * result, 1e-9, "Result should be perpendicular to c");
        // The three orthonormal basis inputs span the first three axes, so the result must be
        // the fourth standard basis vector, up to sign.
        Assert.AreEqual(1d, Math.Abs(result[3]), 1e-9);
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

    [TestMethod]
    public void FromNormalSpace_ZeroHomogeneousCoordinate_Throws()
    {
        // A zero homogeneous coordinate represents a direction at infinity, not a Cartesian point;
        // the previous implementation divided by it unconditionally, silently producing
        // NaN/infinity components instead of signaling that no Cartesian equivalent exists.
        var h = new Vector<double>(1d, 2d, 3d, 0d);
        Assert.ThrowsException<InvalidOperationException>(() => h.FromNormalSpace());
    }

    [TestMethod]
    public void FromNormalSpace_NearZeroButNonzeroHomogeneousCoordinate_ThrowsByDefault()
    {
        // Regression: an earlier fix only rejected an exactly-zero homogeneous coordinate. A
        // technically-nonzero-but-negligible w (e.g. 1e-300) still produced coordinates of an
        // astronomically large, meaningless magnitude instead of being recognized as a direction at
        // infinity.
        var h = new Vector<double>(1d, 2d, 3d, 1e-300);
        Assert.ThrowsException<InvalidOperationException>(() => h.FromNormalSpace());
    }

    [TestMethod]
    public void FromNormalSpace_ExplicitZeroTolerance_AcceptsNearZeroButNonzeroCoordinate()
    {
        // The explicit tolerance override lets a caller opt back into the old exact-zero-only check.
        var h = new Vector<double>(1d, 2d, 3d, 1e-300);
        Vector<double> v = h.FromNormalSpace(tolerance: 0d);
        Assert.AreEqual(3, v.Dimension);
    }

    [TestMethod]
    public void FromNormalSpace_InvalidTolerance_Throws()
    {
        var h = new Vector<double>(1d, 2d, 3d, 1d);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => h.FromNormalSpace(tolerance: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => h.FromNormalSpace(tolerance: -1d));
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

    [TestMethod]
    public void AngleWith_NullSelf_Throws()
    {
        Vector<double> v1 = null;
        var v2 = new Vector<double>(1d, 0d);
        Assert.ThrowsException<ArgumentNullException>(() => v1.AngleWith(v2));
    }

    [TestMethod]
    public void AngleWith_NullOther_Throws()
    {
        var v1 = new Vector<double>(1d, 0d);
        Vector<double> v2 = null;
        Assert.ThrowsException<ArgumentNullException>(() => v1.AngleWith(v2));
    }

    [TestMethod]
    public void AngleWith_LargeParallelVectors_ReturnsZero()
    {
        // Regression for norm-product overflow (item #70): each norm is 1e200, so
        // self.Norm * other.Norm = 1e400 overflows to infinity; the old cosine division
        // produced NaN and Acos returned NaN. The fix normalizes to (1,0) first, making
        // the cosine exactly 1.0 and the angle exactly 0.
        var v1 = new Vector<double>(1e200, 0d);
        var v2 = new Vector<double>(1e200, 0d);
        Assert.AreEqual(0.0, v1.AngleWith(v2), 1e-9);
    }

    [TestMethod]
    public void AngleWith_NaNComponent_Throws()
    {
        var v1 = new Vector<double>(double.NaN, 0d);
        var v2 = new Vector<double>(1d, 0d);
        Assert.ThrowsException<InvalidOperationException>(() => v1.AngleWith(v2));
    }

    [TestMethod]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        Vector<double> left = null;
        Vector<double> right = null;
        Assert.IsTrue(left == right);
        Assert.IsFalse(left != right);
    }

    [TestMethod]
    public void EqualityOperator_LeftNullRightNonNull_ReturnsFalse()
    {
        Vector<double> left = null;
        var right = new Vector<double>(1d, 2d);
        Assert.IsFalse(left == right);
        Assert.IsTrue(left != right);
    }

    [TestMethod]
    public void EqualityOperator_LeftNonNullRightNull_ReturnsFalse()
    {
        // Regression: the previous implementation returned true here (any non-null vector
        // compared equal to null) because the second `|| vector2 is null` term short-circuited
        // the whole expression to true regardless of vector1.
        var left = new Vector<double>(1d, 2d);
        Vector<double> right = null;
        Assert.IsFalse(left == right);
        Assert.IsTrue(left != right);
    }

    [TestMethod]
    public void EqualityOperator_SameReference_ReturnsTrue()
    {
        var vector = new Vector<double>(1d, 2d);
        Assert.IsTrue(vector == vector);
        Assert.IsFalse(vector != vector);
    }

    [TestMethod]
    public void EqualityOperator_EqualValues_ReturnsTrue()
    {
        var left = new Vector<double>(1d, 2d, 3d);
        var right = new Vector<double>(1d, 2d, 3d);
        Assert.IsTrue(left == right);
        Assert.IsFalse(left != right);
    }

    [TestMethod]
    public void EqualityOperator_UnequalValues_ReturnsFalse()
    {
        var left = new Vector<double>(1d, 2d, 3d);
        var right = new Vector<double>(1d, 2d, 4d);
        Assert.IsFalse(left == right);
        Assert.IsTrue(left != right);
    }

    // ── Norm overflow/underflow stability (item 40) ───────────────────────────

    /// <summary>
    /// Components individually large enough that squaring them directly overflows to
    /// <see cref="double.PositiveInfinity"/> must still produce a finite, correctly-scaled norm.
    /// </summary>
    [TestMethod]
    public void Norm_VeryLargeComponents_DoesNotOverflow()
    {
        const double large = 1e200;
        var vector = new Vector<double>(large, large);

        double squared = large * large;
        Assert.IsTrue(double.IsPositiveInfinity(squared + squared), "Test premise: naive sum-of-squares must overflow.");

        double norm = vector.Norm;
        Assert.IsFalse(double.IsPositiveInfinity(norm), "Norm of large-but-representable components must not overflow.");
        Assert.AreEqual(large * Math.Sqrt(2), norm, large * 1e-9);
    }

    /// <summary>
    /// Components individually small enough that squaring them directly underflows to zero must still
    /// produce a correctly-scaled, nonzero norm.
    /// </summary>
    [TestMethod]
    public void Norm_VerySmallComponents_DoesNotUnderflow()
    {
        const double small = 1e-200;
        var vector = new Vector<double>(small, small);

        Assert.AreEqual(0.0, small * small, "Test premise: squaring 'small' alone must underflow to zero.");

        double norm = vector.Norm;
        Assert.AreNotEqual(0.0, norm, "Norm of small-but-representable components must not underflow to zero.");
        Assert.AreEqual(small * Math.Sqrt(2), norm, small * 1e-9);
    }

    /// <summary>
    /// The scaled accumulation must still agree with the naive formula for ordinary, well-scaled
    /// components (3-4-5 triangle), regardless of component order.
    /// </summary>
    [TestMethod]
    public void Norm_OrdinaryComponents_MatchesExpectedValue()
    {
        Assert.AreEqual(5.0, new Vector<double>(3d, 4d).Norm, 1e-12);
        Assert.AreEqual(5.0, new Vector<double>(4d, 3d).Norm, 1e-12);
    }

    // ── Raw scalar division operator (TODO-pass4 item #56) ──────────────────────

    /// <summary>
    /// The raw <c>/</c> operator is documented as unchecked IEEE division (see its XML doc): a zero
    /// divisor propagates <see cref="double.PositiveInfinity"/>/<see cref="double.NegativeInfinity"/> per
    /// component instead of throwing. Callers needing a validated division use a higher-level member such
    /// as <see cref="Vector{T}.Normalize"/> instead.
    /// </summary>
    [TestMethod]
    public void DivisionOperator_ByZero_ProducesInfinityInsteadOfThrowing()
    {
        var v = new Vector<double>(1d, -1d, 0d);
        var result = v / 0d;
        Assert.AreEqual(double.PositiveInfinity, result[0]);
        Assert.AreEqual(double.NegativeInfinity, result[1]);
        Assert.IsTrue(double.IsNaN(result[2]));
    }
}

