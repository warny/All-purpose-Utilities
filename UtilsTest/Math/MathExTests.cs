using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Mathematics;

namespace UtilsTest.Math
{
	[TestClass]
	public class MathExTests
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

		[TestMethod]
		public void PascalTriangleTest()
		{
			var tests = new (int line, int[] values)[] {
				( 3, new[] { 1,3,3,1 } ), // utilise le cache d'initialisation
				( 8, new[] { 1, 8, 28, 56, 70, 56, 28, 8, 1, } ), // calcule la 8° ligne, met en cache la 7 et la 8
				( 7, new[] { 1, 7, 21, 35, 35, 21, 7, 1, } ) // récupère le cache de la 7 calculé par la ligne précédente
			};

			var comparer = new ArrayEqualityComparer<int>();

			foreach (var test in tests)
			{
				var result = MathEx.ComputePascalTriangleLine(test.line);
				Assert.IsTrue(comparer.Equals(test.values, result), $"{{ {string.Join(", ", result)} }} is different from {{ {string.Join(", ", test.values)} }} expected");
			}
		}
	}
}
