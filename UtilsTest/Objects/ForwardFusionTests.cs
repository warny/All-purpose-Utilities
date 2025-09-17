using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Collections;

namespace UtilsTest.Objects
{
	[TestClass]
	public class ForwardFusionTests
	{

		[TestMethod]
		public void FusionTest1()
		{
			int[] list1 = { 1, 3, 5, 6 };
			int[] list2 = { 1, 1, 2, 2, 3, 3, 6 };

			(int Left, int Right)[] expected = {
				(1, 1),
				(1, 1),
				(3, 3),
				(3, 3),
				(6, 6)
			};

			var fusion = new ForwardFusion<int, int>(list1, list2);
			var result = fusion.ToArray();

			var comparer = new EnumerableEqualityComparer<(int Left, int Right)>((i1, i2)=>i1.Left == i2.Left && i1.Right == i2.Right);

			Assert.AreEqual(expected, result, comparer);

		}

		[TestMethod]
		public void FusionTest2()
		{
			int[] list1 = { 3, 5, 6 };
			int[] list2 = { 1, 1, 2, 2, 3, 3, 6 };

			(int Left, int Right)[] expected = {
				(3, 3),
				(3, 3),
				(6, 6)
			};

			var fusion = new ForwardFusion<int, int>(list1, list2);
			var result = fusion.ToArray();

			var comparer = new EnumerableEqualityComparer<(int Left, int Right)>((i1, i2) => i1.Left == i2.Left && i1.Right == i2.Right);

			Assert.AreEqual(expected, result, comparer);

		}

		[TestMethod]
		public void FusionTest3()
		{
			int[] list1 = { 1, 3, 5, 6 };
			int[] list2 = { 2, 2, 3, 3, 6 };

			(int Left, int Right)[] expected = {
				(3, 3),
				(3, 3),
				(6, 6)
			};

			var fusion = new ForwardFusion<int, int>(list1, list2);
			var result = fusion.ToArray();

			var comparer = new EnumerableEqualityComparer<(int Left, int Right)>((i1, i2) => i1.Left == i2.Left && i1.Right == i2.Right);

			Assert.AreEqual(expected, result, comparer);
		}

		[TestMethod]
		public void FusionTest4()
		{
			int[] list1 = { 1, 3, 5 };
			int[] list2 = { 1, 1, 2, 2, 3, 3, 6 };

			(int Left, int Right)[] expected = {
				(1, 1),
				(1, 1),
				(3, 3),
				(3, 3)
			};

			var fusion = new ForwardFusion<int, int>(list1, list2);
			var result = fusion.ToArray();

			var comparer = new EnumerableEqualityComparer<(int Left, int Right)>((i1, i2) => i1.Left == i2.Left && i1.Right == i2.Right);

			Assert.AreEqual(expected, result, comparer);
		}

		[TestMethod]
		public void FusionTest5()
		{
			int[] list1 = { 1, 3, 5, 6 };
			int[] list2 = { 1, 1, 2, 2, 3, 3 };

			(int Left, int Right)[] expected = {
				(1, 1),
				(1, 1),
				(3, 3),
				(3, 3)
			};

			var fusion = new ForwardFusion<int, int>(list1, list2);
			var result = fusion.ToArray();

			var comparer = new EnumerableEqualityComparer<(int Left, int Right)>((i1, i2) => i1.Left == i2.Left && i1.Right == i2.Right);

			Assert.AreEqual(expected, result, comparer);
		}

		[TestMethod]
		public void FusionTest6()
		{
			int[] list1 = { 1, 3, 5, 6 };
			int[] list2 = { 1, 1, 2, 2, 3, 3, 6 };

			(int Left, int Right)[] expected = {
				(1, 1),
				(1, 1),
				(3, 3),
				(3, 3),
				(6, 6)
			};

			var itemComparer = Comparer<int>.Default;

			var fusion = new ForwardFusion<int, int>(list1, list2, itemComparer);
			var result = fusion.ToArray();

			var comparer = new EnumerableEqualityComparer<(int Left, int Right)>((i1, i2) => i1.Left == i2.Left && i1.Right == i2.Right);

			Assert.AreEqual(expected, result, comparer);

		}


		[TestMethod]
		public void FusionTest7()
		{
			int[] list1 = { 1, 3, 5, 6 };
			int[] list2 = { 1, 1, 2, 2, 3, 3, 6 };

			(int Left, int Right)[] expected = {
				(1, 1),
				(1, 1),
				(3, 3),
				(3, 3),
				(6, 6)
			};

			var fusion = new ForwardFusion<int, int>(list1, list2, (l, r) => l.CompareTo(r));
			var result = fusion.ToArray();

			var comparer = new EnumerableEqualityComparer<(int Left, int Right)>((i1, i2) => i1.Left == i2.Left && i1.Right == i2.Right);

			Assert.AreEqual(expected, result, comparer);

		}

	}
}
