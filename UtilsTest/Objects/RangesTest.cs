using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class RangesTest
	{
		[TestMethod]
		public void SimpleRangeTest1()
		{
			var tests = new (int Start, int End, string Result)[] {
				(1, 5, "[ 1 - 5 ]"),
				(5, 1, "[ 1 - 5 ]"),
				(-3, 8, "[ -3 - 8 ]"),
				(-8, -3, "[ -8 - -3 ]"),
			};

			foreach (var test in tests)
			{
				var ranges = new Ranges<double>();
				ranges.Add(test.Start, test.End);
				Assert.AreEqual(test.Result, ranges.ToString());
			}
		}

		[TestMethod]
		public void UnionRangeTest1()
		{
			var tests = new ((int Start, int End)[] Ranges, string Result)[] {
				(new [] { (1, 5), (6, 7) }, "[ 1 - 5 ] ∪ [ 6 - 7 ]"),
				(new [] { (5, 1), (7, 6) }, "[ 1 - 5 ] ∪ [ 6 - 7 ]"),
				(new [] { (7, 6), (5, 1) }, "[ 1 - 5 ] ∪ [ 6 - 7 ]"),
				(new [] { (-3, 8), (6, 7) }, "[ -3 - 8 ]"),
				(new [] { (-8, -3), (-3, 7) }, "[ -8 - 7 ]"),
				(new [] { (-8, -3), (-4, 7) }, "[ -8 - 7 ]"),
			};

			foreach (var test in tests)
			{
				var ranges = new Ranges<double>();
				foreach (var range in test.Ranges)
				{
					ranges.Add(range.Start, range.End);
				}
				Assert.AreEqual(test.Result, ranges.ToString());
			}
		}

		[TestMethod]
		public void ExcludeRangeTest1()
		{
			var tests = new ((int Start, int End)[] Ranges, (int Start, int End)[] Exclusions, string Result)[] {
				(new [] { (1, 5) }, new [] { (7, 8) }, "[ 1 - 5 ]"),
				(new [] { (1, 5) }, new [] { (2, 3) }, "[ 1 - 2 ] ∪ [ 3 - 5 ]"),
				(new [] { (-5, -1), (1, 5) }, new [] { (-2, 2) }, "[ -5 - -2 ] ∪ [ 2 - 5 ]"),
				(new [] { (-5, -3), (-1, 1), (3, 5) }, new [] { (-4, 4) }, "[ -5 - -4 ] ∪ [ 4 - 5 ]"),
			};

			foreach (var test in tests)
			{
				var ranges = new Ranges<double>();
				foreach (var range in test.Ranges)
				{
					ranges.Add(range.Start, range.End);
				}
				foreach (var range in test.Exclusions)
				{
					ranges.Remove(range.Start, range.End);
				}
				Assert.AreEqual(test.Result, ranges.ToString());
			}
		}
	}
}
