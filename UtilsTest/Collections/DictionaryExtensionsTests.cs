using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Collections;

namespace UtilsTest.Collections;

[TestClass]
public class DictionaryExtensionsTests
{
    [TestMethod]
    public void GetOrAddValueAddsWhenMissing()
    {
        var dictionary = new Dictionary<int, string>();

        var first = dictionary.GetOrAdd(1, "initial");
        var second = dictionary.GetOrAdd(1, "ignored");

        Assert.AreEqual("initial", first);
        Assert.AreEqual("initial", second);
        Assert.AreEqual("initial", dictionary[1]);
    }

    [TestMethod]
    public void GetOrAddFactoryInvokesFactoryOnce()
    {
        var dictionary = new Dictionary<int, int>();
        int calls = 0;

        int first = dictionary.GetOrAdd(2, () =>
        {
            calls++;
            return 5;
        });
        int second = dictionary.GetOrAdd(2, () =>
        {
            calls++;
            return 7;
        });

        Assert.AreEqual(5, first);
        Assert.AreEqual(5, second);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void TryUpdateReturnsExpectedResult()
    {
        var dictionary = new Dictionary<int, string>
        {
            { 3, "old" },
        };

        bool updated = dictionary.TryUpdate(3, "new");
        bool notUpdated = dictionary.TryUpdate(4, "ignored");

        Assert.IsTrue(updated);
        Assert.IsFalse(notUpdated);
        Assert.AreEqual("new", dictionary[3]);
    }
}
