using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;
using Utils.Collections;
using Utils.Objects;

namespace UtilsTest.Lists
{
	[TestClass]
	public class SkipListTests
	{
		private static EnumerableEqualityComparer<int> Comparer { get; } = EnumerableEqualityComparer<int>.Default;

		[TestMethod]
		public void AddTest()
		{
			SkipList<int> list = [2, 3, 1];

			int[] result = [1, 2, 3];

			Assert.AreEqual(result, list, Comparer, string.Format($"[{string.Join(",", result)}] != [{string.Join(",", list)}]"));
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
			SkipList<int> list = new SkipList<int>(2);
			Random rng = new Random();
			//int[] result = [315257807,-841631303,140604031,830723831,1884102399,-584640210,-1263055896,-1587260726,1366135807,1312344665,-1303799965,-94870107,1990441821,-1847527116,1473890267,-1117619141,556307842,1170806024,720161059,-1200388556,-269357910,42367756,-1914091390,-1071643002,-1115613744];
			//foreach ((int item, int i) in result.Select((item, i) => (item, i)))
			//{
			//	Debug.WriteLine($"{i} : {item}");
			//	list.Add(item);
			//	Debug.WriteLine($"{i} : [{string.Join(",", list)}]");

			//}
			int[] result = rng.RandomArray(10000, (i) => rng.RandomInt());
			for (int i = 0; i < result.Length; i++)
			{
				list.Add(result[i]);
			}

			System.Array.Sort(result);

			Assert.AreEqual(result, list, Comparer, String.Format($"[{string.Join(",", result)}] != [{string.Join(",", list)}]"));

		}

		[TestMethod]
		public void ContainsTest()
		{
			Random rng = new Random();
			int[] result = rng.RandomArray(10000, (i) => rng.RandomInt());
			SkipList<int> list = new SkipList<int>();
			for (int i = 0; i < result.Length; i++)
			{
				list.Add(result[i]);
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
		[Ignore]
		public void RemoveTest()
		{
			SkipList<int> list = new SkipList<int>(2);
			Random rng = new Random();
			int[] result = rng.RandomArray(1000, (i) => rng.RandomInt());
			result = [.. result.Distinct()];
			for (int i = 0; i < result.Length; i++)
			{
				list.Add(result[i]);
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
