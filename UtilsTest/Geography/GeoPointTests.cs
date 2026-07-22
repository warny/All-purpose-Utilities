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
                bool ok = GeoPoint<double>.TryParse(point.coordinates, out GeoPoint<double> result);
                Assert.IsTrue(ok);
                Assert.AreEqual(point.latitude, result.Latitude, 1e-9);
                Assert.AreEqual(point.longitude, result.Longitude, 1e-9);
            }
        }

        [TestMethod]
        public void TryParse_InvalidCoordinates_ReturnsFalse()
        {
            bool ok = GeoPoint<double>.TryParse("not a coordinate", out GeoPoint<double> result);
            Assert.IsFalse(ok);
            Assert.AreEqual(default, result);
        }

        [TestMethod]
        public void EqualPointsProduceEqualHashCodes()
        {
            // Regression test: Equals() and GetHashCode() both snap latitude/longitude to the same
            // EqualityStep grid (2^-33 ≈ 1.16e-10 degrees for double) before comparing/hashing, so
            // points that only differ by noise smaller than half a step are equal and always hash equally.
            var point1 = new GeoPoint<double>(45.123456, -73.654321);
            var point2 = new GeoPoint<double>(45.12345600001, -73.65432099999);   // differ by 1e-11 < half step

            Assert.AreEqual(point1, point2);
            Assert.AreEqual(point1.GetHashCode(), point2.GetHashCode());
        }

        [TestMethod]
        public void PointsOnOppositeSidesOfARoundingBoundaryAreNotEqual()
        {
            // Documents the deliberate trade-off of snap-to-grid equality: two values that straddle
            // a grid boundary compare as unequal even when they are very close.
            // With EqualityStep = 2^-33 ≈ 1.16e-10 degrees, these two values differ by 2e-10 ≈ 1.72
            // steps, so they land in different grid cells.
            var point1 = new GeoPoint<double>(1.0000449999, 0);
            var point2 = new GeoPoint<double>(1.0000450001, 0);

            Assert.AreNotEqual(point1, point2);
        }

        [TestMethod]
        public void PointsOnOppositeSidesOfTheAntimeridianAreEqualWhenClose()
        {
            // Regression test: longitude wraps around at ±180°, so a longitude just below +180° and
            // one just above -180° can be the same physical point even though their raw numeric values
            // are ~360° apart. SnapCircleIndex normalizes to [0°, 360°) before snapping to the grid,
            // so both sides of the antimeridian map to the same grid index when they are within half
            // a step (≈ 5.8e-11°) of 180°.
            // Here both values are 1e-11° from ±180° → within half a step → same index → equal.
            var point1 = new GeoPoint<double>(10, 179.99999999999);
            var point2 = new GeoPoint<double>(10, -179.99999999999);

            Assert.AreEqual(point1, point2);
            Assert.AreEqual(point1.GetHashCode(), point2.GetHashCode());
        }

        [TestMethod]
        public void IsApproximately_TrueWithinTolerance_FalseBeyondIt()
        {
            var point1 = new GeoPoint<double>(10, 20);
            var point2 = new GeoPoint<double>(10.00001, 20.00001);
            var point3 = new GeoPoint<double>(10.1, 20.1);

            Assert.IsTrue(point1.IsApproximately(point2, 1e-4));
            Assert.IsFalse(point1.IsApproximately(point3, 1e-4));
        }

        [TestMethod]
        public void IsApproximately_DefaultOverload_UsesDefaultTolerance()
        {
            // Default tolerance = EqualityStep ≈ 1.16e-10°. Values 5e-11° apart are well within it.
            var point1 = new GeoPoint<double>(10, 20);
            var point2 = new GeoPoint<double>(10.00000000005, 20.00000000005);

            Assert.IsTrue(point1.IsApproximately(point2));
        }

        [TestMethod]
        public void IsApproximately_HandlesAntimeridianWraparound()
        {
            var point1 = new GeoPoint<double>(10, 179.9999999);
            var point2 = new GeoPoint<double>(10, -179.9999999);

            Assert.IsTrue(point1.IsApproximately(point2, 1e-5));
        }

        [TestMethod]
        public void IsApproximately_ConsidersTwoPointsAtTheSamePoleEqualRegardlessOfLongitude()
        {
            var point1 = new GeoPoint<double>(90, 10);
            var point2 = new GeoPoint<double>(90, -170);

            Assert.IsTrue(point1.IsApproximately(point2, 1e-9));
        }

        [TestMethod]
        public void TryParse_StringWithTrailingGarbage_ReturnsFalse()
        {
            // Before the fix the regex had no anchors: Match() found a valid substring inside
            // garbage input ("48.8566 INVALID" matched the leading "48.8566"), silently accepting
            // malformed input. Anchored regex rejects any input not fully consumed by the pattern.
            bool ok = GeoPoint<double>.TryParse("48.8566 INVALID, 2.3522", out _);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void TryParse_StringWithLeadingGarbage_ReturnsFalse()
        {
            bool ok = GeoPoint<double>.TryParse("GARBAGE 48.8566, 2.3522", out _);
            Assert.IsFalse(ok);
        }

        [DataTestMethod]
        [DataRow("45°30'\"")]
        [DataRow("45°'\"")]
        [DataRow("45°30''")]
        public void TryParse_MalformedDmsSeparators_ReturnsFalse(string value)
        {
            Assert.IsFalse(GeoPoint<double>.TryParse($"{value}, 2", out _));
        }

        [TestMethod]
        public void TryParse_StringWithLeadingAndTrailingWhitespace_ReturnsTrue()
        {
            // Surrounding whitespace is explicitly allowed by \s* in the anchored regex.
            bool ok = GeoPoint<double>.TryParse("  48.8566  ,  2.3522  ", out GeoPoint<double> result);
            Assert.IsTrue(ok);
            Assert.AreEqual(48.8566, result.Latitude, 1e-9);
            Assert.AreEqual(2.3522, result.Longitude, 1e-9);
        }

        [TestMethod]
        public void FormatDms_WithNonZeroSeconds_SecondsAreInRange()
        {
            // Before the fix, seconds were multiplied by SecondsInDegree (3600) instead of
            // SecondsInMinute (60), producing values such as 2649 for 12.3456° (correct: 44).
            // After the fix, seconds must always be in [0, 59].
            double[] testLatitudes = [12.3456, 45.6789, -33.7654, 89.9999, -0.001];
            foreach (double lat in testLatitudes)
            {
                var point = new GeoPoint<double>(lat, 0);
                string formatted = point.ToString("D");
                string latPart = formatted.Split(',')[0].Trim();

                int quoteIdx = latPart.IndexOf('"');
                Assert.IsTrue(quoteIdx >= 0, $"No seconds in output for lat={lat}: {formatted}");
                int apostrIdx = latPart.LastIndexOf('\'', quoteIdx - 1);
                int sec = int.Parse(latPart[(apostrIdx + 1)..quoteIdx]);

                Assert.IsTrue(sec >= 0 && sec < 60,
                    $"Seconds={sec} out of [0,59] for lat={lat}. Full output: {formatted}");
            }
        }

        [TestMethod]
        public void FormatDms_RoundTripWithSeconds_ParsesBackWithinOneSecond()
        {
            // Round-trip through DMS format: parsed value may differ by up to 1 second (1/3600°)
            // because the format truncates (floor) to whole seconds.
            double[] testLatitudes = [12.3456, 45.6789, -33.7654];
            foreach (double lat in testLatitudes)
            {
                var original = new GeoPoint<double>(lat, 0);
                string dms = original.ToString("D");
                var parsed = new GeoPoint<double>(dms);

                Assert.AreEqual(original.Latitude, parsed.Latitude, 1.0 / 3600.0,
                    $"Round-trip latitude mismatch for {lat}. DMS was: {dms}");
            }
        }

        [TestMethod]
        public void AngleWithIdenticalPointsReturnsZeroNotNaN()
        {
            // Before the clamp fix, floating-point rounding could push the Acos argument just
            // outside [-1, 1], returning NaN for identical or nearly identical points.
            var point = new GeoPoint<double>(48.8566, 2.3522);
            double angle = point.AngleWith(point);

            Assert.IsFalse(double.IsNaN(angle), "AngleWith identical point should not return NaN");
            Assert.AreEqual(0.0, angle, 1e-9);
        }

        [TestMethod]
        public void AngleWithNearlyIdenticalPointsDoesNotReturnNaN()
        {
            var point1 = new GeoPoint<double>(48.8566, 2.3522);
            var point2 = new GeoPoint<double>(48.8566 + 1e-12, 2.3522 + 1e-12);

            double angle = point1.AngleWith(point2);

            Assert.IsFalse(double.IsNaN(angle), "AngleWith nearly identical point should not return NaN");
        }

        [TestMethod]
        public void AngleWithAntipodalPointIsOneHundredEightyDegrees()
        {
            var north = new GeoPoint<double>(0, 0);
            var south = new GeoPoint<double>(0, 180);

            double angle = north.AngleWith(south);

            Assert.IsFalse(double.IsNaN(angle), "AngleWith antipodal point should not return NaN");
            Assert.AreEqual(180.0, angle, 1e-6);
        }

        [TestMethod]
        public void NorthPolePointsWithDifferentLongitudesAreEqualAndHaveTheSameHashCode()
        {
            // All longitudes at a pole refer to the same geographic point.
            // Equals must return true and GetHashCode must return the same value.
            var pole1 = new GeoPoint<double>(90, 10);
            var pole2 = new GeoPoint<double>(90, -170);

            Assert.AreEqual(pole1, pole2);
            Assert.AreEqual(pole1.GetHashCode(), pole2.GetHashCode());
        }

        [TestMethod]
        public void SouthPolePointsWithDifferentLongitudesAreEqualAndHaveTheSameHashCode()
        {
            var pole1 = new GeoPoint<double>(-90, 45);
            var pole2 = new GeoPoint<double>(-90, -135);

            Assert.AreEqual(pole1, pole2);
            Assert.AreEqual(pole1.GetHashCode(), pole2.GetHashCode());
        }

        [TestMethod]
        public void PolesCanBeUsedAsHashSetKeysWithDifferentLongitudes()
        {
            // Verify that a HashSet correctly treats all north-pole points as the same key.
            var set = new HashSet<GeoPoint<double>>
            {
                new GeoPoint<double>(90, 0),
                new GeoPoint<double>(90, 90),
                new GeoPoint<double>(90, -180),
            };

            Assert.AreEqual(1, set.Count);
        }
    }
}
