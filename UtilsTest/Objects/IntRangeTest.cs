using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class IntRangeTest
	{
		[TestMethod]
		public void IntRangeTest1()
		{
			var tests = new (string test, string result)[] {
				("1;2;3", "1-3"),
				("3;2;1", "1-3"),
				("1;3", "1;3"),
				("1-3", "1-3"),
				("1-3;5", "1-3;5"),
				("5;1-3", "1-3;5"),
				("1-3;4", "1-4"),
				("1-3;4-6", "1-6"),
				("4-6;1-3;", "1-6"),
				("1-4;3-6", "1-6"),
				("3;1-4;3-6", "1-6"),
			};

			foreach (var test in tests)
			{
				var intRange = new IntRange(test.test);
				Assert.AreEqual(test.result, intRange.ToString());
			}
		}

		[TestMethod]
		public void IntRangeTest2()
		{
			var tests = new (string test1, string test2, string result)[] {
				("1", "3", "1;3"),
				("1", "2", "1-2"),
				("1-2", "3", "1-3"),
				("1", "2-3", "1-3"),
				("1-2", "4-5", "1-2;4-5"),
				("1-2;7-9", "4-5", "1-2;4-5;7-9"),
				("1-3", "4-5", "1-5"),
				("1-3;7-9", "4-6", "1-9"),
				("1-3;5-9", "4", "1-9"),
				("1-3;5-9", "2-6", "1-9"),
			};

			void doTest (string test1, string test2, string expected)
			{
				var range1 = new IntRange(test1);
				var range2 = new IntRange(test2);
				var result = range1 + range2;
				Assert.AreEqual(expected, result.ToString());
			}

			foreach (var test in tests)
			{
				doTest(test.test1, test.test2, test.result);
				doTest(test.test2, test.test1, test.result);
			}
		}

		[TestMethod]
		public void IntRangeTest3()
		{
			var tests = new (string test1, string test2, string result)[] {
				("1", "3", "1"),
				("1", "2", "1"),
				("1", "1", ""),
				("1-2", "3", "1-2"),
				("1-2", "3-4", "1-2"),
				("1-5", "3", "1-2;4-5"),
				("1-5", "3-5", "1-2"),
				("1-5", "2;4", "1;3;5"),
				("1-5;7-9", "2;4;8", "1;3;5;7;9"),
				("1-5;7-9", "4-7", "1-3;8-9"),
				("1-5;7-9", "4-9", "1-3"),
				("1-5;7-9", "1-7", "8-9"),
			};

			foreach (var test in tests)
			{
				var range1 = new IntRange(test.test1);
				var range2 = new IntRange(test.test2);
				var result = range1 - range2;
				Assert.AreEqual(test.result, result.ToString());
			}
		}

		[TestMethod]
		public void IntRangeTest4()
		{
			var tests = new (string range, int[] values, bool result)[] {
				("1", new [] { 1 }, true),
				("1", new [] { 0, 2 }, false),
				("1-3", new [] { 1, 2, 3 }, true),
				("1-3", new [] { 0, 4 }, false),
				("1-3; 5-7", new [] { 1, 2, 3, 5, 6, 7 }, true),
				("1-3; 5-7", new [] { 0, 4, 8 }, false),
				("1; 5-7", new [] { 1, 5, 6, 7 }, true),
				("1; 5-7", new [] { 0, 2,3, 4, 8 }, false),
			};

			foreach (var test in tests)
			{
				var range = new IntRange(test.range);
				foreach (var value in test.values)
				{
					Assert.AreEqual(test.result, range.Contains(value), $"{value} {(test.result ? "n'a pas": "a")} été trouvé dans {range}");
				}
			}
		}

		[TestMethod]
		public void IntRangeTest5()
		{
			var comparer = new Utils.Arrays.ArrayEqualityComparer<int>();

			var tests = new (string range, int[] result)[] {
				("1", new [] { 1 }),
				("1-3", new [] { 1, 2, 3 }),
				("1-3; 5-7", new [] { 1, 2, 3, 5, 6, 7 }),
				("1; 5-7", new [] { 1, 5, 6, 7 }),
			};

			foreach (var test in tests)
			{
				var range = new IntRange(test.range);
				var result = range.ToArray();
				Assert.IsTrue(comparer.Equals(test.result, result), $"{{{string.Join(",", result)}}} est différent de {{{string.Join(",", test.result)}}}");
			}
		}
	}
}
