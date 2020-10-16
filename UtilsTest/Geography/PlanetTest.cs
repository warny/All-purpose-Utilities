using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace UtilsTest.Geography
{
	[TestClass]
	public class PlanetTest
	{
		[TestMethod]
		public void TestDistance()
		{
			(GeoPoint geoPoint1, GeoPoint geoPoint2, double distance)[] distances = new (GeoPoint geoPoint1, GeoPoint geoPoint2, double distance)[] {
				(new GeoPoint(0,0), new GeoPoint(0,0), 0.0),
				(new GeoPoint(10,10), new GeoPoint(10,10), 0.0),
				(new GeoPoint(0,0), new GeoPoint(0,90), System.Math.PI / 2),
				(new GeoPoint(0,0), new GeoPoint(90,0), System.Math.PI / 2),
				(new GeoPoint(0,0), new GeoPoint(60,0), System.Math.PI / 3),
				(new GeoPoint(0,0), new GeoPoint(0,60), System.Math.PI / 3)
			};

			for (double i = 1; i < 256; i *= 2)
			{
				Planet planet = new Planet(i);
				foreach (var distance in distances)
				{
					Assert.AreEqual(distance.distance * i, planet.Distance(distance.geoPoint1, distance.geoPoint2), 0.0001);
				}
			}

			Planet earth = Planets.Earth;
			GeoPoint paris = new GeoPoint("N48°51',E2°21'");
			GeoPoint newyork = new GeoPoint("N40°43'N,O74°00'");
			Assert.AreEqual(5832, earth.Distance(paris, newyork), 0.5);
		}

		[TestMethod]
		public void TestMove()
		{
			(GeoVector start, double distance, GeoVector result)[] movements = new (GeoVector start, double distance, GeoVector result)[] {
				(new GeoVector(0, 0, 0), 0, new GeoVector(0, 0, 0)),
				(new GeoVector(0, 90, 0), 0, new GeoVector(0, 90, 0)),
				(new GeoVector(90, 0, 0), 0, new GeoVector(90, 0, 0)),
				(new GeoVector(0, 0, 0), System.Math.PI / 2, new GeoVector(90, 0, 0)),
				(new GeoVector(0, 0, 0), System.Math.PI / 3, new GeoVector(60, 0, 0)),
				(new GeoVector(0, 0, 90), System.Math.PI / 2, new GeoVector(0, 90, 90)),
				(new GeoVector(0, 0, 90), System.Math.PI / 3, new GeoVector(0, 60, 90)),
				(new GeoVector(0, 0, 180), System.Math.PI / 2, new GeoVector(-90, 0, 180)),
				(new GeoVector(0, 0, 180), System.Math.PI / 3, new GeoVector(-60, 0, 180)),
				(new GeoVector(0, 0, 270), System.Math.PI / 2, new GeoVector(0, -90, 270)),
				(new GeoVector(0, 0, 270), System.Math.PI / 3, new GeoVector(0, -60, 270))
			};

			Planet planet = new Planet(1);
			foreach (var movement in movements)
			{
				Assert.AreEqual(movement.result, planet.Travel(movement.start, movement.distance));
			}
		}

		[TestMethod]
		public void TestVector()
		{
			Planet earth = Planets.Earth;
			GeoPoint paris = new GeoPoint("N48°51',E2°21'");
			GeoPoint newyork = new GeoPoint("N40°43',O74°00'");

			GeoPoint baghdad = new GeoPoint("N35°,E45°");
			GeoPoint osaka = new GeoPoint("N35°,E135°");

			GeoVector vector = new GeoVector(paris, newyork);
			Assert.AreEqual(290, vector.Bearing, 1);

			GeoVector vector2 = new GeoVector(baghdad, osaka);
			Assert.AreEqual(300, vector2.Bearing, 1);

		}
	}
}
