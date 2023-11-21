﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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

			var test = EnumerableEx.Zip(left, right).ToArray();

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

			var test = EnumerableEx.Zip(left, right).ToArray();

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

			var test = EnumerableEx.Zip(left, right).ToArray();

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

			var test = EnumerableEx.Zip(left, right, continueAfterShortestListEnds: false).ToArray();

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

			var test = EnumerableEx.Zip(left, right, continueAfterShortestListEnds: false).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(expected, test));
		}

		[TestMethod]
		public void PackTest()
		{
			var test = new string[] { "a", "b", "b", "c", "b", "b" };
			var expected = new Pack<string>[] {
				new ("a", 1),
				new ("b", 2),
				new ("c", 1),
				new ("b", 2),
			};

			var result = EnumerableEx.Pack(test).ToArray();

			var comparer = new ArrayEqualityComparer<Pack<string>>();
			Assert.IsTrue(comparer.Equals(expected, result));
		}

		[TestMethod]
		public void UpackTest()
		{
			var test = new Pack<string>[] {
				new ("a", 1),
				new ("b", 2),
				new ("c", 1),
				new ("b", 2),
			};
			var expected = new string[] { "a", "b", "b", "c", "b", "b" };

			var result = EnumerableEx.Unpack(test).ToArray();

			var comparer = new ArrayEqualityComparer<string>();
			Assert.IsTrue(comparer.Equals(expected, result));
		}

		[TestMethod]
		public void SliceTest()
		{
			var test = new string[] { "a", "b", "c", "d", "e", "f" };
			var indexes = new int[] { 1, 4 };

			var expected = new string[][] {
				new [] { "a" },
				new [] { "b", "c", "d" },
				new [] { "e", "f" }
			};

			var result = EnumerableEx.Slice(test, indexes).Select(c=>c.ToArray()).ToArray();

			var innerComparer = new ArrayEqualityComparer<string>();
			var comparer = new ArrayEqualityComparer<string[]>(innerComparer);

			Assert.IsTrue(comparer.Equals(expected, result));
		}

		[TestMethod]
		public void FlattenTest()
		{
			var test = new string[][] {
				new [] { "a" },
				new [] { "b", "c", "d" },
				new [] { "e", "f" }
			};

			var expected = new string[] { "a", "b", "c", "d", "e", "f" };

			var result = EnumerableEx.Flatten(test).ToArray();
			
			var comparer = new ArrayEqualityComparer<string>();
			Assert.IsTrue(comparer.Equals(expected, result));
		}
	}
}
