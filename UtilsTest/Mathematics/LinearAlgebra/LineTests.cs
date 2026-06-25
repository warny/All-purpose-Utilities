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
}
