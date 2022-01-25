using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class EnumeratorsTests
	{
		ArrayEqualityComparer<int> intComparer = new ArrayEqualityComparer<int>();
		ArrayEqualityComparer<float> floatComparer = new ArrayEqualityComparer<float>();
		ArrayEqualityComparer<double> doubleComparer = new ArrayEqualityComparer<double>();

		[TestMethod]
		public void EnumerateRangeTest1()
		{
			string range = "1-3;5;3-1";
			int[] expected = { 1, 2, 3, 5, 3, 2, 1 };
			var result = Enumerators.Enumerate(range).ToArray();
			Assert.IsTrue(intComparer.Equals(expected, result));
		}

		[TestMethod]
		public void EnumerateTest1()
		{
			var tests = new (int start, int end, int[] expected)[] {
				(1, 3, new [] {1,2,3 }),
				(3, 1, new [] { 3,2,1 }),
			};
			foreach (var test in tests)
			{
				var result = Enumerators.Enumerate(test.start, test.end).ToArray();
				Assert.IsTrue(intComparer.Equals(test.expected, result));
			}
		}

		[TestMethod]
		public void EnumerateTest2()
		{
			var tests = new (int start, int end, int step, int[] expected)[] {
				(1, 5, 2, new [] { 1, 3, 5 }),
				(5, 1, 2, new [] { 5, 3, 1 }),
			};
			foreach (var test in tests)
			{
				var result = Enumerators.Enumerate(test.start, test.end, test.step).ToArray();
				Assert.IsTrue(intComparer.Equals(test.expected, result));
			}
		}

		[TestMethod]
		public void EnumerateTest3()
		{
			var tests = new (float start, float end, float step, float[] expected)[] {
				(1, 5, 2, new float[] { 1, 3, 5 }),
				(5, 1, 2, new float[] { 5, 3, 1 }),
			};
			foreach (var test in tests)
			{
				var result = Enumerators.Enumerate(test.start, test.end, test.step).ToArray();
				Assert.IsTrue(floatComparer.Equals(test.expected, result));
			}
		}

		[TestMethod]
		public void EnumerateTest4()
		{
			var tests = new (double start, double end, double step, double[] expected)[] {
				(1, 5, 2, new double[] { 1, 3, 5 }),
				(5, 1, 2, new double[] { 5, 3, 1 }),
			};
			foreach (var test in tests)
			{
				var result = Enumerators.Enumerate(test.start, test.end, test.step).ToArray();
				Assert.IsTrue(doubleComparer.Equals(test.expected, result));
			}
		}

		[TestMethod]
		public void EnumerateCountTest1()
		{
			var tests = new (float start, float end, int count, float[] expected)[] {
				(1, 5, 2, new float[] { 1, 5 }),
				(5, 1, 2, new float[] { 5, 1 }),
				(1, 5, 3, new float[] { 1, 3, 5 }),
				(5, 1, 3, new float[] { 5, 3, 1 }),
			};
			foreach (var test in tests)
			{
				var result = Enumerators.EnumerateCount(test.start, test.end, test.count).ToArray();
				Assert.IsTrue(floatComparer.Equals(test.expected, result));
			}
		}

		[TestMethod]
		public void EnumerateCountTest2()
		{
			var tests = new (double start, double end, int count, double[] expected)[] {
				(1, 5, 2, new double[] { 1, 5 }),
				(5, 1, 2, new double[] { 5, 1 }),
				(1, 5, 3, new double[] { 1, 3, 5 }),
				(5, 1, 3, new double[] { 5, 3, 1 }),
			};
			foreach (var test in tests)
			{
				var result = Enumerators.EnumerateCount(test.start, test.end, test.count).ToArray();
				Assert.IsTrue(doubleComparer.Equals(test.expected, result));
			}
		}
	}
}
