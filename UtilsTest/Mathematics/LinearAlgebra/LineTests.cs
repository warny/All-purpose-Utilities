using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra;

[TestClass]
public class LineTests
{
    private const double Delta = 1e-10;

    [TestMethod]
    public void DistanceTo_PointOnLine_ReturnsZero()
    {
        var line = new Line<double>(
            new Vector<double>(0d, 0d, 0d),
            new Vector<double>(1d, 0d, 0d));

        Assert.AreEqual(0d, line.DistanceTo(new Vector<double>(5d, 0d, 0d)), Delta);
        Assert.AreEqual(0d, line.DistanceTo(new Vector<double>(-3d, 0d, 0d)), Delta);
    }

    [TestMethod]
    public void DistanceTo_PointPerpendicularFrom2DLine_ReturnsCorrectDistance()
    {
        // Line along the x-axis; point directly above.
        var line = new Line<double>(
            new Vector<double>(0d, 0d),
            new Vector<double>(1d, 0d));

        Assert.AreEqual(3d, line.DistanceTo(new Vector<double>(0d, 3d)), Delta);
        Assert.AreEqual(3d, line.DistanceTo(new Vector<double>(7d, 3d)), Delta);
    }

    [TestMethod]
    public void DistanceTo_3D_ReturnsCorrectDistance()
    {
        // Line along the z-axis; point at (3,4,0).
        var line = new Line<double>(
            new Vector<double>(0d, 0d, 0d),
            new Vector<double>(0d, 0d, 1d));

        // Distance = sqrt(3²+4²) = 5
        Assert.AreEqual(5d, line.DistanceTo(new Vector<double>(3d, 4d, 10d)), Delta);
    }

    [TestMethod]
    public void DistanceTo_DifferentDimension_Throws()
    {
        var line = new Line<double>(
            new Vector<double>(0d, 0d),
            new Vector<double>(1d, 0d));

        Assert.ThrowsException<ArgumentException>(
            () => line.DistanceTo(new Vector<double>(0d, 0d, 0d)));
    }

    [TestMethod]
    public void Constructor_DifferentDimensions_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1d, 0d, 0d)));
    }

    [TestMethod]
    public void Equals_SamePointAndDirection_ReturnsTrue()
    {
        var a = new Line<double>(new Vector<double>(1d, 2d), new Vector<double>(3d, 4d));
        var b = new Line<double>(new Vector<double>(1d, 2d), new Vector<double>(3d, 4d));
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentPoint_ReturnsFalse()
    {
        var a = new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1d, 0d));
        var b = new Line<double>(new Vector<double>(1d, 0d), new Vector<double>(1d, 0d));
        Assert.IsFalse(a.Equals(b));
    }

    // ── Zero-direction rejection (item 37) ────────────────────────────────────

    [TestMethod]
    public void Constructor_ZeroDirection_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(0d, 0d)));
    }

    [TestMethod]
    public void Constructor_NearZeroDirection_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1e-300, 1e-300)));
    }

    // ── Geometric equality (item 38) ──────────────────────────────────────────

    [TestMethod]
    public void IsGeometricallyEquivalentTo_SameLine_DifferentAnchorAndScale_ReturnsTrue()
    {
        // Same line (the x-axis), described from a different anchor point and with a scaled,
        // opposite-sense direction vector — Equals() would say these differ.
        var a = new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1d, 0d));
        var b = new Line<double>(new Vector<double>(5d, 0d), new Vector<double>(-2d, 0d));

        Assert.IsFalse(a.Equals(b));
        Assert.IsTrue(a.IsGeometricallyEquivalentTo(b, 1e-9));
    }

    [TestMethod]
    public void IsGeometricallyEquivalentTo_ParallelButOffsetLine_ReturnsFalse()
    {
        var a = new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1d, 0d));
        var b = new Line<double>(new Vector<double>(0d, 1d), new Vector<double>(1d, 0d));

        Assert.IsFalse(a.IsGeometricallyEquivalentTo(b, 1e-9));
    }

    [TestMethod]
    public void IsGeometricallyEquivalentTo_NonParallelLine_ReturnsFalse()
    {
        var a = new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1d, 0d));
        var b = new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(0d, 1d));

        Assert.IsFalse(a.IsGeometricallyEquivalentTo(b, 1e-9));
    }

    [TestMethod]
    public void IsGeometricallyEquivalentTo_InvalidTolerance_Throws()
    {
        var a = new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1d, 0d));
        var b = new Line<double>(new Vector<double>(0d, 0d), new Vector<double>(1d, 0d));

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.IsGeometricallyEquivalentTo(b, -1.0));
    }

    // ── ToString format/provider propagation (item 39) ────────────────────────

    [TestMethod]
    public void ToString_WithFormat_AppliesFormatToCoordinates()
    {
        var line = new Line<double>(new Vector<double>(1.23456, 2.5), new Vector<double>(1d, 0d));

        string formatted = line.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        StringAssert.Contains(formatted, "1.23");
        StringAssert.Contains(formatted, "2.50");
        StringAssert.Contains(formatted, "1.00");
        StringAssert.Contains(formatted, "0.00");
    }

    [TestMethod]
    public void ToString_WithCulture_AppliesFormatProviderToCoordinates()
    {
        var line = new Line<double>(new Vector<double>(1.5, 0d), new Vector<double>(1d, 0d));
        var culture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

        string formatted = line.ToString("F1", culture);

        StringAssert.Contains(formatted, "1,5");
    }
}
