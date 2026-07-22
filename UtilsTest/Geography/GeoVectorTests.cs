using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            // Regression test: Equals() and GetHashCode() snap latitude/longitude/bearing to the
            // EqualityStep grid (2^-33 ≈ 1.16e-10 degrees for double), so vectors that only differ
            // by noise smaller than half a step are equal and always hash equally.
            var vector1 = new GeoVector<double>(45.123456, -73.654321, 10.123456);
            var vector2 = new GeoVector<double>(45.12345600001, -73.65432099999, 10.12345600001);  // differ by 1e-11 < half step

            Assert.AreEqual(vector1, vector2);
            Assert.AreEqual(vector1.GetHashCode(), vector2.GetHashCode());
        }

        [TestMethod]
        public void VectorsOnOppositeSidesOfARoundingBoundaryAreNotEqual()
        {
            // Documents the deliberate trade-off of snap-to-grid equality applied to bearing.
            // With EqualityStep = 2^-33 ≈ 1.16e-10 degrees, these two bearings differ by 2e-10 ≈ 1.72
            // steps, so they land in different grid cells.
            var vector1 = new GeoVector<double>(0, 0, 1.0000449999);
            var vector2 = new GeoVector<double>(0, 0, 1.0000450001);

            Assert.AreNotEqual(vector1, vector2);
        }

        [TestMethod]
        public void VectorsWithBearingOnOppositeSidesOfZeroAreEqualWhenClose()
        {
            // Regression test: bearing wraps around at 0°/360°, so a bearing just below 360° and one
            // just above 0° can be nearly the same heading even though their raw values differ by ~360°.
            // SnapCircleIndex normalizes to [0°, 360°) before snapping: 359.99999999999° is 1e-11° below
            // 360° (within half a step ≈ 5.8e-11°), which rounds UP to the perigon index and wraps to 0;
            // 0.00000000001° is 1e-11° above 0°, which rounds DOWN to 0 — both map to index 0 → equal.
            var vector1 = new GeoVector<double>(0, 0, 359.99999999999);
            var vector2 = new GeoVector<double>(0, 0, 0.00000000001);

            Assert.AreEqual(vector1, vector2);
            Assert.AreEqual(vector1.GetHashCode(), vector2.GetHashCode());
        }

        [TestMethod]
        public void IsApproximately_TrueWithinTolerance_FalseBeyondIt()
        {
            var vector1 = new GeoVector<double>(10, 20, 30);
            var vector2 = new GeoVector<double>(10.00001, 20.00001, 30.00001);
            var vector3 = new GeoVector<double>(10.1, 20.1, 30.1);

            Assert.IsTrue(vector1.IsApproximately(vector2, 1e-4));
            Assert.IsFalse(vector1.IsApproximately(vector3, 1e-4));
        }

        [TestMethod]
        public void IsApproximately_HandlesBearingWraparound()
        {
            var vector1 = new GeoVector<double>(0, 0, 359.9999999);
            var vector2 = new GeoVector<double>(0, 0, 0.0000001);

            Assert.IsTrue(vector1.IsApproximately(vector2, 1e-5));
        }

        [TestMethod]
        public void TravelWithZeroAngleReturnsOriginalVector()
        {
            // Regression for Asin clamp: travelling 0° must return the same vector, not NaN.
            var vector = new GeoVector<double>(45, 10, 90);
            var result = vector.Travel(0);

            Assert.IsFalse(double.IsNaN(result.Latitude), "Travel(0) must not produce NaN latitude");
            Assert.AreEqual(vector.Latitude, result.Latitude, 1e-9);
            Assert.AreEqual(vector.Longitude, result.Longitude, 1e-9);
        }

        [TestMethod]
        public void TravelWithSmallAngleDoesNotReturnNaN()
        {
            // A very small angle (near 0) stresses the Asin clamp; if the argument drifts just
            // above 1 due to floating-point rounding, unclamped Asin would return NaN.
            var vector = new GeoVector<double>(89.9999, 0, 0);
            var result = vector.Travel(1e-10);

            Assert.IsFalse(double.IsNaN(result.Latitude), "Travel with tiny angle must not produce NaN");
        }

        [TestMethod]
        public void IntersectionsOfNearlyIdenticalVectorsDoesNotReturnNaN()
        {
            // Before the clamp fix, Acos inputs could drift just outside [-1,1] for nearly
            // identical vectors or near-degenerate great-circle configurations, producing NaN.
            var v1 = new GeoVector<double>(0.0001, 0.0001, 90);
            var v2 = new GeoVector<double>(0, 0, 90);

            // Either returns empty (same great circle) or non-NaN intersections.
            var result = v1.Intersections(v2);
            foreach (var pt in result)
            {
                Assert.IsFalse(double.IsNaN(pt.Latitude), $"Intersection latitude must not be NaN: {pt}");
                Assert.IsFalse(double.IsNaN(pt.Longitude), $"Intersection longitude must not be NaN: {pt}");
            }
        }

        [TestMethod]
        public void RecenterOnSelfReturnsOrigin()
        {
            var vector = new GeoVector<double>(45, 30, 90);
            var recentered = vector.Recenter(vector);

            Assert.AreEqual(new GeoVector<double>(0, 0, 0), recentered);
        }

        [TestMethod]
        public void RecenterMapsOtherPointToNewLatitudeEqualToAngularDistance()
        {
            var reference = new GeoVector<double>(0, 0, 90);
            var other = new GeoVector<double>(0, 10, 90);

            var recentered = reference.Recenter(other);

            Assert.AreEqual(reference.Point.AngleWith(other.Point), recentered.Latitude, 1e-9);
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
