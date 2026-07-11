using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Objects;

namespace UtilsTest.Objects;

[TestClass]
public class ObjectUtilsTests
{
    [TestMethod]
    public void ComputeHashArrayWithNullElementDoesNotThrowTest()
    {
        System.Array array = new object[] { "a", null, "b" };
        int hash = array.ComputeHash();
        int enumerableHash = ((IEnumerable<object>)array).ComputeHash();
        Assert.AreEqual(enumerableHash, hash);
    }

    [TestMethod]
    public void ComputeHashArrayWithNullElementIsConsistentTest()
    {
        System.Array withNull = new object[] { "a", null, "b" };
        System.Array sameShape = new object[] { "a", null, "b" };
        Assert.AreEqual(withNull.ComputeHash(), sameShape.ComputeHash());
    }

    [TestMethod]
    public void ComputeHashMultidimensionalArrayWithNullElementDoesNotThrowTest()
    {
        var array = new object[2, 2];
        array[0, 0] = "a";
        array[0, 1] = null;
        array[1, 0] = "b";
        array[1, 1] = "c";

        int hash = ((System.Array)array).ComputeHash();
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public async Task DoAsyncWithSyncDelegatesReturnsIfNotNullResultTest()
    {
        string value = "hello";
        string result = await value.DoAsync(v => v.ToUpperInvariant(), () => "fallback");
        Assert.AreEqual("HELLO", result);
    }

    [TestMethod]
    public async Task DoAsyncWithSyncDelegatesReturnsIfNullResultTest()
    {
        string value = null;
        string result = await value.DoAsync(v => v.ToUpperInvariant(), () => "fallback");
        Assert.AreEqual("fallback", result);
    }

    [TestMethod]
    public async Task DoAsyncWithAsyncDelegatesComposesTasksWithoutThreadPoolOffloadTest()
    {
        int callingThreadId = Environment.CurrentManagedThreadId;
        int? observedThreadId = null;

        string value = "hello";
        string result = await value.DoAsync(
            async v =>
            {
                observedThreadId = Environment.CurrentManagedThreadId;
                await Task.Yield();
                return v.ToUpperInvariant();
            },
            async () =>
            {
                await Task.Yield();
                return "fallback";
            });

        Assert.AreEqual("HELLO", result);
        // Unlike the sync-delegate overload (which offloads via Task.Run), the async-delegate
        // overload invokes the delegate synchronously up to its first await, on the caller's thread.
        Assert.AreEqual(callingThreadId, observedThreadId);
    }

    [TestMethod]
    public async Task DoAsyncWithAsyncDelegateAndFallbackValueReturnsFallbackWhenNullTest()
    {
        string value = null;
        string result = await value.DoAsync(async v =>
        {
            await Task.Yield();
            return v.ToUpperInvariant();
        }, "fallback");

        Assert.AreEqual("fallback", result);
    }
}
