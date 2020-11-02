using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Lists;
using Utils.Objects;

namespace UtilsTest.Lists
{
	[TestClass]
	public class SkipListTest
	{
		ArrayEqualityComparer<int> comparer = new ArrayEqualityComparer<int>();

		[TestMethod]
		public void AddTest()
		{
			SkipList<int> list = new SkipList<int>();
			list.Add(2);
			list.Add(3);
			list.Add(1);

			int[] result = new[] { 1, 2, 3 };

			var value = new int[3];
			list.CopyTo(value, 0);

			Assert.IsTrue(comparer.Equals(result, value));
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

	}
}
