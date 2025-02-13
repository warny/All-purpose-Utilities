using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Collections;
using Utils.Collections;
using Utils.Objects;

namespace UtilsTest.Lists
{
	[TestClass]
	[Ignore]
	public class SkipListTests
	{
		private static EnumerableEqualityComparer<int> comparer { get; } = EnumerableEqualityComparer<int>.Default;

		[TestMethod]
		public void AddTest()
		{
			SkipList<int> list = [2, 3, 1];

			int[] result = [1, 2, 3];

			var value = new int[3];
			list.CopyTo(value, 0);

			Assert.AreEqual(result, value, comparer);
		}

		[TestMethod]
		public void SortTimeCompute() {
			Random rng = new Random();
			int[] result = new int[10000];
			for (int i = 0; i < result.Length; i++)
			{
				var number = rng.RandomInt();
				result[i] = number;
			}
			System.Array.Sort(result);
		}

		[TestMethod]
		public void LargeArrayTest()
		{
			SkipList<int> list = new SkipList<int>();
			Random rng = new Random();
			int[] result = new int[10000];
			for (int i = 0; i < result.Length; i++)
			{
				var number = rng.RandomInt();
				result[i] = number;
				list.Add(number);
			}
			System.Array.Sort(result);

			var value = new int[result.Length];
			list.CopyTo(value, 0);

			Assert.IsTrue(comparer.Equals(result, value));

		}

		[TestMethod]
		public void ContainsTest()
		{
			SkipList<int> list = new SkipList<int>();
			Random rng = new Random();
			int[] result = new int[10000];
			for (int i = 0; i < result.Length; i++)
			{
				var number = rng.RandomInt();
				if (number == 0) Debugger.Break();
				result[i] = number;
				list.Add(number);
			}
			System.Array.Sort(result);

			foreach (var item in result)
			{
				bool test = list.Contains(item);
				if (!test) Debugger.Break();
				Assert.IsTrue(test, $"{item} was not found");
			}

		}

		[TestMethod]
		public void RemoveTest()
		{
			SkipList<int> list = new SkipList<int>();
			Random rng = new Random();
			int[] result = new int[10000];
			for (int i = 0; i < result.Length; i++)
			{
				var number = rng.RandomInt();
				result[i] = number;
				list.Add(number);
			}

			foreach (var item in result)
			{
				Assert.IsTrue(list.Remove(item));
			}

			foreach (var item in result)
			{
				Assert.IsFalse(list.Contains(item));
			}
		}

	}
}
