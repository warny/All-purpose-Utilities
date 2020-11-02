using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics;

namespace UtilsTest.Math
{
	[TestClass]
	public class MathExTest
	{
		[TestMethod]
		public void IntBetweenTest()
		{
			int lowerBound = 5;
			int upperBound = 10;

			var tests = new (int value, bool resultII, bool resultEI, bool resultIE, bool resultEE)[] {
				(4, false, false, false, false),
				(5, true, false, true, false),
				(7, true, true, true, true),
				(9, true, true, true, true),
				(10, true, true, false, false),
				(11, false, false, false, false),
			};

			foreach (var test in tests)
			{
				Assert.AreEqual(test.resultII, test.value.Between(lowerBound, upperBound));
				Assert.AreEqual(test.resultEI, test.value.Between(lowerBound, upperBound, includeLowerBound: false));
				Assert.AreEqual(test.resultIE, test.value.Between(lowerBound, upperBound, includeUpperBound: false));
				Assert.AreEqual(test.resultEE, test.value.Between(lowerBound, upperBound, includeLowerBound: false, includeUpperBound: false));
			}
		}

		[TestMethod]
		public void DoubleBetweenTest()
		{
			double lowerBound = 5;
			double upperBound = 10;

			var tests = new (double value, bool resultII, bool resultEI, bool resultIE, bool resultEE)[] {
				(4, false, false, false, false),
				(5, true, false, true, false),
				(7, true, true, true, true),
				(9, true, true, true, true),
				(10, true, true, false, false),
				(11, false, false, false, false),
			};

			foreach (var test in tests)
			{
				Assert.AreEqual(test.resultII, test.value.Between(lowerBound, upperBound));
				Assert.AreEqual(test.resultEI, test.value.Between(lowerBound, upperBound, includeLowerBound: false));
				Assert.AreEqual(test.resultIE, test.value.Between(lowerBound, upperBound, includeUpperBound: false));
				Assert.AreEqual(test.resultEE, test.value.Between(lowerBound, upperBound, includeLowerBound: false, includeUpperBound: false));
			}
		}
	}
}
