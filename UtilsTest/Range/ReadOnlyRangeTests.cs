using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.Range;

namespace UtilsTest.Range;

[TestClass]
public class ReadOnlyRangeTests
{
    private static IReadOnlyList<int> List(params int[] values) => values;

    [TestMethod]
    public void OutOfRange_EndIndex_ThrowsWithCorrectParamName()
    {
        var list = List(0, 1, 2, 3, 4);
        var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => list.Between(0, 10)); // endIndex=10 >= Count=5

        // Regression: was incorrectly reporting "startIndex" instead of "endIndex".
        Assert.AreEqual("endIndex", ex.ParamName);
    }

    [TestMethod]
    public void Range_ForwardStep_ReturnsCorrectElements()
    {
        var list = List(10, 20, 30, 40, 50);
        var range = list.Between(0, 4, 2); // indices 0,2,4 → 10,30,50
        Assert.AreEqual(3, range.Count);
        Assert.AreEqual(10, range[0]);
        Assert.AreEqual(30, range[1]);
        Assert.AreEqual(50, range[2]);
    }
}
