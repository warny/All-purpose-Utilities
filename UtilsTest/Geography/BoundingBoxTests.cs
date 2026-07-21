using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Geography.Model;

namespace UtilsTest.Geography;

[TestClass]
public class BoundingBoxTests
{
    [TestMethod]
    public void ConstructorNormalizesCornersRegardlessOfOrder()
    {
        var box = new BoundingBox<double>(10, 10, -5, -5);

        Assert.AreEqual(-5, box.MinLatitude);
        Assert.AreEqual(-5, box.MinLongitude);
        Assert.AreEqual(10, box.MaxLatitude);
        Assert.AreEqual(10, box.MaxLongitude);
    }

    [TestMethod]
    public void FromStringParsesMinMaxOrder()
    {
        var box = BoundingBox<double>.FromString("-10,-20,10,20");

        Assert.AreEqual(-10, box.MinLatitude);
        Assert.AreEqual(-20, box.MinLongitude);
        Assert.AreEqual(10, box.MaxLatitude);
        Assert.AreEqual(20, box.MaxLongitude);
    }

    [TestMethod]
    public void ContainsReturnsTrueForPointInsideAndFalseOutside()
    {
        var box = new BoundingBox<double>(-10, -10, 10, 10);

        Assert.IsTrue(box.Contains(new GeoPoint<double>(0, 0)));
        Assert.IsTrue(box.Contains(new GeoPoint<double>(10, 10)));
        Assert.IsFalse(box.Contains(new GeoPoint<double>(11, 0)));
        Assert.IsFalse(box.Contains(new GeoPoint<double>(0, 11)));
    }

    [TestMethod]
    public void IntersectsDetectsOverlapAndDisjointBoxes()
    {
        var box1 = new BoundingBox<double>(0, 0, 10, 10);
        var box2 = new BoundingBox<double>(5, 5, 15, 15);
        var box3 = new BoundingBox<double>(20, 20, 30, 30);

        Assert.IsTrue(box1.Intersects(box2));
        Assert.IsFalse(box1.Intersects(box3));
    }

    [TestMethod]
    public void GetCenterpointReturnsMidpoint()
    {
        var box = new BoundingBox<double>(0, 0, 10, 20);
        var center = box.GetCenterpoint();

        Assert.AreEqual(5, center.Latitude, 1e-9);
        Assert.AreEqual(10, center.Longitude, 1e-9);
    }

    [TestMethod]
    public void SpanPropertiesReturnDifferenceBetweenBounds()
    {
        var box = new BoundingBox<double>(-10, -20, 10, 30);

        Assert.AreEqual(20, box.LatitudeSpan, 1e-9);
        Assert.AreEqual(50, box.LongitudeSpan, 1e-9);
    }

    [TestMethod]
    public void EqualsAndHashCodeAreConsistentForIdenticalBoxes()
    {
        var box1 = new BoundingBox<double>(0, 0, 10, 10);
        var box2 = new BoundingBox<double>(10, 10, 0, 0); // reversed corners, same box

        Assert.AreEqual(box1, box2);
        Assert.AreEqual(box1.GetHashCode(), box2.GetHashCode());
        Assert.IsTrue(box1 == box2);
    }

    [TestMethod]
    public void NotEqualOperatorDetectsDifferentBoxes()
    {
        var box1 = new BoundingBox<double>(0, 0, 10, 10);
        var box2 = new BoundingBox<double>(0, 0, 20, 20);

        Assert.IsTrue(box1 != box2);
    }

    [TestMethod]
    public void ToStringContainsAllFourBounds()
    {
        var box = new BoundingBox<double>(1, 2, 3, 4);
        string text = box.ToString();

        StringAssert.Contains(text, "1");
        StringAssert.Contains(text, "2");
        StringAssert.Contains(text, "3");
        StringAssert.Contains(text, "4");
    }

    [TestMethod]
    public void AntimeridianCrossingBoxIsNotSupportedAndConvertsToWideBox()
    {
        // Documents the known limitation: a box from 170°E to 170°W (20° arc across the
        // antimeridian) is silently stored as a 340°-wide box because ValidateBoundingBox
        // uses T.Min/T.Max on the raw longitude values. The test pins the current behavior
        // so any future fix is visible as a test change.
        var box = new BoundingBox<double>(-10, 170, 10, -170);

        // Current (unsupported) behavior: Min/Max ordering gives a 340° box.
        Assert.AreEqual(-170.0, box.MinLongitude, 1e-9);
        Assert.AreEqual(170.0, box.MaxLongitude, 1e-9);
        Assert.AreEqual(340.0, box.LongitudeSpan, 1e-9);
    }
}
