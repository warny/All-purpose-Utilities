using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Utils.Collections;

namespace UtilsTest.Collections;

[TestClass]
public class GetOrAddThreadSafetyTests
{
    [TestMethod]
    public async Task GetOrAdd_AllowsConcurrentAccess()
    {
        var dictionary = new Dictionary<int, int>();

        async Task<int> AddAsync()
        {
            return await Task.Run(() => dictionary.GetOrAdd(0, () => 1));
        }

        var tasks = Enumerable.Range(0, 20).Select(_ => AddAsync());
        var results = await Task.WhenAll(tasks);

        CollectionAssert.AreEqual(Enumerable.Repeat(1, 20).ToArray(), results);
        Assert.AreEqual(1, dictionary.Count);
        Assert.AreEqual(1, dictionary[0]);
    }

    [TestMethod]
    public void GetOrAdd_IsMarkedAsSynchronized()
    {
        var methods = typeof(DictionaryExtensions).GetMethods()
            .Where(m => m.Name == "GetOrAdd");

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<MethodImplAttribute>();
            Assert.IsNotNull(attr);
            Assert.IsTrue(attr.Value.HasFlag(MethodImplOptions.Synchronized));
        }
    }
}

