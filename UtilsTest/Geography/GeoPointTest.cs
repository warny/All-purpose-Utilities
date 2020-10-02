using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Geography.Model;

namespace UtilsTest.Geography
{
	[TestClass]
	public class GeoPointTest
	{
		(double latitude, double longitude, string coordinates, string dcoordinates)[] points = new (double latitude, double longitude, string coordinates, string dcoordinates) [] {
			(0,0, "0, 0", "0°, 0°"),
			(45,45, "N45, E45", "N45°, E45°"),
			(-45,45, "S45, E45", "S45°, E45°"),
			(-45,-45, "S45, W45", "S45°, W45°"),
			(45.5,-45.5, "N45.5, W45.5", "N45°30', W45°30'"),
		};

		[TestMethod]
		public void GeoPoint1()
		{
			foreach (var point in points) {
				GeoPoint geoPoint = new GeoPoint(point.latitude, point.longitude);
				Assert.AreEqual(point.latitude, geoPoint.Latitude);
				Assert.AreEqual(point.longitude, geoPoint.Longitude);
				Assert.AreEqual(point.coordinates, geoPoint.ToString());
				Assert.AreEqual(point.dcoordinates, geoPoint.ToString("d"));
			}

		}

		[TestMethod]
		public void GeoPoint2()
		{
			foreach (var point in points) {
				GeoPoint geoPoint = new GeoPoint(point.coordinates);
				Assert.AreEqual(point.latitude, geoPoint.Latitude);
				Assert.AreEqual(point.longitude, geoPoint.Longitude);
				Assert.AreEqual(point.coordinates, geoPoint.ToString());
				Assert.AreEqual(point.dcoordinates, geoPoint.ToString("d"));
			}
		}

		[TestMethod]
		public void GeoPoint3()
		{
			foreach (var point in points) {
				GeoPoint geoPoint = new GeoPoint(point.dcoordinates);
				Assert.AreEqual(point.latitude, geoPoint.Latitude);
				Assert.AreEqual(point.longitude, geoPoint.Longitude);
				Assert.AreEqual(point.coordinates, geoPoint.ToString());
				Assert.AreEqual(point.dcoordinates, geoPoint.ToString("d"));
			}
		}
	}
}
