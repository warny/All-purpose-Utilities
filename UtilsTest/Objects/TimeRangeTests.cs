using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Range;

namespace UtilsTest.Objects;

[TestClass]
public class TimeRangeTests
{
    [TestMethod]
    public void ContainsReturnsTrueInsideStandardRange()
    {
        var range = new TimeRange(new TimeOnly(9, 0), new TimeOnly(17, 0));

        Assert.IsTrue(range.Contains(new TimeOnly(9, 0)));
        Assert.IsTrue(range.Contains(new TimeOnly(12, 30)));
        Assert.IsTrue(range.Contains(new TimeOnly(17, 0)));
    }

    [TestMethod]
    public void ContainsReturnsFalseOutsideStandardRange()
    {
        var range = new TimeRange(new TimeOnly(9, 0), new TimeOnly(17, 0));

        Assert.IsFalse(range.Contains(new TimeOnly(8, 59)));
        Assert.IsFalse(range.Contains(new TimeOnly(17, 1)));
    }

    [TestMethod]
    public void ContainsUsesSpecifiedRuleWhenStartIsGreaterThanEnd()
    {
        var range = new TimeRange(new TimeOnly(22, 0), new TimeOnly(2, 0));

        Assert.IsTrue(range.Contains(new TimeOnly(1, 0)));
        Assert.IsTrue(range.Contains(new TimeOnly(2, 0)));
        Assert.IsTrue(range.Contains(new TimeOnly(22, 0)));
        Assert.IsTrue(range.Contains(new TimeOnly(23, 59)));
    }

    [TestMethod]
    public void ContainsReturnsTrueOnlyForExactValueWhenBoundsAreEqual()
    {
        var range = new TimeRange(new TimeOnly(10, 15), new TimeOnly(10, 15));

        Assert.IsTrue(range.Contains(new TimeOnly(10, 15)));
        Assert.IsFalse(range.Contains(new TimeOnly(10, 14)));
        Assert.IsFalse(range.Contains(new TimeOnly(10, 16)));
    }
}
