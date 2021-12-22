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
			var tests = new (Range<double> Test, string Result)[] {
				(new Range<double>(1, 5), "[ 1 - 5 ]"),
				(new Range<double>(-3, 8), "[ -3 - 8 ]"),
				(new Range<double>(-8, -3), "[ -8 - -3 ]"),
				(new Range<double>(1, 5, containsStart: false), "] 1 - 5 ]"),
				(new Range<double>(-3, 8, containsEnd: false), "[ -3 - 8 ["),
				(new Range<double>(-8, -3, false, false), "] -8 - -3 ["),
			};

			foreach (var test in tests)
			{
				var ranges = new Ranges<double>();
				ranges.Add(test.Test);
				Assert.AreEqual(test.Result, ranges.ToString());
			}
		}

		[TestMethod]
		public void UnionRangeTest1()
		{
			var tests = new ((int Start, int End)[] Ranges, string Result)[] {
				(new [] { (1, 5), (6, 7) }, "[ 1 - 5 ] ∪ [ 6 - 7 ]"),
				(new [] { (6, 7), (1, 5) }, "[ 1 - 5 ] ∪ [ 6 - 7 ]"),
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
				(new [] { (1, 5) }, new [] { (2, 3) }, "[ 1 - 2 [ ∪ ] 3 - 5 ]"),
				(new [] { (-5, -1), (1, 5) }, new [] { (-2, 2) }, "[ -5 - -2 [ ∪ ] 2 - 5 ]"),
				(new [] { (-5, -3), (-1, 1), (3, 5) }, new [] { (-4, 4) }, "[ -5 - -4 [ ∪ ] 4 - 5 ]"),
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

		[TestMethod]
		public void MiscelaneousTests() {
			Ranges<double> ranges = new Ranges<double>(new Range<double>(-10, 10));

			Assert.AreEqual("[ -10 - 10 ]", ranges.ToString());
			Assert.IsTrue(ranges.Contains(5));
			
			ranges.Remove(5);
			Assert.AreEqual("[ -10 - 5 [ ∪ ] 5 - 10 ]", ranges.ToString());
			Assert.IsFalse(ranges.Contains(5));
			
			ranges.Add(5);
			Assert.AreEqual("[ -10 - 10 ]", ranges.ToString());
			Assert.IsTrue(ranges.Contains(5));

			ranges.Remove(-5, 5, false, false);
			Assert.AreEqual("[ -10 - -5 ] ∪ [ 5 - 10 ]", ranges.ToString());
			Assert.IsTrue(ranges.Contains(-5));
			Assert.IsFalse(ranges.Contains(0));
			Assert.IsTrue(ranges.Contains(5));

			ranges.Add(-5, 5);
			Assert.AreEqual("[ -10 - 10 ]", ranges.ToString());
			Assert.IsTrue(ranges.Contains(-5));
			Assert.IsTrue(ranges.Contains(0));
			Assert.IsTrue(ranges.Contains(5));

			ranges.Remove(-5, 5);
			Assert.AreEqual("[ -10 - -5 [ ∪ ] 5 - 10 ]", ranges.ToString());
			Assert.IsFalse(ranges.Contains(-5));
			Assert.IsFalse(ranges.Contains(0));
			Assert.IsFalse(ranges.Contains(5));

			ranges.Add(-5, 5);
			Assert.AreEqual("[ -10 - 10 ]", ranges.ToString());
			Assert.IsTrue(ranges.Contains(-5));
			Assert.IsTrue(ranges.Contains(0));
			Assert.IsTrue(ranges.Contains(5));

			ranges.Remove(-5, 5);
			ranges.Add(-5, 5, false, false);
			Assert.AreEqual("[ -10 - -5 [ ∪ ] -5 - 5 [ ∪ ] 5 - 10 ]", ranges.ToString());
			Assert.IsFalse(ranges.Contains(-5));
			Assert.IsTrue(ranges.Contains(0));
			Assert.IsFalse(ranges.Contains(5));
		}

	}
}
