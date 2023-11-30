using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Arrays;
using Utils.Geography.Model;
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
				GeoVector<double> geoVector = new (vector.latitude, vector.longitude, vector.direction);
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
				GeoVector<double> geoVector = new (vector.coordinates);
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
				GeoVector<double> geoVector = new (vector.dcoordinates);
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
				GeoVector<double> geoVector = new (new (vector.latitude, vector.longitude), vector.direction);
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
				(new (0, 0, 0), new (0, 90, 0), [ new (90, 0), new (-90, 0) ]),
				(new (45, 0, 90), new (0, 0, 90), [ new (0, 90), new (0, -90) ]),
				(new (45, 0, 90), new (-45, 0, 90), [ new (0, 90), new (0, -90) ]),
				(new (45, 0, 90), new (0, 90, 0), [ new (0, -90), new (0, 90) ]),
				(new (45, 0, 90), new (0, 0, 0), [ new (45, 0), new (-45, 180) ]),
				(new (0, 90, 135), new (0, 0, 0), [ new (-45, 180), new (45, 0) ]),
				(new (0, 0, 135), new (0, -90, 0), [ new (-45, 90), new (45, 270) ]),
				(new (0, 0, 45), new (0, -90, 0), [ new (45, 90), new (-45, 270) ]),
			};

			var geoPointsComparer = new ArrayEqualityComparer<GeoPoint<double>>();

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
	}
}
