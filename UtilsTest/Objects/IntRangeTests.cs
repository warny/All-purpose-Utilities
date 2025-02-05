using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Linq;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class IntRangeTests
	{
		[TestMethod]
		public void IntRangeParseTest()
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
				var intRange = new IntRange<int>(test.test, CultureInfo.GetCultureInfo("fr-FR"));
				Assert.AreEqual(test.result, intRange.ToString(CultureInfo.GetCultureInfo("fr-FR")));
			}
		}

		[TestMethod]
		public void IntRangeUnionTest()
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
				var range1 = new IntRange<int>(test1, CultureInfo.GetCultureInfo("fr-FR"));
				var range2 = new IntRange<int>(test2, CultureInfo.GetCultureInfo("fr-FR"));
				var result = range1 | range2;
				Assert.AreEqual(expected, result.ToString(CultureInfo.GetCultureInfo("fr-FR")));
			}

			foreach (var test in tests)
			{
				doTest(test.test1, test.test2, test.result);
				doTest(test.test2, test.test1, test.result);
			}
		}

		[TestMethod]
		public void IntRangeIntersectTest()
		{
			// Each tuple: (range1 string, range2 string, expected intersection)
			// An empty string "" indicates that the intersection is empty (no intervals).
			var tests = new (string test1, string test2, string result)[]
			{
				// Disjoint single values => no intersection
				("1",        "3",         ""),
				// Same single value => intersection is that value
				("1",        "1",         "1"),
				// Simple overlapping intervals => "1-3" and "2-4" => intersection is "2-3"
				("1-3",      "2-4",       "2-3"),
				// One overlaps the end of the other => "1-4" and "4-8" => intersection is "4"
				("1-4",      "4-8",       "4"),
				// Multiple intervals vs single interval => "1-2;5-7" & "2-5" => intersection is "2;5"
				("1-2;5-7",  "2-5",       "2;5"),
				// Slightly more complex intervals => "1-4;6-8" & "4-6" => intersection is "4;6"
				("1-4;6-8",  "4-6",       "4;6"),
				// Contained intervals => "1-9" & "2-3;5-7" => intersection is exactly "2-3;5-7"
				("1-9",      "2-3;5-7",   "2-3;5-7"),
				// Edge overlap => "1-9" & "9-10" => intersection is "9"
				("1-9",      "9-10",      "9"),
				// Single points vs intervals => "2;4;6" & "3-5;6-7" => intersection is "4;6"
				("2;4;6",    "3-5;6-7",   "4;6"),
			};

			void doTest(string test1, string test2, string expected)
			{
				var range1 = new IntRange<int>(test1, CultureInfo.GetCultureInfo("fr-FR"));
				var range2 = new IntRange<int>(test2, CultureInfo.GetCultureInfo("fr-FR"));

				// Intersect using the bitwise AND operator '&'
				var result = range1 & range2;

				// Convert the result back to string and compare
				Assert.AreEqual(expected, result.ToString(CultureInfo.GetCultureInfo("fr-FR")));
			}

			// Run each test in both directions, 
			// since intersection is commutative (A & B == B & A).
			foreach (var test in tests)
			{
				doTest(test.test1, test.test2, test.result);
				doTest(test.test2, test.test1, test.result);
			}
		}


		[TestMethod]
		public void IntRangeExceptTest()
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
				var range1 = new IntRange<int>(test.test1, CultureInfo.GetCultureInfo("fr-FR"));
				var range2 = new IntRange<int>(test.test2, CultureInfo.GetCultureInfo("fr-FR"));
				var result = range1 - range2;
				Assert.AreEqual(test.result, result.ToString(CultureInfo.GetCultureInfo("fr-FR")));
			}
		}

		[TestMethod]
		public void IntRangeContainsTest()
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
				var range = new IntRange<int>(test.range, CultureInfo.GetCultureInfo("fr-FR"));
				foreach (var value in test.values)
				{
					Assert.AreEqual(test.result, range.Contains(value), $"{value} {(test.result ? "n'a pas": "a")} été trouvé dans {range}");
				}
			}
		}

		[TestMethod]
		public void IntRangeToArrayTest()
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
				var range = new IntRange<int>(test.range, CultureInfo.GetCultureInfo("fr-FR"));
				var result = range.ToArray();
				Assert.IsTrue(comparer.Equals(test.result, result), $"{{{string.Join(",", result)}}} est différent de {{{string.Join(",", test.result)}}}");
			}
		}
	}
}
