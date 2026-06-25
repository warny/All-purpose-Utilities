using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;
using Utils.Collections;
using Utils.Randomization;

namespace UtilsTest.Lists
{
    [TestClass]
    public class SkipListTests
    {
        private static EnumerableEqualityComparer<int> Comparer { get; } = EnumerableEqualityComparer<int>.Default;

        private static void TestAdd(int threshold, int[] result)
        {
            SkipList<int> list = new SkipList<int>(threshold);
            for (int i = 0; i < result.Length; i++)
            {
                list.Add(result[i]);
            }
            System.Array.Sort(result);

            Assert.AreEqual(result, list, Comparer, string.Format($"[{string.Join(",", result)}] != [{string.Join(",", list)}]"));
        }

        [TestMethod]
        public void AddTest()
        {
            TestAdd(10, [2, 3, 1]);
        }

        [TestMethod]
        public void AddTestWithLevels()
        {
            TestAdd(3, [5, 6, 1, 2, 0, 3, 4, 7]);
        }

        [TestMethod]
        public void AddTestLargeArray()
        {
            Random rng = new Random();
            int[] result = rng.RandomArray(10000, (i) => rng.RandomInt());
            TestAdd(5, result);
        }

        private static void TestContains(int threshold, int[] result)
        {
            SkipList<int> list = new SkipList<int>(threshold);
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
        public void ContainsTest()
        {
            TestContains(10, [3, 1, 2]);
        }

        [TestMethod]
        public void ContainsTestWithLevels()
        {
            TestContains(3, [5, 6, 1, 2, 0, 3, 4, 7]);
        }


        [TestMethod]
        public void ContainsTestLargeArray()
        {
            Random rng = new Random();
            int[] result = rng.RandomArray(10000, (i) => rng.RandomInt());
            TestContains(5, result);

        }

        private static void TestRemove(int threshold, int[] result)
        {
            SkipList<int> list = new SkipList<int>(threshold);
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

        [TestMethod]
        public void RemoveTest()
        {
            TestRemove(10, [1, 2, 3]);
            TestRemove(10, [3, 2, 1]);
            TestRemove(10, [3, 1, 2]);
            TestRemove(10, [2, 1, 3]);
            TestRemove(10, [2, 3, 1]);
            TestRemove(10, [1, 3, 2]);
        }

        [TestMethod]
        public void RemoveTestWithLevels()
        {
            TestRemove(3, [5, 6, 1, 2, 0, 3, 4, 7]);
        }

        [TestMethod]
        public void RemoveTestLargeArray()
        {
            Random rng = new Random();
            int[] result = rng.RandomArray(1000, (i) => rng.RandomInt());
            result = [.. result.Distinct()];
            TestRemove(5, result);
        }

        [TestMethod]
        public void AddBeforeFirstWithLevels_MaintainsStructure()
        {
            SkipList<int> list = new(2);
            foreach (var v in new[] { 5, 6, 7, 8 }) list.Add(v);
            list.Add(1);
            list.Add(0);
            CollectionAssert.AreEqual(new[] { 0, 1, 5, 6, 7, 8 }, list.ToArray());
        }

        [TestMethod]
        public void RemoveFirstElementWithUpLinks_MaintainsStructure()
        {
            SkipList<int> list = new(2);
            foreach (var v in new[] { 1, 2, 3, 4, 5, 6 }) list.Add(v);
            Assert.IsTrue(list.Remove(1));
            Assert.AreEqual(5, list.Count);
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6 }, list.ToArray());
            list.Add(0);
            Assert.IsTrue(list.Contains(0));
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 6 }, list.ToArray());
        }

        [TestMethod]
        public void RemoveLastElementWithUpLinks_MaintainsStructure()
        {
            SkipList<int> list = new(2);
            foreach (var v in new[] { 1, 2, 3, 4, 5, 6 }) list.Add(v);
            Assert.IsTrue(list.Remove(6));
            Assert.AreEqual(5, list.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, list.ToArray());
            list.Add(7);
            Assert.IsTrue(list.Contains(7));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 7 }, list.ToArray());
        }

        [TestMethod]
        public void AddAndContains_WithSmallThreshold_NoSkipNodeBeyondEnd()
        {
            SkipList<int> list = new(2);
            int[] values = [10, 20, 30, 40, 50];
            foreach (var v in values) list.Add(v);
            foreach (var v in values) Assert.IsTrue(list.Contains(v));
            CollectionAssert.AreEqual(values, list.ToArray());
        }

    }
}
