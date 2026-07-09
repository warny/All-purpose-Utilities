using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Geography.Model;

namespace UtilsTest.Geography
{
    [TestClass]
    public class GeoPointTests
    {
        readonly (double latitude, double longitude, string coordinates, string dcoordinates)[] points = [
            (0,0, "0, 0", "0°, 0°"),
            (45,45, "N45, E45", "N45°, E45°"),
            (-45,45, "S45, E45", "S45°, E45°"),
            (-45,-45, "S45, W45", "S45°, W45°"),
            (45.5,-45.5, "N45.5, W45.5", "N45°30', W45°30'"),
        ];

        [TestMethod]
        public void GeoPoint1()
        {
            foreach (var point in points)
            {
                GeoPoint<double> geoPoint = new GeoPoint<double>(point.latitude, point.longitude);
                Assert.AreEqual(point.latitude, geoPoint.Latitude);
                Assert.AreEqual(point.longitude, geoPoint.Longitude);
                Assert.AreEqual(point.coordinates, geoPoint.ToString());
                Assert.AreEqual(point.dcoordinates, geoPoint.ToString("d"));
            }

        }

        [TestMethod]
        public void GeoPoint2()
        {
            foreach (var point in points)
            {
                GeoPoint<double> geoPoint = new GeoPoint<double>(point.coordinates);
                Assert.AreEqual(point.latitude, geoPoint.Latitude);
                Assert.AreEqual(point.longitude, geoPoint.Longitude);
                Assert.AreEqual(point.coordinates, geoPoint.ToString());
                Assert.AreEqual(point.dcoordinates, geoPoint.ToString("d"));
            }
        }

        [TestMethod]
        public void GeoPoint3()
        {
            foreach (var point in points)
            {
                GeoPoint<double> geoPoint = new GeoPoint<double>(point.dcoordinates);
                Assert.AreEqual(point.latitude, geoPoint.Latitude);
                Assert.AreEqual(point.longitude, geoPoint.Longitude);
                Assert.AreEqual(point.coordinates, geoPoint.ToString());
                Assert.AreEqual(point.dcoordinates, geoPoint.ToString("d"));
            }
        }

        [TestMethod]
        public void TryParse_ValidCoordinates_ReturnsTrue()
        {
            foreach (var point in points)
            {
                bool ok = GeoPoint<double>.TryParse(point.coordinates, out GeoPoint<double>? result);
                Assert.IsTrue(ok);
                Assert.IsNotNull(result);
                Assert.AreEqual(point.latitude, result.Latitude, 1e-9);
                Assert.AreEqual(point.longitude, result.Longitude, 1e-9);
            }
        }

        [TestMethod]
        public void TryParse_InvalidCoordinates_ReturnsFalse()
        {
            bool ok = GeoPoint<double>.TryParse("not a coordinate", out GeoPoint<double>? result);
            Assert.IsFalse(ok);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void EqualPointsProduceEqualHashCodes()
        {
            // Regression test: Equals() and GetHashCode() both round latitude/longitude to the same
            // precision (5 decimal places) before comparing/hashing, so points that only differ in
            // noise beyond that precision are equal and always hash equally.
            var point1 = new GeoPoint<double>(45.123456, -73.654321);
            var point2 = new GeoPoint<double>(45.1234560001, -73.6543209999);

            Assert.AreEqual(point1, point2);
            Assert.AreEqual(point1.GetHashCode(), point2.GetHashCode());
        }

        [TestMethod]
        public void PointsOnOppositeSidesOfARoundingBoundaryAreNotEqual()
        {
            // Documents a deliberate trade-off: comparing on rounded values (rather than a raw
            // tolerance window) keeps Equals/GetHashCode always consistent, at the cost of treating
            // two very close values as different when they straddle the rounding boundary.
            var point1 = new GeoPoint<double>(1.0000449999, 0);
            var point2 = new GeoPoint<double>(1.0000450001, 0);

            Assert.AreNotEqual(point1, point2);
        }
    }
}
