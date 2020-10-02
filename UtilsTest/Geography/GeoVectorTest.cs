using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace UtilsTest.Geography
{
	[TestClass]
	public class GeoVectorTest
	{
		[TestMethod]
		public void GeoVector1()
		{
			var vectors = new (double latitude, double longitude, double direction, string coordinates, string dcoordinates)[] {
				(0, 0, 10, "0, 0, 10", "0°, 0°, 10"),
				(45, 45, 10, "N45, E45, 10", "N45°, E45°, 10"),
				(-45, 45, 10, "S45, E45, 10", "S45°, E45°, 10"),
				(-45, -45, -350, "S45, W45, 10", "S45°, W45°, 10"),
				(45.5, -45.5, 370, "N45.5, W45.5, 10", "N45°30', W45°30', 10"),
			};

			foreach (var vector in vectors)
			{
				GeoVector geoVector = new GeoVector(vector.latitude, vector.longitude, vector.direction);
				Assert.AreEqual(vector.latitude, geoVector.Latitude);
				Assert.AreEqual(vector.longitude, geoVector.Longitude);
				Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Direction);
				Assert.AreEqual(vector.coordinates, geoVector.ToString());
				Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
			}
		}

		[TestMethod]
		public void GeoVector2()
		{
			var vectors = new (double latitude, double longitude, double direction, string coordinates, string dcoordinates)[] {
				(0, 0, 10, "0, 0, 10", "0°, 0°, 10"),
				(45, 45, 10, "N45, E45, 10", "N45°, E45°, 10"),
				(-45, 45, 10, "S45, E45, 10", "S45°, E45°, 10"),
				(-45, -45, -350, "S45, W45, 10", "S45°, W45°, 10"),
				(45.5, -45.5, 370, "N45.5, W45.5, 10", "N45°30', W45°30', 10"),
			};

			foreach (var vector in vectors)
			{
				GeoVector geoVector = new GeoVector(vector.coordinates);
				Assert.AreEqual(vector.latitude, geoVector.Latitude);
				Assert.AreEqual(vector.longitude, geoVector.Longitude);
				Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Direction);
				Assert.AreEqual(vector.coordinates, geoVector.ToString());
				Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
			}


		}

		[TestMethod]
		public void GeoVector3()
		{
			var vectors = new (double latitude, double longitude, double direction, string coordinates, string dcoordinates)[] {
				(0, 0, 10, "0, 0, 10", "0°, 0°, 10"),
				(45, 45, 10, "N45, E45, 10", "N45°, E45°, 10"),
				(-45, 45, 10, "S45, E45, 10", "S45°, E45°, 10"),
				(-45, -45, -350, "S45, W45, 10", "S45°, W45°, 10"),
				(45.5, -45.5, 370, "N45.5, W45.5, 10", "N45°30', W45°30', 10"),
			};

			foreach (var vector in vectors)
			{
				GeoVector geoVector = new GeoVector(vector.dcoordinates);
				Assert.AreEqual(vector.latitude, geoVector.Latitude);
				Assert.AreEqual(vector.longitude, geoVector.Longitude);
				Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Direction);
				Assert.AreEqual(vector.coordinates, geoVector.ToString());
				Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
			}
		}

		[TestMethod]
		public void GeoVector4()
		{
			var vectors = new (double latitude, double longitude, double direction, string coordinates, string dcoordinates)[] {
				(0, 0, 10, "0, 0, 10", "0°, 0°, 10"),
				(45, 45, 10, "N45, E45, 10", "N45°, E45°, 10"),
				(-45, 45, 10, "S45, E45, 10", "S45°, E45°, 10"),
				(-45, -45, -350, "S45, W45, 10", "S45°, W45°, 10"),
				(45.5, -45.5, 370, "N45.5, W45.5, 10", "N45°30', W45°30', 10"),
			};

			foreach (var vector in vectors)
			{
				GeoVector geoVector = new GeoVector(new GeoPoint(vector.latitude, vector.longitude), vector.direction);
				Assert.AreEqual(vector.latitude, geoVector.Latitude);
				Assert.AreEqual(vector.longitude, geoVector.Longitude);
				Assert.AreEqual(MathEx.Mod(vector.direction, 360), geoVector.Direction);
				Assert.AreEqual(vector.coordinates, geoVector.ToString());
				Assert.AreEqual(vector.dcoordinates, geoVector.ToString("d"));
			}
		}
	}
}
