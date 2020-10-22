using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Utils.Arrays;

namespace UtilsTest.Array
{
	[TestClass]
	public class ArrayComparerTest
	{
		[TestMethod]
		public void CompareSimpleEqualsArrays()
		{
			var array1 = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
			var array2 = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

			ArrayEqualityComparer<int> comparer = new ArrayEqualityComparer<int>();
			Assert.IsTrue(comparer.Equals(array1, array2));
		}


		[TestMethod]
		public void CompareSimpleNonEqualsArrays()
		{
			var array1 = new[] { 1, 2, 3, 4, 5, 6, 7, 9, 8 };
			var array2 = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

			ArrayEqualityComparer<int> comparer = new ArrayEqualityComparer<int>();
			Assert.IsFalse(comparer.Equals(array1, array2));
		}

		[TestMethod]
		public void CompareNestedEqualsArrays()
		{
			var array1 = new[] {
				new[] { 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 2, 1, 2, 3, 4, 5, 6, 7, 8 },
				new[] { 3, 1, 2, 3, 4, 5, 6, 7 },
				new[] { 4, 1, 2, 3, 4, 5, 6 }
			};
			var array2 = new[] {
				new[] { 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 2, 1, 2, 3, 4, 5, 6, 7, 8 },
				new[] { 3, 1, 2, 3, 4, 5, 6, 7 },
				new[] { 4, 1, 2, 3, 4, 5, 6 }
			};

			ArrayEqualityComparer<int[]> comparer = new ArrayEqualityComparer<int[]>();
			Assert.IsTrue(comparer.Equals(array1, array2));
		}

		[TestMethod]
		public void CompareNestedNonEqualsArrays()
		{
			var array1 = new[] {
				new[] { 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 2, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 3, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 4, 1, 2, 3, 4, 5, 6, 7, 8, 9 }
			};
			var array2 = new[] {
				new[] { 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 2, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 4, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
				new[] { 3, 1, 2, 3, 4, 5, 6, 7, 8, 9 }
			};

			ArrayEqualityComparer<int[]> comparer = new ArrayEqualityComparer<int[]>();
			Assert.IsFalse(comparer.Equals(array1, array2));
		}
	}
}
