using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;
using Utils.Randomization;

namespace UtilsTest.Lists
{
    [TestClass]
    public class SkipListDictionaryTests
    {
        [TestMethod]
        public void AddAndRetrieve_BasicOperations()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("b", 2);
            dict.Add("a", 1);
            dict.Add("c", 3);

            Assert.AreEqual(3, dict.Count);
            Assert.AreEqual(1, dict["a"]);
            Assert.AreEqual(2, dict["b"]);
            Assert.AreEqual(3, dict["c"]);
        }

        [TestMethod]
        public void Keys_AreSortedInAscendingOrder()
        {
            var dict = new SkipListDictionary<int, string>();
            dict.Add(30, "thirty");
            dict.Add(10, "ten");
            dict.Add(20, "twenty");

            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, dict.Keys.ToArray());
        }

        [TestMethod]
        public void Enumeration_YieldsEntriesInKeyOrder()
        {
            var dict = new SkipListDictionary<int, string>();
            dict.Add(30, "thirty");
            dict.Add(10, "ten");
            dict.Add(20, "twenty");

            var pairs = dict.ToArray();
            Assert.AreEqual(3, pairs.Length);
            Assert.AreEqual(10, pairs[0].Key);
            Assert.AreEqual(20, pairs[1].Key);
            Assert.AreEqual(30, pairs[2].Key);
            Assert.AreEqual("ten", pairs[0].Value);
        }

        [TestMethod]
        public void Indexer_SetUpdatesValueInPlace()
        {
            var dict = new SkipListDictionary<string, int>();
            dict["a"] = 1;
            dict["a"] = 42;

            Assert.AreEqual(42, dict["a"]);
            Assert.AreEqual(1, dict.Count);
        }

        [TestMethod]
        public void Indexer_SetAddsIfKeyAbsent()
        {
            var dict = new SkipListDictionary<string, int>();
            dict["x"] = 99;

            Assert.AreEqual(1, dict.Count);
            Assert.AreEqual(99, dict["x"]);
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void Indexer_GetMissingKey_Throws()
        {
            var dict = new SkipListDictionary<string, int>();
            _ = dict["missing"];
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Add_DuplicateKey_Throws()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("a", 1);
            dict.Add("a", 2);
        }

        [TestMethod]
        public void TryGetValue_ExistingKey_ReturnsTrue()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("a", 42);

            Assert.IsTrue(dict.TryGetValue("a", out var value));
            Assert.AreEqual(42, value);
        }

        [TestMethod]
        public void TryGetValue_MissingKey_ReturnsFalse()
        {
            var dict = new SkipListDictionary<string, int>();

            Assert.IsFalse(dict.TryGetValue("z", out _));
        }

        [TestMethod]
        public void ContainsKey_ReturnsTrueForExistingKey()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("hello", 1);

            Assert.IsTrue(dict.ContainsKey("hello"));
            Assert.IsFalse(dict.ContainsKey("world"));
        }

        [TestMethod]
        public void Contains_KeyValuePair_ChecksBothKeyAndValue()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("a", 1);

            Assert.IsTrue(dict.Contains(new KeyValuePair<string, int>("a", 1)));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, int>("a", 99)));
            Assert.IsFalse(dict.Contains(new KeyValuePair<string, int>("z", 1)));
        }

        [TestMethod]
        public void Remove_ByKey_RemovesEntry()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("a", 1);
            dict.Add("b", 2);

            Assert.IsTrue(dict.Remove("a"));
            Assert.AreEqual(1, dict.Count);
            Assert.IsFalse(dict.ContainsKey("a"));
            Assert.IsTrue(dict.ContainsKey("b"));
        }

        [TestMethod]
        public void Remove_MissingKey_ReturnsFalse()
        {
            var dict = new SkipListDictionary<string, int>();
            Assert.IsFalse(dict.Remove("ghost"));
        }

        [TestMethod]
        public void Remove_KeyValuePair_OnlyRemovesWhenValueMatches()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("a", 1);

            Assert.IsFalse(dict.Remove(new KeyValuePair<string, int>("a", 99)));
            Assert.IsTrue(dict.ContainsKey("a"));

            Assert.IsTrue(dict.Remove(new KeyValuePair<string, int>("a", 1)));
            Assert.IsFalse(dict.ContainsKey("a"));
        }

        [TestMethod]
        public void Clear_RemovesAllEntries()
        {
            var dict = new SkipListDictionary<string, int>();
            dict.Add("a", 1);
            dict.Add("b", 2);
            dict.Clear();

            Assert.AreEqual(0, dict.Count);
            Assert.IsFalse(dict.ContainsKey("a"));
            Assert.IsFalse(dict.ContainsKey("b"));
        }

        [TestMethod]
        public void CopyTo_CopiesAllEntriesInOrder()
        {
            var dict = new SkipListDictionary<int, string>();
            dict.Add(3, "c");
            dict.Add(1, "a");
            dict.Add(2, "b");

            var array = new KeyValuePair<int, string>[3];
            dict.CopyTo(array, 0);

            Assert.AreEqual(1, array[0].Key);
            Assert.AreEqual(2, array[1].Key);
            Assert.AreEqual(3, array[2].Key);
        }

        [TestMethod]
        public void Values_ReflectsCurrentState()
        {
            var dict = new SkipListDictionary<int, string>();
            dict.Add(2, "b");
            dict.Add(1, "a");
            dict.Add(3, "c");

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, dict.Values.ToArray());
        }

        [TestMethod]
        public void Values_Contains_FindsByEquality()
        {
            var dict = new SkipListDictionary<int, string>();
            dict.Add(1, "hello");

            Assert.IsTrue(dict.Values.Contains("hello"));
            Assert.IsFalse(dict.Values.Contains("world"));
        }

        [TestMethod]
        public void CustomComparer_ReversedOrder()
        {
            var dict = new SkipListDictionary<int, string>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
            dict.Add(1, "one");
            dict.Add(3, "three");
            dict.Add(2, "two");

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, dict.Keys.ToArray());
        }

        [TestMethod]
        public void LargeInsert_MaintainsOrder()
        {
            var dict = new SkipListDictionary<int, int>(threshold: 5);
            var rng = new Random(42);
            var keys = Enumerable.Range(0, 1000).OrderBy(_ => rng.Next()).ToList();

            foreach (var key in keys)
                dict.Add(key, key * 2);

            var resultKeys = dict.Keys.ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, 1000).ToArray(), resultKeys);

            foreach (var key in keys)
                Assert.AreEqual(key * 2, dict[key]);
        }

        [TestMethod]
        public void LargeInsertAndRemove_MaintainsConsistency()
        {
            var dict = new SkipListDictionary<int, int>(threshold: 5);
            var rng = new Random(0);
            var keys = rng.RandomArray(500, _ => rng.RandomInt()).Distinct().ToArray();

            foreach (var key in keys)
                dict.Add(key, key);

            foreach (var key in keys)
                Assert.IsTrue(dict.Remove(key), $"Remove failed for key {key}");

            Assert.AreEqual(0, dict.Count);
        }
    }
}
