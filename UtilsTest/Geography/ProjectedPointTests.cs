using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Geography.Model;
using Utils.Geography.Projections;

namespace UtilsTest.Geography;

[TestClass]
public class ProjectedPointTests
{
    [TestMethod]
    public void ConstructorRejectsNullProjection()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new ProjectedPoint<double>(1, 2, null!));
    }

    [TestMethod]
    public void DeconstructTwoValuesReturnsXAndY()
    {
        var point = new ProjectedPoint<double>(1, 2, Projections<double>.Equirectangular);
        var (x, y) = point;

        Assert.AreEqual(1, x);
        Assert.AreEqual(2, y);
    }

    [TestMethod]
    public void DeconstructThreeValuesReturnsXYAndProjection()
    {
        var projection = Projections<double>.Equirectangular;
        var point = new ProjectedPoint<double>(1, 2, projection);
        var (x, y, p) = point;

        Assert.AreEqual(1, x);
        Assert.AreEqual(2, y);
        Assert.AreSame(projection, p);
    }

    [TestMethod]
    public void EqualsReturnsTrueForSameCoordinatesAndProjection()
    {
        var projection = Projections<double>.Equirectangular;
        var point1 = new ProjectedPoint<double>(1, 2, projection);
        var point2 = new ProjectedPoint<double>(1, 2, projection);

        Assert.AreEqual(point1, point2);
        Assert.AreEqual(point1.GetHashCode(), point2.GetHashCode());
    }

    [TestMethod]
    public void EqualsReturnsFalseForDifferentProjection()
    {
        var point1 = new ProjectedPoint<double>(1, 2, Projections<double>.Equirectangular);
        var point2 = new ProjectedPoint<double>(1, 2, Projections<double>.Mercator);

        Assert.AreNotEqual(point1, point2);
    }

    [TestMethod]
    public void ToStringWithFormatUsesGivenNumericFormat()
    {
        var point = new ProjectedPoint<double>(1.23456, 2.34567, Projections<double>.Equirectangular);
        string text = point.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        StringAssert.Contains(text, "1.23");
        StringAssert.Contains(text, "2.35");
    }
}
