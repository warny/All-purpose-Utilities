using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra;

[TestClass]
public class AffineSubspaceTests
{
    private const double Delta = 1e-9;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Vector<double> V(params double[] c) => new(c);

    // -------------------------------------------------------------------------
    // FromNormals — construction and basic properties
    // -------------------------------------------------------------------------

    [TestMethod]
    public void FromNormals_OneNormalIn3D_DimensionIsTwo()
    {
        var s = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.AreEqual(2, s.Dimension);
        Assert.AreEqual(3, s.AmbientDimension);
        Assert.AreEqual(1, s.Codimension);
    }

    [TestMethod]
    public void FromNormals_TwoNormalsIn3D_DimensionIsOne()
    {
        var s = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 1, 0), V(0, 0, 1));
        Assert.AreEqual(1, s.Dimension);
    }

    [TestMethod]
    public void FromNormals_AllNormalsSpanSpace_DimensionIsZero()
    {
        // Three independent normals in R³ collapse the subspace to a single point.
        var s = AffineSubspace<double>.FromNormals(V(1, 2, 3), V(1, 0, 0), V(0, 1, 0), V(0, 0, 1));
        Assert.AreEqual(0, s.Dimension);
    }

    [TestMethod]
    public void FromNormals_LinearlyDependentNormals_RankReduced()
    {
        // Passing the same normal twice should not increase codimension.
        var s = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1), V(0, 0, 2));
        Assert.AreEqual(2, s.Dimension);
    }

    [TestMethod]
    public void FromNormals_NoNormals_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => AffineSubspace<double>.FromNormals(V(0, 0, 0)));

    [TestMethod]
    public void FromNormals_WrongDimension_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => AffineSubspace<double>.FromNormals(V(0, 0, 0), V(1, 0)));

    // -------------------------------------------------------------------------
    // FromSpan — construction and basic properties
    // -------------------------------------------------------------------------

    [TestMethod]
    public void FromSpan_TwoDirectionsIn3D_DimensionIsTwo()
    {
        var s = AffineSubspace<double>.FromSpan(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        Assert.AreEqual(2, s.Dimension);
    }

    [TestMethod]
    public void FromSpan_LinearlyDependentDirections_RankReduced()
    {
        // Two collinear vectors should yield dimension 1.
        var s = AffineSubspace<double>.FromSpan(V(0, 0, 0), V(1, 0, 0), V(2, 0, 0));
        Assert.AreEqual(1, s.Dimension);
    }

    [TestMethod]
    public void FromSpan_AllZeroDirections_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => AffineSubspace<double>.FromSpan(V(0, 0, 0), V(0, 0, 0)));

    [TestMethod]
    public void FromSpan_NoDirections_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => AffineSubspace<double>.FromSpan(V(0, 0, 0)));

    // -------------------------------------------------------------------------
    // DistanceTo
    // -------------------------------------------------------------------------

    [TestMethod]
    public void DistanceTo_PointOnXyPlane_ReturnsZero()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.AreEqual(0d, plane.DistanceTo(V(3, -7, 0)), Delta);
    }

    [TestMethod]
    public void DistanceTo_PointAboveXyPlane_ReturnsZCoordinate()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.AreEqual(5d, plane.DistanceTo(V(1, 2, 5)), Delta);
        Assert.AreEqual(3d, plane.DistanceTo(V(0, 0, -3)), Delta);
    }

    [TestMethod]
    public void DistanceTo_OffsetPlane_ReturnsCorrectDistance()
    {
        // Plane z = 4 (anchor at (0,0,4), normal (0,0,1)).
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 4), V(0, 0, 1));
        Assert.AreEqual(1d, plane.DistanceTo(V(99, 99, 5)), Delta);
        Assert.AreEqual(4d, plane.DistanceTo(V(0, 0, 0)), Delta);
    }

    [TestMethod]
    public void DistanceTo_PointOnLineSubspace_ReturnsZero()
    {
        // x-axis as 1D subspace via two normals.
        var line = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 1, 0), V(0, 0, 1));
        Assert.AreEqual(0d, line.DistanceTo(V(17, 0, 0)), Delta);
    }

    [TestMethod]
    public void DistanceTo_WrongDimension_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentException>(() => plane.DistanceTo(V(1, 2)));
    }

    // -------------------------------------------------------------------------
    // Project
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Project_PointAboveXyPlane_DropsZComponent()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var proj = plane.Project(V(1, 2, 7));
        Assert.AreEqual(1d, proj[0], Delta);
        Assert.AreEqual(2d, proj[1], Delta);
        Assert.AreEqual(0d, proj[2], Delta);
    }

    [TestMethod]
    public void Project_PointOnPlane_ReturnsSamePoint()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var p = V(3, -4, 0);
        var proj = plane.Project(p);
        Assert.AreEqual(p[0], proj[0], Delta);
        Assert.AreEqual(p[1], proj[1], Delta);
        Assert.AreEqual(p[2], proj[2], Delta);
    }

    [TestMethod]
    public void Project_OffsetAnchor_ProjectsToCorrectPoint()
    {
        // Plane y = 2.
        var plane = AffineSubspace<double>.FromNormals(V(0, 2, 0), V(0, 1, 0));
        var proj = plane.Project(V(5, 9, 3));
        Assert.AreEqual(5d, proj[0], Delta);
        Assert.AreEqual(2d, proj[1], Delta);
        Assert.AreEqual(3d, proj[2], Delta);
    }

    // -------------------------------------------------------------------------
    // Contains
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Contains_PointOnSubspace_ReturnsTrue()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.IsTrue(plane.Contains(V(3, 4, 0), 1e-9));
    }

    [TestMethod]
    public void Contains_PointOffSubspace_ReturnsFalse()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.IsFalse(plane.Contains(V(0, 0, 0.1), 1e-9));
    }

    // ── Contains tolerance validation (TODO-pass4 item #48) ─────────────────────

    [TestMethod]
    public void Contains_NegativeTolerance_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => plane.Contains(V(3, 4, 0), -1e-9));
    }

    [TestMethod]
    public void Contains_NaNTolerance_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => plane.Contains(V(3, 4, 0), double.NaN));
    }

    /// <summary>
    /// Before this fix, positive infinity silently made every finite-distance point a "member" of the
    /// subspace instead of being rejected like the rest of this library's tolerance parameters.
    /// </summary>
    [TestMethod]
    public void Contains_PositiveInfinityTolerance_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => plane.Contains(V(3, 4, 100), double.PositiveInfinity));
    }

    [TestMethod]
    public void Contains_ZeroToleranceExactMember_ReturnsTrue()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.IsTrue(plane.Contains(V(3, 4, 0), 0d));
    }

    // -------------------------------------------------------------------------
    // IntersectWith(Line<T>)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void IntersectWith_Line_CrossingXyPlane_ReturnsCorrectPoint()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var line = new Line<double>(V(1, 2, 5), V(0, 0, -1));
        var pt = plane.IntersectWith(line);
        Assert.IsNotNull(pt);
        Assert.AreEqual(1d, pt[0], Delta);
        Assert.AreEqual(2d, pt[1], Delta);
        Assert.AreEqual(0d, pt[2], Delta);
    }

    [TestMethod]
    public void IntersectWith_Line_ParallelToPlane_ReturnsNull()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var line = new Line<double>(V(0, 0, 3), V(1, 0, 0));
        Assert.IsNull(plane.IntersectWith(line));
    }

    [TestMethod]
    public void IntersectWith_Line_ObliqueAngle_ReturnsCorrectPoint()
    {
        // Plane z=0, line from (0,0,2) in direction (1,0,-1) hits (2,0,0).
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var line = new Line<double>(V(0, 0, 2), V(1, 0, -1));
        var pt = plane.IntersectWith(line);
        Assert.IsNotNull(pt);
        Assert.AreEqual(2d, pt[0], Delta);
        Assert.AreEqual(0d, pt[1], Delta);
        Assert.AreEqual(0d, pt[2], Delta);
    }

    [TestMethod]
    public void IntersectWith_Line_HighCodimension_ParallelLine_ReturnsNull()
    {
        // x-axis has codimension 2 in R³.  A line through (0,1,0) in direction (0,0,1)
        // is not parallel (denominator > 0) but never reaches the x-axis.
        var xAxis = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 1, 0), V(0, 0, 1));
        var line = new Line<double>(V(0, 1, 0), V(0, 0, 1));
        Assert.IsNull(xAxis.IntersectWith(line));
    }

    [TestMethod]
    public void IntersectWith_Line_WrongDimension_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var line = new Line<double>(V(0, 0), V(1, 0));
        Assert.ThrowsException<ArgumentException>(() => plane.IntersectWith(line));
    }

    // -------------------------------------------------------------------------
    // IntersectWith(AffineSubspace<T>)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void IntersectWith_TwoOrthogonalPlanes_ReturnsLine()
    {
        // z=0 plane and y=0 plane intersect along the x-axis.
        var planeZ = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var planeY = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 1, 0));
        var intersection = planeZ.IntersectWith(planeY);
        Assert.IsNotNull(intersection);
        Assert.AreEqual(1, intersection.Dimension);
        // The intersection axis: any point on it must satisfy y=0 and z=0.
        Assert.AreEqual(0d, intersection.Anchor[1], Delta);
        Assert.AreEqual(0d, intersection.Anchor[2], Delta);
    }

    [TestMethod]
    public void IntersectWith_ParallelPlanes_ReturnsNull()
    {
        var planeZ0 = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var planeZ1 = AffineSubspace<double>.FromNormals(V(0, 0, 1), V(0, 0, 1));
        Assert.IsNull(planeZ0.IntersectWith(planeZ1));
    }

    [TestMethod]
    public void IntersectWith_SamePlane_ReturnsSamePlane()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var result = plane.IntersectWith(plane);
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Dimension);
    }

    [TestMethod]
    public void IntersectWith_PlaneAndCrossingLine_ReturnsPoint()
    {
        // Plane z=0 and the vertical line through (1,2,3) intersect at (1,2,0).
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var vertLine = AffineSubspace<double>.FromSpan(V(1, 2, 3), V(0, 0, 1));
        var result = plane.IntersectWith(vertLine);
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Dimension);
        Assert.AreEqual(1d, result.Anchor[0], Delta);
        Assert.AreEqual(2d, result.Anchor[1], Delta);
        Assert.AreEqual(0d, result.Anchor[2], Delta);
    }

    [TestMethod]
    public void IntersectWith_PlaneAndParallelLine_ReturnsNull()
    {
        // Plane z=0 and a horizontal line at z=1 are parallel (no intersection).
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var horizLine = AffineSubspace<double>.FromSpan(V(0, 0, 1), V(1, 0, 0));
        Assert.IsNull(plane.IntersectWith(horizLine));
    }

    [TestMethod]
    public void IntersectWith_AffineSubspace_WrongDimension_Throws()
    {
        var plane3D = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var line2D = AffineSubspace<double>.FromSpan(V(0, 0), V(1, 0));
        Assert.ThrowsException<ArgumentException>(() => plane3D.IntersectWith(line2D));
    }

    // -------------------------------------------------------------------------
    // ToLine
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ToLine_OneDimensionalSubspace_ReturnsLineWithSameAnchor()
    {
        var sub = AffineSubspace<double>.FromSpan(V(1, 2, 3), V(0, 0, 1));
        var line = sub.ToLine();
        Assert.AreEqual(sub.Anchor, line.Point);
        Assert.AreEqual(0d, line.DistanceTo(V(1, 2, 99)), Delta);
    }

    [TestMethod]
    public void ToLine_TwoDimensionalSubspace_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<InvalidOperationException>(() => plane.ToLine());
    }

    // -------------------------------------------------------------------------
    // Equals
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Equals_SameSubspace_ReturnsTrue()
    {
        var a = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var b = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_SamePlane_DifferentAnchorAndNormal_ReturnsTrue()
    {
        // Both describe the xy-plane, but with different anchors and normal scales.
        var a = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var b = AffineSubspace<double>.FromNormals(V(5, -3, 0), V(0, 0, 2));
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentPlanes_ReturnsFalse()
    {
        var a = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1)); // z=0
        var b = AffineSubspace<double>.FromNormals(V(0, 0, 1), V(0, 0, 1)); // z=1
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_SpanAndNormalEquivalent_ReturnsTrue()
    {
        // xy-plane defined by span vs. by normal.
        var fromSpan = AffineSubspace<double>.FromSpan(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        var fromNormals = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.IsTrue(fromSpan.Equals(fromNormals));
    }

    // ── GetHashCode consistency (TODO-pass4 item #51) ───────────────────────────

    /// <summary>
    /// The required equal-implies-same-hash contract must hold even though <see cref="AffineSubspace{T}.GetHashCode"/>
    /// is deliberately coarser than <see cref="AffineSubspace{T}.Equals(AffineSubspace{T})"/> (see this
    /// type's XML doc on <c>GetHashCode</c>): two geometrically identical subspaces built from different
    /// anchors/normal scales must still hash identically.
    /// </summary>
    [TestMethod]
    public void GetHashCode_EqualSubspacesWithDifferentAnchorAndNormalScale_HaveSameHash()
    {
        var a = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        var b = AffineSubspace<double>.FromNormals(V(5, -3, 0), V(0, 0, 2));
        Assert.IsTrue(a.Equals(b));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    // -------------------------------------------------------------------------
    // Clone
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Clone_ReturnsEqualButDistinctInstance()
    {
        var original = AffineSubspace<double>.FromNormals(V(1, 2, 3), V(0, 0, 1));
        var clone = (AffineSubspace<double>)original.Clone();
        Assert.IsTrue(original.Equals(clone));
        Assert.IsFalse(ReferenceEquals(original, clone));
    }

    // -------------------------------------------------------------------------
    // Null-member validation (TODO-pass4 item #49)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void FromSpan_NullAnchor_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => AffineSubspace<double>.FromSpan(null!, V(1, 0, 0)));

    [TestMethod]
    public void FromSpan_NullDirectionsArray_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => AffineSubspace<double>.FromSpan(V(0, 0, 0), (Vector<double>[])null!));

    [TestMethod]
    public void FromSpan_NullDirectionElement_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => AffineSubspace<double>.FromSpan(V(0, 0, 0), V(1, 0, 0), null!));

    [TestMethod]
    public void FromNormals_NullAnchor_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => AffineSubspace<double>.FromNormals(null!, V(0, 0, 1)));

    [TestMethod]
    public void FromNormals_NullNormalsArray_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => AffineSubspace<double>.FromNormals(V(0, 0, 0), (Vector<double>[])null!));

    [TestMethod]
    public void FromNormals_NullNormalElement_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1), null!));

    [TestMethod]
    public void Project_NullPoint_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentNullException>(() => plane.Project(null!));
    }

    [TestMethod]
    public void DistanceTo_NullPoint_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentNullException>(() => plane.DistanceTo(null!));
    }

    [TestMethod]
    public void Contains_NullPoint_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentNullException>(() => plane.Contains(null!, 1e-9));
    }

    [TestMethod]
    public void IntersectWith_NullLine_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentNullException>(() => plane.IntersectWith((Line<double>)null!));
    }

    [TestMethod]
    public void IntersectWith_NullAffineSubspace_Throws()
    {
        var plane = AffineSubspace<double>.FromNormals(V(0, 0, 0), V(0, 0, 1));
        Assert.ThrowsException<ArgumentNullException>(() => plane.IntersectWith((AffineSubspace<double>)null!));
    }

    // -------------------------------------------------------------------------
    // Explicit rank tolerance (TODO-pass4 item #50)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void FromSpan_ExplicitRankTolerance_StillProducesCorrectSubspace()
    {
        var s = AffineSubspace<double>.FromSpan(V(0, 0, 0), 1e-6, V(1, 0, 0), V(0, 1, 0));
        Assert.AreEqual(2, s.Dimension);
    }

    [TestMethod]
    public void FromSpan_LooseRankTolerance_TreatsNearDependentDirectionAsDependent()
    {
        // A direction that is only slightly off collinear with the first one is treated as dependent
        // when the caller supplies a looser tolerance than the tiny default epsilon.
        var s = AffineSubspace<double>.FromSpan(V(0, 0, 0), 1e-3, V(1, 0, 0), V(1, 1e-6, 0));
        Assert.AreEqual(1, s.Dimension);
    }

    [TestMethod]
    public void FromSpan_NegativeRankTolerance_Throws()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => AffineSubspace<double>.FromSpan(V(0, 0, 0), -1e-9, V(1, 0, 0)));

    [TestMethod]
    public void FromNormals_ExplicitRankTolerance_StillProducesCorrectSubspace()
    {
        var s = AffineSubspace<double>.FromNormals(V(0, 0, 0), 1e-6, V(0, 0, 1));
        Assert.AreEqual(2, s.Dimension);
    }

    [TestMethod]
    public void FromNormals_NegativeRankTolerance_Throws()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => AffineSubspace<double>.FromNormals(V(0, 0, 0), -1e-9, V(0, 0, 1)));

    // -------------------------------------------------------------------------
    // ToString format/provider propagation (TODO-pass4 item #52)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ToString_WithFormat_AppliesFormatToAnchorCoordinates()
    {
        var s = AffineSubspace<double>.FromNormals(V(1.23456, 2.5, 0), V(0, 0, 1));

        string formatted = s.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        StringAssert.Contains(formatted, "1.23");
        StringAssert.Contains(formatted, "2.50");
    }

    [TestMethod]
    public void ToString_WithCulture_UsesCultureSpecificDecimalSeparator()
    {
        var s = AffineSubspace<double>.FromNormals(V(1.5, 0, 0), V(0, 0, 1));
        var culture = new System.Globalization.CultureInfo("fr-FR");

        string formatted = s.ToString("F1", culture);

        StringAssert.Contains(formatted, "1,5");
    }
}
