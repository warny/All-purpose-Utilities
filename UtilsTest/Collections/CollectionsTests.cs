using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Collections;

namespace UtilsTest.Collections
{
	[TestClass]
	public class CollectionsTests
	{
		[TestMethod]
		public void SameSizeCollections()
		{
			var left = new int[] { 1, 2, 3 };
			var right = new string[] { "a", "b", "c" };

			var expected = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c"),
			};

			var test = CollectionUtils.Zip(left, right).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(expected, test));
		}

		[TestMethod]
		public void LongerLeftCollectionsTest1()
		{
			var left = new int[] { 1, 2, 3, 4 };
			var right = new string[] { "a", "b", "c" };

			var expected = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c"),
				(4, null)
			};

			var test = CollectionUtils.Zip(left, right).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(expected, test));
		}

		[TestMethod]
		public void LongerRightCollectionsTest1()
		{
			var left = new int[] { 1, 2, 3 };
			var right = new string[] { "a", "b", "c", "d" };

			var expected = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c"),
				(0, "d")
			};

			var test = CollectionUtils.Zip(left, right).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(expected, test));
		}

		[TestMethod]
		public void LongerLeftCollectionsTest2()
		{
			var left = new int[] { 1, 2, 3, 4 };
			var right = new string[] { "a", "b", "c" };

			var expected = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c")
			};

			var test = CollectionUtils.Zip(left, right, continueAfterShortestListEnds: false).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(expected, test));
		}

		[TestMethod]
		public void LongerRightCollectionsTest2()
		{
			var left = new int[] { 1, 2, 3 };
			var right = new string[] { "a", "b", "c", "d" };

			var expected = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c")
			};

			var test = CollectionUtils.Zip(left, right, continueAfterShortestListEnds: false).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(expected, test));
		}

		[TestMethod]
		public void PackTest()
		{
			var test = new string[] { "a", "b", "b", "c", "b", "b" };
			var expected = new (string value, int repetition)[] {
				("a", 1),
				("b", 2),
				("c", 1),
				("b", 2),
			};

			var result = CollectionUtils.Pack(test).ToArray();

			var comparer = new ArrayEqualityComparer<(string, int)>();
			Assert.IsTrue(comparer.Equals(expected, result));
		}

		[TestMethod]
		public void UpackTest()
		{
			var test = new (string value, int repetition)[] {
				("a", 1),
				("b", 2),
				("c", 1),
				("b", 2),
			};
			var expected = new string[] { "a", "b", "b", "c", "b", "b" };

			var result = CollectionUtils.Unpack(test).ToArray();

			var comparer = new ArrayEqualityComparer<string>();
			Assert.IsTrue(comparer.Equals(expected, result));
		}

	}
}
