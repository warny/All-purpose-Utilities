using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Geography.Model;

namespace UtilsTest.Geography;

[TestClass]
public class MapPositionTests
{
    [TestMethod]
    public void ConstructorRejectsNullGeoPoint()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new MapPosition<double>(null!, 5));
    }

    [TestMethod]
    public void ConstructorRejectsZeroZoomLevel()
    {
        var point = new GeoPoint<double>(0, 0);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new MapPosition<double>(point, 0));
    }

    [TestMethod]
    public void EqualsReturnsTrueForSameGeoPointAndZoom()
    {
        var position1 = new MapPosition<double>(new GeoPoint<double>(10, 20), 5);
        var position2 = new MapPosition<double>(new GeoPoint<double>(10, 20), 5);

        Assert.AreEqual(position1, position2);
        Assert.AreEqual(position1.GetHashCode(), position2.GetHashCode());
        Assert.IsTrue(position1 == position2);
    }

    [TestMethod]
    public void EqualsReturnsFalseForDifferentZoom()
    {
        var position1 = new MapPosition<double>(new GeoPoint<double>(10, 20), 5);
        var position2 = new MapPosition<double>(new GeoPoint<double>(10, 20), 6);

        Assert.AreNotEqual(position1, position2);
        Assert.IsTrue(position1 != position2);
    }

    [TestMethod]
    public void ToStringContainsGeoPointAndZoomLevel()
    {
        var position = new MapPosition<double>(new GeoPoint<double>(10, 20), 5);
        string text = position.ToString();

        StringAssert.Contains(text, "5");
    }
}
