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
