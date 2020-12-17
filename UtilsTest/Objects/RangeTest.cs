using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Arrays;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class RangeTest
	{
		ArrayEqualityComparer<int> comparer = new ArrayEqualityComparer<int>();

		[TestMethod]
		public void RangeTest1()
		{
			int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
			int[] expected = { 4, 5, 6 };
			var range = table.From(3, 3);
			Assert.IsTrue(comparer.Equals(expected, range));
		}

		[TestMethod]
		public void RangeTest2()
		{
			int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
			int[] expected = { 6, 7, 8, 9, 10 };
			var range = table.From(5);
			Assert.IsTrue(comparer.Equals(expected, range));
		}

		[TestMethod]
		public void RangeTest3()
		{
			int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
			int[] expected = { 1, 2, 3, 4, 5 };
			var range = table.To(5);
			Assert.IsTrue(comparer.Equals(expected, range));
		}

		[TestMethod]
		public void RangeTest4()
		{
			int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
			int[] expected = { 4, 5, 6, 7 };
			var range = table.Between(3, 6);
			Assert.IsTrue(comparer.Equals(expected, range));
		}
	}
}
