using System.Collections.Generic;
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
}
