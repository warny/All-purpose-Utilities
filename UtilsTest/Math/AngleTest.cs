using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics;

namespace UtilsTest.Math
{
	[TestClass]
	public class AngleTest
	{
		[TestMethod]
		public void TestAngle1()
		{

			var tests = new (double angle, double result)[] {
				(0,0),
				(90, 90),
				(180, 180),
				(270, 270),
				(361, 1)
			};

			foreach (var test in tests)
			{
				var angle = new Angle(test.angle, AngleUnitEnum.Degree);
				Assert.AreEqual(test.result, angle.ToDegrees(), 0.0000001);
			}

			foreach (var test in tests)
			{
				var angle = new Angle(test.angle * MathEx.Deg2Rad, AngleUnitEnum.Radian);
				Assert.AreEqual(test.result, angle.ToDegrees(), 0.0000001);
				Assert.AreEqual(test.result * MathEx.Deg2Rad, angle.ToRadians(), 0.0000001);
			}
		}
	}
}
