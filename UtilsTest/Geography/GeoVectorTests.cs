using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Utils.Arrays;
using Utils.Geography.Model;
using Utils.Collections;
using Utils.Mathematics;

namespace UtilsTest.Geography
{
    [TestClass]
    public class GeoVectorTests
    {
        (double latitude, double longitude, double direction, string coordinates, string dcoordinates)[] vectors = [
                (0, 0, 10, "0, 0, 10", "0°, 0°, 10"),
                (45, 45, 10, "N45, E45, 10", "N45°, E45°, 10"),
                (-45, 45, 10, "S45, E45, 10", "S45°, E45°, 10"),
                (-45, -45, -350, "S45, W45, 10", "S45°, W45°, 10"),
                (45.5, -45.5, 370, "N45.5, W45.5, 10", "N45°30', W45°30', 10"),
            ];

        [TestMethod]
        public void GeoVector1()
        {

            foreach (var vector in vectors)
            {
                GeoVector<double> geoVector = new(vector.latitude, vector.longitude, vector.direction);
                Assert.AreEqual(vector.latitude, geoVector.Latitude);
                Assert.AreEqual(vector.longitude, geoVector.Longitude);
                Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Bearing);
                Assert.AreEqual(vector.coordinates, geoVector.ToString());
                Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
            }
        }

        [TestMethod]
        public void GeoVector2()
        {
            foreach (var vector in vectors)
            {
                GeoVector<double> geoVector = new(vector.coordinates);
                Assert.AreEqual(vector.latitude, geoVector.Latitude);
                Assert.AreEqual(vector.longitude, geoVector.Longitude);
                Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Bearing);
                Assert.AreEqual(vector.coordinates, geoVector.ToString());
                Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
            }


        }

        [TestMethod]
        public void GeoVector3()
        {
            foreach (var vector in vectors)
            {
                GeoVector<double> geoVector = new(vector.dcoordinates);
                Assert.AreEqual(vector.latitude, geoVector.Latitude);
                Assert.AreEqual(vector.longitude, geoVector.Longitude);
                Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Bearing);
                Assert.AreEqual(vector.coordinates, geoVector.ToString());
                Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
            }
        }

        [TestMethod]
        public void GeoVector4()
        {
            foreach (var vector in vectors)
            {
                GeoVector<double> geoVector = new(new(vector.latitude, vector.longitude), vector.direction);
                Assert.AreEqual(vector.latitude, geoVector.Latitude);
                Assert.AreEqual(vector.longitude, geoVector.Longitude);
                Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Bearing);
                Assert.AreEqual(vector.coordinates, geoVector.ToString());
                Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
            }
        }

        [TestMethod]
        public void GeoVectorIntersectionTest()
        {
            var tests = new (GeoVector<double> v1, GeoVector<double> v2, GeoPoint<double>[] intersections)[] {
                (new (0, 0, 90), new (0, 0, 90), []),
                (new (0, 90, 90), new (0, 0, 90), []),
                (new (0, 90, 90), new (0, 0, 270), []),
                (new (0, 0, 0), new (0, 0, 90), [ new (0, 0), new (0, 180) ]),
                (new (90, 0, 0), new (0, 0, 90), [ new (0, 0), new (0, 180) ]),
                (new (0, 0, 0), new (0, 90, 0), [ new (90, 0), new (-90, 180) ]),
                (new (0, 0, 0), new (0, 90, 0), [ new (90, 0), new (-90, 0) ]),
                (new (45, 0, 90), new (0, 0, 90), [ new (0, 90), new (0, -90) ]),
                (new (45, 0, 90), new (-45, 0, 90), [ new (0, 90), new (0, -90) ]),
                (new (45, 0, 90), new (0, 90, 0), [ new (0, -90), new (0, 90) ]),
                (new (45, 0, 90), new (0, 0, 0), [ new (45, 0), new (-45, 180) ]),
                (new (0, 90, 135), new (0, 0, 0), [ new (-45, 180), new (45, 0) ]),
                (new (0, 0, 135), new (0, -90, 0), [ new (-45, 90), new (45, 270) ]),
                (new (0, 0, 45), new (0, -90, 0), [ new (45, 90), new (-45, 270) ]),
            };

            var geoPointsComparer = EnumerableEqualityComparer<GeoPoint<double>>.Default;

            foreach (var test in tests)
            {
                var intersections = test.v1.Intersections(test.v2);
                bool equals = geoPointsComparer.Equals(test.intersections, intersections);
                if (!equals)
                {
                    string strTarget = test.intersections is null ? "(null)" : "(" + string.Join("), (", (IEnumerable<GeoPoint<double>>)test.intersections) + ")";
                    string strResult = intersections is null ? "(null)" : "(" + string.Join("), (", (IEnumerable<GeoPoint<double>>)intersections) + ")";
                    Assert.Fail("Result [{0}] differs from target [{1}]", strResult, strTarget);
                }
            }
        }

        [TestMethod]
        public void EqualVectorsProduceEqualHashCodes()
        {
            // Regression test: Equals() and GetHashCode() both round latitude/longitude/bearing to the
            // same precision (5 decimal places) before comparing/hashing, so vectors that only differ in
            // noise beyond that precision are equal and always hash equally.
            var vector1 = new GeoVector<double>(45.123456, -73.654321, 10.123456);
            var vector2 = new GeoVector<double>(45.1234560001, -73.6543209999, 10.1234560001);

            Assert.AreEqual(vector1, vector2);
            Assert.AreEqual(vector1.GetHashCode(), vector2.GetHashCode());
        }

        [TestMethod]
        public void VectorsOnOppositeSidesOfARoundingBoundaryAreNotEqual()
        {
            // Documents the same deliberate trade-off as GeoPointTests.
            // PointsOnOppositeSidesOfARoundingBoundaryAreNotEqual, applied to bearing.
            var vector1 = new GeoVector<double>(0, 0, 1.0000449999);
            var vector2 = new GeoVector<double>(0, 0, 1.0000450001);

            Assert.AreNotEqual(vector1, vector2);
        }

        [TestMethod]
        public void VectorsWithBearingOnOppositeSidesOfZeroAreEqualWhenClose()
        {
            // Regression test: bearing wraps around at 0/360 deg, so a bearing just below 360 and a
            // bearing just above 0 can be almost the same heading even though their raw numeric values
            // are ~360 deg apart. Equals/GetHashCode must normalize bearing (via
            // IAngleCalculator<T>.AreEqualRounded/NormalizeRounded) before comparing, not just round the
            // raw value, otherwise these would incorrectly compare as different.
            var vector1 = new GeoVector<double>(0, 0, 359.999998);
            var vector2 = new GeoVector<double>(0, 0, 0.000002);

            Assert.AreEqual(vector1, vector2);
            Assert.AreEqual(vector1.GetHashCode(), vector2.GetHashCode());
        }

        [TestMethod]
        public void RecenterOnSelfReturnsOrigin()
        {
            var vector = new GeoVector<double>(45, 30, 90);
            var recentered = vector.Recenter(vector);

            Assert.AreEqual(new GeoVector<double>(0, 0, 0), recentered);
        }

        [TestMethod]
        public void RecenterOnNullThrows()
        {
            var vector = new GeoVector<double>(45, 30, 90);
            Assert.ThrowsException<ArgumentNullException>(() => vector.Recenter(null!));
        }

        [TestMethod]
        public void RecenterMapsOtherPointToNewLatitudeEqualToAngularDistance()
        {
            var reference = new GeoVector<double>(0, 0, 90);
            var other = new GeoVector<double>(0, 10, 90);

            var recentered = reference.Recenter(other);

            Assert.AreEqual(reference.AngleWith(other), recentered.Latitude, 1e-9);
        }

        [TestMethod]
        public void StringConstructorParsesSameResultAsNumericConstructor()
        {
            // Regression test: the string constructor used to parse the input string twice; make
            // sure the parsed latitude/longitude/bearing still match the numeric constructor.
            var fromString = new GeoVector<double>("N45.5, W45.5, 370", CultureInfo.InvariantCulture);
            var fromNumbers = new GeoVector<double>(45.5, -45.5, 370);

            Assert.AreEqual(fromNumbers, fromString);
        }
    }
}
