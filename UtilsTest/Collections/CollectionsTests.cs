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
			var right = new string [] { "a", "b", "c" };

			var result = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c"),
			};

			var test = CollectionUtils.Zip(left, right).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(result, test));	
		}

		[TestMethod]
		public void LongerLeftCollections()
		{
			var left = new int[] { 1, 2, 3, 4 };
			var right = new string[] { "a", "b", "c" };

			var result = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c"),
				(4, null)
			};

			var test = CollectionUtils.Zip(left, right).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(result, test));
		}

		[TestMethod]
		public void LongerRightCollections()
		{
			var left = new int[] { 1, 2, 3 };
			var right = new string[] { "a", "b", "c", "d" };

			var result = new (int, string)[] {
				(1, "a"),
				(2, "b"),
				(3, "c"),
				(0, "d")
			};

			var test = CollectionUtils.Zip(left, right).ToArray();

			var comparer = new ArrayEqualityComparer<(int, string)>();
			Assert.IsTrue(comparer.Equals(result, test));
		}
	}
}
