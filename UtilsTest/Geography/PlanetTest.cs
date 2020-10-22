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
		}

		[TestMethod]
		public void TestMove()
		{
			(GeoVector start, double distance, GeoVector result)[] moves = new (GeoVector start, double distance, GeoVector result)[] {
				(new GeoVector(0, 0, 0), 0, new GeoVector(0, 0, 0)),
				(new GeoVector(0, 90, 0), 0, new GeoVector(0, 90, 0)),
				(new GeoVector(90, 0, 0), 0, new GeoVector(90, 0, 0)),
				(new GeoVector(0, 0, 0), System.Math.PI / 2, new GeoVector(90, 0, 0)),
				(new GeoVector(0, 0, 0), System.Math.PI * 1.5, new GeoVector(-90, 0, 180)),
				(new GeoVector(0, 0, 0), System.Math.PI / 3, new GeoVector(60, 0, 0)),
				(new GeoVector(0, 0, 90), System.Math.PI / 2, new GeoVector(0, 90, 90)),
				(new GeoVector(0, 0, 90), System.Math.PI * 1.5, new GeoVector(0, -90, 90)),
				(new GeoVector(0, 0, 90), System.Math.PI / 3, new GeoVector(0, 60, 90)),
				(new GeoVector(0, 0, 180), System.Math.PI / 2, new GeoVector(-90, 0, 180)),
				(new GeoVector(0, 0, 180), System.Math.PI * 1.5, new GeoVector(90, 0, 0)),
				(new GeoVector(0, 0, 180), System.Math.PI / 3, new GeoVector(-60, 0, 180)),
				(new GeoVector(0, 0, 270), System.Math.PI / 2, new GeoVector(0, -90, 270)),
				(new GeoVector(0, 0, 270), System.Math.PI * 1.5, new GeoVector(0, 90, 270)),
				(new GeoVector(0, 0, 270), System.Math.PI / 3, new GeoVector(0, -60, 270)),
				(new GeoVector(90, 0, 0), System.Math.PI / 2, new GeoVector(0, 180, 180)),
				(new GeoVector(90, 90, 0), System.Math.PI / 2, new GeoVector(0, -90, 180)),
				(new GeoVector(90, 180, 0), System.Math.PI / 2, new GeoVector(0, 0, 180)),
				(new GeoVector(90, -90, 0), System.Math.PI / 2, new GeoVector(0, 90, 180)),
				(new GeoVector(-90, 0, 0), System.Math.PI / 2, new GeoVector(0, 180, 0)),
				(new GeoVector(-90, 90, 0), System.Math.PI / 2, new GeoVector(0, -90, 0)),
				(new GeoVector(-90, 180, 0), System.Math.PI / 2, new GeoVector(0, 0, 0)),
				(new GeoVector(-90, -90, 0), System.Math.PI / 2, new GeoVector(0, 90, 0)),
				(new GeoVector(0, 0, 45), System.Math.PI / 2, new GeoVector(45, 90, 90)),
				(new GeoVector(0, 0, -45), System.Math.PI / 2, new GeoVector(45, -90, -90)),
				(new GeoVector(0, 0, 135), System.Math.PI / 2, new GeoVector(-45, 90, 90)),
				(new GeoVector(0, 0, -135), System.Math.PI / 2, new GeoVector(-45, -90, -90)),
				(new GeoVector(0, 0, 45), System.Math.PI, new GeoVector(0, 180, -135)),
				(new GeoVector(0, 0, -45), System.Math.PI, new GeoVector(0, 180, 135)),
				(new GeoVector(0, 0, 135), System.Math.PI, new GeoVector(0, 180, -45)),
				(new GeoVector(0, 0, -135), System.Math.PI, new GeoVector(0, 180, 45)),
			};

			Planet planet = new Planet(1);
			int i = 0;
			foreach (var move in moves)
			{
				var destination = planet.Travel(move.start, move.distance);
				Assert.AreEqual(move.result, destination, $"{i} - ({move.start}) Travel of {move.distance}");
				i++;
			}
		}

		[TestMethod]
		public void TestVector()
		{
			Planet earth = Planets.Earth;
			GeoPoint paris = new GeoPoint("N48°51',E2°21'");
			GeoPoint newyork = new GeoPoint("N40°43',W74°00'");

			GeoPoint baghdad = new GeoPoint("N35°,E45°");
			GeoPoint osaka = new GeoPoint("N35°,E135°");

			GeoVector vector1 = new GeoVector(paris, newyork);
			Assert.AreEqual(291.7, vector1.Bearing, 0.1);
			Assert.AreEqual(newyork, new GeoPoint(earth.Travel(vector1, earth.Distance(paris, newyork))));

			GeoVector vector2 = new GeoVector(baghdad, osaka);
			Assert.AreEqual(60.1, vector2.Bearing, 0.1);
			Assert.AreEqual(osaka, new GeoPoint(earth.Travel(vector2, earth.Distance(baghdad, osaka))));
		}
	}
}
