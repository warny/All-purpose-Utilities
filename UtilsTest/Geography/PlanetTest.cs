﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Utils.Geography.Model;

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
					Assert.AreEqual(distance.distance * i, planet.Distance(distance.geoPoint1, distance.geoPoint2));
				}
			}

		}

		[TestMethod]
		public void TestMove()
		{
			(GeoVector start, double distance, GeoVector result)[] movements = new (GeoVector start, double distance, GeoVector result)[] {
				(new GeoVector(0, 0, 0), 0, new GeoVector(0, 0, 0)),
				(new GeoVector(0, 90, 0), 0, new GeoVector(0, 90, 0)),
				(new GeoVector(90, 0, 0), 0, new GeoVector(90, 0, 0)),
				(new GeoVector(0, 0, 0), System.Math.PI / 2, new GeoVector(0, 90, 0)),
				(new GeoVector(0, 0, 0), System.Math.PI / 3, new GeoVector(0, 60, 0)),
				(new GeoVector(0, 0, 90), System.Math.PI / 2, new GeoVector(90, 0, 0)),
				(new GeoVector(0, 0, 90), System.Math.PI / 3, new GeoVector(60, 0, 0))
			};

			Planet planet = new Planet(1);
			foreach (var movement in movements)
			{
				Assert.AreEqual(new GeoPoint(movement.result), planet.Move(movement.start, movement.distance));
			}
		}
	}
}