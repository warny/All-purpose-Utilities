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
	public class PlanetTests
	{
		[TestMethod]
		public void TestDistance()
		{
			(GeoPoint<double> geoPoint1, GeoPoint<double> geoPoint2, double distance)[] distances = [
				(new (0,0), new (0,0), 0.0),
				(new (10,10), new (10,10), 0.0),
				(new (0,0), new (0,90), System.Math.PI / 2),
				(new (0,0), new (90,0), System.Math.PI / 2),
				(new (0,0), new (60,0), System.Math.PI / 3),
				(new (0,0), new (0,60), System.Math.PI / 3)
			];

			for (double i = 1; i < 256; i *= 2)
			{
				var planet = new Planet<double>(i);
				foreach (var distance in distances)
				{
					Assert.AreEqual(distance.distance * i, planet.Distance(distance.geoPoint1, distance.geoPoint2), 0.0001);
				}
			}
		}

		[TestMethod]
		public void TestMove()
		{
			(GeoVector<double> start, double distance, GeoVector<double> result)[] moves = [
				(new (0, 0, 0), 0, new (0, 0, 0)),
				(new (0, 90, 0), 0, new (0, 90, 0)),
				(new (90, 0, 0), 0, new (90, 0, 0)),
				(new (0, 0, 0), System.Math.PI / 2, new (90, 0, 0)),
				(new (0, 0, 0), System.Math.PI * 1.5, new (-90, 0, 180)),
				(new (0, 0, 0), System.Math.PI / 3, new (60, 0, 0)),
				(new (0, 0, 90), System.Math.PI / 2, new (0, 90, 90)),
				(new (0, 0, 90), System.Math.PI * 1.5, new (0, -90, 90)),
				(new (0, 0, 90), System.Math.PI / 3, new (0, 60, 90)),
				(new (0, 0, 180), System.Math.PI / 2, new (-90, 0, 180)),
				(new (0, 0, 180), System.Math.PI * 1.5, new (90, 0, 0)),
				(new (0, 0, 180), System.Math.PI / 3, new (-60, 0, 180)),
				(new (0, 0, 270), System.Math.PI / 2, new (0, -90, 270)),
				(new (0, 0, 270), System.Math.PI * 1.5, new (0, 90, 270)),
				(new (0, 0, 270), System.Math.PI / 3, new (0, -60, 270)),
				(new (90, 0, 0), System.Math.PI / 2, new (0, 180, 180)),
				(new (90, 90, 0), System.Math.PI / 2, new (0, -90, 180)),
				(new (90, 180, 0), System.Math.PI / 2, new (0, 0, 180)),
				(new (90, -90, 0), System.Math.PI / 2, new (0, 90, 180)),
				(new (-90, 0, 0), System.Math.PI / 2, new (0, 180, 0)),
				(new (-90, 90, 0), System.Math.PI / 2, new (0, -90, 0)),
				(new (-90, 180, 0), System.Math.PI / 2, new (0, 0, 0)),
				(new (-90, -90, 0), System.Math.PI / 2, new (0, 90, 0)),
				(new (0, 0, 45), System.Math.PI / 2, new (45, 90, 90)),
				(new (0, 0, -45), System.Math.PI / 2, new (45, -90, -90)),
				(new (0, 0, 135), System.Math.PI / 2, new (-45, 90, 90)),
				(new (0, 0, -135), System.Math.PI / 2, new (-45, -90, -90)),
				(new (0, 0, 45), System.Math.PI, new (0, 180, -135)),
				(new (0, 0, -45), System.Math.PI, new (0, 180, 135)),
				(new (0, 0, 135), System.Math.PI, new (0, 180, -45)),
				(new (0, 0, -135), System.Math.PI, new (0, 180, 45)),
			];

			Planet<double> planet = new(1);
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
			var earth = Planets<double>.Earth;
			var paris = new GeoPoint<double>("N48°51',E2°21'");
			var newyork = new GeoPoint<double>("N40°43',W74°00'");

			var baghdad = new GeoPoint<double>("N35°,E45°");
			var osaka = new GeoPoint<double>("N35°,E135°");

			var vector1 = new GeoVector<double>(paris, newyork);
			Assert.AreEqual(291.7, vector1.Bearing, 0.1);
			Assert.AreEqual(newyork, new GeoPoint<double>(earth.Travel(vector1, earth.Distance(paris, newyork))));

			var vector2 = new GeoVector<double>(baghdad, osaka);
			Assert.AreEqual(60.1, vector2.Bearing, 0.1);
			Assert.AreEqual(osaka, new GeoPoint<double>(earth.Travel(vector2, earth.Distance(baghdad, osaka))));
		}
	}
}
