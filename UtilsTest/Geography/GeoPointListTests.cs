using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Geography.Model;

namespace UtilsTest.Geography;

[TestClass]
public class GeoPointListTests
{
    [TestMethod]
    public void BoundingBoxCoversAllPositivePoints()
    {
        var list = new GeoPointList<double>([
            new GeoPoint<double>(10, 20),
            new GeoPoint<double>(30, 5),
            new GeoPoint<double>(15, 40),
        ]);

        var box = list.BoundingBox;

        Assert.AreEqual(10, box.MinLatitude);
        Assert.AreEqual(5, box.MinLongitude);
        Assert.AreEqual(30, box.MaxLatitude);
        Assert.AreEqual(40, box.MaxLongitude);
    }

    [TestMethod]
    public void BoundingBoxIsCorrectWhenAllCoordinatesAreNegative()
    {
        // Regression test: the bounding box used to seed max at 0, which is wrong
        // when every latitude/longitude in the list is negative.
        var list = new GeoPointList<double>([
            new GeoPoint<double>(-30, -40),
            new GeoPoint<double>(-10, -20),
            new GeoPoint<double>(-20, -50),
        ]);

        var box = list.BoundingBox;

        Assert.AreEqual(-30, box.MinLatitude);
        Assert.AreEqual(-50, box.MinLongitude);
        Assert.AreEqual(-10, box.MaxLatitude);
        Assert.AreEqual(-20, box.MaxLongitude);
    }

    [TestMethod]
    public void BoundingBoxOnSinglePointDegeneratesToAPoint()
    {
        var list = new GeoPointList<double>([new GeoPoint<double>(-5, -5)]);
        var box = list.BoundingBox;

        Assert.AreEqual(-5, box.MinLatitude);
        Assert.AreEqual(-5, box.MinLongitude);
        Assert.AreEqual(-5, box.MaxLatitude);
        Assert.AreEqual(-5, box.MaxLongitude);
    }

    [TestMethod]
    public void BoundingBoxOnEmptyListThrows()
    {
        var list = new GeoPointList<double>();
        Assert.ThrowsException<InvalidOperationException>(() => _ = list.BoundingBox);
    }

    [TestMethod]
    public void GeoPointList2BoundingBoxIsCorrectWhenAllCoordinatesAreNegative()
    {
        var list2 = new GeoPointList2<double>([
            new GeoPointList<double>([new GeoPoint<double>(-30, -40), new GeoPoint<double>(-10, -20)]),
            new GeoPointList<double>([new GeoPoint<double>(-25, -60), new GeoPoint<double>(-15, -35)]),
        ]);

        var box = list2.BoundingBox;

        Assert.AreEqual(-30, box.MinLatitude);
        Assert.AreEqual(-60, box.MinLongitude);
        Assert.AreEqual(-10, box.MaxLatitude);
        Assert.AreEqual(-20, box.MaxLongitude);
    }

    [TestMethod]
    public void GeoPointList2BoundingBoxOnEmptyListThrows()
    {
        var list2 = new GeoPointList2<double>();
        Assert.ThrowsException<InvalidOperationException>(() => _ = list2.BoundingBox);
    }

    [TestMethod]
    public void GeoPointList2FromNestedEnumerablesCopiesEachInnerCollection()
    {
        var source = new[]
        {
            new[] { new GeoPoint<double>(0, 0), new GeoPoint<double>(1, 1) },
            new[] { new GeoPoint<double>(2, 2) },
        };

        var list2 = new GeoPointList2<double>(source);

        Assert.AreEqual(2, list2.Count);
        Assert.AreEqual(2, list2[0].Count);
        Assert.AreEqual(1, list2[1].Count);
    }
}
