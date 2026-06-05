using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
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
}
