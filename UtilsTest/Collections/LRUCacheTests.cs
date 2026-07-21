using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Utils.Collections;

namespace UtilsTest.Collections;

[TestClass]
public class LRUCacheTests
{
    [TestMethod]
    public void Add_Get_BasicRoundTrip()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        Assert.AreEqual("one", cache[1]);
    }

    [TestMethod]
    public void Evicts_LeastRecentlyUsed_WhenCapacityExceeded()
    {
        var cache = new LRUCache<int, string>(3);
        cache.Add(1, "one");
        cache.Add(2, "two");
        cache.Add(3, "three");
        _ = cache[1]; // access 1 → 2 becomes LRU
        cache.Add(4, "four"); // evicts 2

        Assert.IsFalse(cache.ContainsKey(2), "LRU key 2 should be evicted");
        Assert.IsTrue(cache.ContainsKey(1));
        Assert.IsTrue(cache.ContainsKey(3));
        Assert.IsTrue(cache.ContainsKey(4));
    }

    [TestMethod]
    public void Keys_IsLiveView_ReflectsChanges()
    {
        var cache = new LRUCache<int, string>(5);
        ICollection<int> keys = cache.Keys;

        cache.Add(1, "a");
        cache.Add(2, "b");

        // The view must reflect additions without re-fetching Keys.
        Assert.AreEqual(2, keys.Count);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, keys.ToArray());
    }

    [TestMethod]
    public void Values_IsLiveView_ReflectsChanges()
    {
        var cache = new LRUCache<int, string>(5);
        ICollection<string> values = cache.Values;

        cache.Add(1, "hello");
        cache.Add(2, "world");

        Assert.AreEqual(2, values.Count);
        CollectionAssert.AreEquivalent(new[] { "hello", "world" }, values.ToArray());
    }

    [TestMethod]
    public void Keys_Contains_FindsExistingKey()
    {
        var cache = new LRUCache<string, int>(5);
        cache.Add("x", 42);
        Assert.IsTrue(cache.Keys.Contains("x"));
        Assert.IsFalse(cache.Keys.Contains("y"));
    }

    [TestMethod]
    public void Clear_RemovesAllEntries()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        cache.Add(2, "two");

        cache.Clear();

        Assert.AreEqual(0, cache.Count);
        Assert.IsFalse(cache.ContainsKey(1));
        Assert.IsFalse(cache.ContainsKey(2));
    }

    [TestMethod]
    public void PublicMembers_AreNotMarkedSynchronized()
    {
        // LRUCache<K,V> synchronizes through an explicit internal lock object, not
        // MethodImplOptions.Synchronized (which locks on `this` — a public reference other code
        // could also lock on, risking contention or deadlocks). Guard against it creeping back in.
        var type = typeof(LRUCache<int, string>);
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            bool isSynchronized = (method.MethodImplementationFlags & MethodImplAttributes.Synchronized) != 0;
            Assert.IsFalse(isSynchronized, $"{method.Name} should not be marked MethodImplOptions.Synchronized.");
        }
    }

    [TestMethod]
    public void ConcurrentAddsWithDistinctKeys_AllSucceedAndAreRetrievable()
    {
        const int threadCount = 8;
        const int keysPerThread = 200;
        var cache = new LRUCache<int, int>(threadCount * keysPerThread);

        Parallel.For(0, threadCount, t =>
        {
            for (int i = 0; i < keysPerThread; i++)
            {
                int key = t * keysPerThread + i;
                cache.Add(key, key * 2);
            }
        });

        Assert.AreEqual(threadCount * keysPerThread, cache.Count);
        for (int t = 0; t < threadCount; t++)
        {
            for (int i = 0; i < keysPerThread; i++)
            {
                int key = t * keysPerThread + i;
                Assert.IsTrue(cache.TryGetValue(key, out int value), $"key {key} should be retrievable");
                Assert.AreEqual(key * 2, value);
            }
        }
    }

    // ------------------------------------------------------------------ #14 Capacity validation

    [TestMethod]
    public void Constructor_ThrowsOnZeroCapacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LRUCache<int, string>(0));
    }

    [TestMethod]
    public void Constructor_ThrowsOnNegativeCapacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LRUCache<int, string>(-1));
    }

    [TestMethod]
    public void Count_NeverExceedsCapacity()
    {
        var cache = new LRUCache<int, int>(3);
        for (int i = 0; i < 10; i++)
            cache.Add(i, i);
        Assert.IsTrue(cache.Count <= 3, $"Count={cache.Count} exceeded capacity 3.");
    }

    // ------------------------------------------------------------------ #15 Indexer throws KeyNotFoundException

    [TestMethod]
    public void Indexer_ThrowsKeyNotFoundForMissingKey()
    {
        var cache = new LRUCache<int, string>(5);
        Assert.ThrowsExactly<KeyNotFoundException>(() => _ = cache[99]);
    }

    [TestMethod]
    public void TryGetValue_ReturnsFalseForMissingKey_DoesNotThrow()
    {
        var cache = new LRUCache<int, string>(5);
        bool found = cache.TryGetValue(99, out string val);
        Assert.IsFalse(found);
        Assert.IsNull(val);
    }

    // ------------------------------------------------------------------ #16 Remove(KeyValuePair) requires value match

    [TestMethod]
    public void Remove_KeyValuePair_MatchingPair_Removes()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        bool result = cache.Remove(new KeyValuePair<int, string>(1, "one"));
        Assert.IsTrue(result);
        Assert.IsFalse(cache.ContainsKey(1));
    }

    [TestMethod]
    public void Remove_KeyValuePair_WrongValue_DoesNotRemove()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        bool result = cache.Remove(new KeyValuePair<int, string>(1, "wrong"));
        Assert.IsFalse(result, "Remove should return false when value does not match.");
        Assert.IsTrue(cache.ContainsKey(1), "Entry should still exist after failed value-match removal.");
    }

    [TestMethod]
    public void Remove_KeyValuePair_MissingKey_ReturnsFalse()
    {
        var cache = new LRUCache<int, string>(5);
        bool result = cache.Remove(new KeyValuePair<int, string>(99, "anything"));
        Assert.IsFalse(result);
    }

    // ------------------------------------------------------------------ #17 CopyTo upfront validation

    [TestMethod]
    public void CopyTo_ThrowsArgumentNullException_OnNullArray()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        Assert.ThrowsExactly<ArgumentNullException>(() => cache.CopyTo(null!, 0));
    }

    [TestMethod]
    public void CopyTo_ThrowsArgumentOutOfRangeException_OnNegativeIndex()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => cache.CopyTo(new KeyValuePair<int, string>[5], -1));
    }

    [TestMethod]
    public void CopyTo_ThrowsArgumentException_OnInsufficientSpace()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        cache.Add(2, "two");
        // Array has 3 slots but we start at index 2 → only 1 slot left for 2 items.
        var dest = new KeyValuePair<int, string>[3];
        Assert.ThrowsExactly<ArgumentException>(() => cache.CopyTo(dest, 2));
    }

    [TestMethod]
    public void CopyTo_ThrowsBeforePartialWrite()
    {
        // Verify no partial write occurs when the array is too small.
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        cache.Add(2, "two");
        var dest = new KeyValuePair<int, string>[1]; // only 1 slot for 2 items
        try { cache.CopyTo(dest, 0); } catch (ArgumentException) { }
        // Destination must remain unwritten.
        Assert.AreEqual(default, dest[0]);
    }

    [TestMethod]
    public void Keys_CopyTo_ThrowsArgumentNullException_OnNullArray()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        Assert.ThrowsExactly<ArgumentNullException>(() => cache.Keys.CopyTo(null!, 0));
    }

    [TestMethod]
    public void Values_CopyTo_ThrowsArgumentNullException_OnNullArray()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        Assert.ThrowsExactly<ArgumentNullException>(() => cache.Values.CopyTo(null!, 0));
    }

    [TestMethod]
    public void Keys_CopyTo_ThrowsArgumentException_OnInsufficientSpace()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        cache.Add(2, "two");
        Assert.ThrowsExactly<ArgumentException>(() => cache.Keys.CopyTo(new int[1], 0));
    }

    [TestMethod]
    public void Values_CopyTo_ThrowsArgumentException_OnInsufficientSpace()
    {
        var cache = new LRUCache<int, string>(5);
        cache.Add(1, "one");
        cache.Add(2, "two");
        Assert.ThrowsExactly<ArgumentException>(() => cache.Values.CopyTo(new string[1], 0));
    }

    [TestMethod]
    public void ConcurrentReadWriteEnumerateStress_DoesNotThrow()
    {
        var cache = new LRUCache<int, int>(50);
        const int threadCount = 8;
        const int opsPerThread = 2000;
        var exceptions = new ConcurrentBag<Exception>();

        Parallel.For(0, threadCount, t =>
        {
            try
            {
                var rnd = new Random(t);
                for (int i = 0; i < opsPerThread; i++)
                {
                    int key = rnd.Next(0, 100);
                    switch (rnd.Next(6))
                    {
                        case 0:
                            cache[key] = key; // upsert: safe to call concurrently with the same key
                            break;
                        case 1:
                            cache.TryGetValue(key, out _);
                            break;
                        case 2:
                            cache.Remove(key);
                            break;
                        case 3:
                            _ = cache.Keys.Count;
                            foreach (var _ in cache.Keys) { }
                            break;
                        case 4:
                            foreach (var _ in cache.Values) { }
                            break;
                        case 5:
                            foreach (var _ in cache) { }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.IsTrue(exceptions.IsEmpty, string.Join(Environment.NewLine, exceptions));
        Assert.IsTrue(cache.Count <= 50);
    }
}
