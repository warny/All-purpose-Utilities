using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Range;

namespace UtilsTest.Objects;

[TestClass]
public class CyclicRangeTests
{
    [TestMethod]
    public void ContainsUsesConfiguredCyclicRuleWhenStartIsGreaterThanEnd()
    {
        var range = new CyclicRange<int>(22, 2, 0, 23);

        Assert.IsTrue(range.Contains(1));
        Assert.IsTrue(range.Contains(2));
        Assert.IsFalse(range.Contains(10));
        Assert.IsTrue(range.Contains(22));
        Assert.IsTrue(range.Contains(23));
    }

    [TestMethod]
    public void RangesCanStoreInterfaceBasedRanges()
    {
        var ranges = new Ranges<int>();
        IRange<int> range = new Range<int>(5, 10);

        ranges.Add(range);

        Assert.AreEqual(1, ranges.Count);
        Assert.IsTrue(ranges.Contains(7));
    }

    [TestMethod]
    public void AddThrowsWhenMixingRegularAndCyclicRanges()
    {
        var ranges = new Ranges<int>();
        ranges.Add(new Range<int>(0, 3));

        Assert.ThrowsException<InvalidOperationException>(() => ranges.Add(new CyclicRange<int>(22, 2, 0, 23)));
    }

    [TestMethod]
    public void AddThrowsWhenMixingCyclicAndRegularRanges()
    {
        var ranges = new Ranges<int>();
        ranges.Add(new CyclicRange<int>(22, 2, 0, 23));

        Assert.ThrowsException<InvalidOperationException>(() => ranges.Add(new Range<int>(0, 3)));
    }

    [TestMethod]
    public void AddThrowsWhenCyclicRangesDoNotShareCycleBounds()
    {
        var ranges = new Ranges<int>();
        ranges.Add(new CyclicRange<int>(22, 2, 0, 23));

        Assert.ThrowsException<InvalidOperationException>(() => ranges.Add(new CyclicRange<int>(23, 1, 1, 24)));
    }

    [TestMethod]
    public void AddAllowsCyclicRangesWithSameBounds()
    {
        var ranges = new Ranges<int>();
        ranges.Add(new CyclicRange<int>(22, 2, 0, 23));

        ranges.Add(new CyclicRange<int>(22, 2, 0, 23));

        Assert.AreEqual(1, ranges.Count);
        Assert.IsTrue(ranges.Contains(2));
    }

    [TestMethod]
    public void IntersectReturnsTwoRangesWhenOverlapCrossesCycleBoundary()
    {
        var left = new CyclicRange<TimeOnly>(new TimeOnly(5, 0), new TimeOnly(19, 0), TimeOnly.MinValue, TimeOnly.MaxValue);
        var right = new CyclicRange<TimeOnly>(new TimeOnly(17, 0), new TimeOnly(7, 0), TimeOnly.MinValue, TimeOnly.MaxValue);

        var intersections = left.Intersect(right);

        Assert.AreEqual(2, intersections.Length);
        Assert.AreEqual(new TimeRange(new TimeOnly(5, 0), new TimeOnly(7, 0)), intersections[0]);
        Assert.AreEqual(new TimeRange(new TimeOnly(17, 0), new TimeOnly(19, 0)), intersections[1]);
    }

    [TestMethod]
    public void UnionMergesLinearRanges()
    {
        var left = new Ranges<int>(new Range<int>(1, 3));
        var right = new Ranges<int>(new Range<int>(3, 5));

        var union = left | right;

        Assert.AreEqual(1, union.Count);
        Assert.IsTrue(union.Contains(1));
        Assert.IsTrue(union.Contains(5));
    }

    [TestMethod]
    public void UnionPreservesTimeRangeModel()
    {
        var left = new Ranges<TimeOnly>(new TimeRange(new TimeOnly(5, 0), new TimeOnly(19, 0)));
        var right = new Ranges<TimeOnly>(new TimeRange(new TimeOnly(5, 0), new TimeOnly(19, 0)));

        var union = left | right;

        Assert.AreEqual(1, union.Count);
        Assert.IsTrue(union.Contains(new TimeOnly(7, 0)));
    }

    [TestMethod]
    public void UnionReturnsFullCycleWhenCoverageIsComplete()
    {
        var left = new Ranges<TimeOnly>(new TimeRange(new TimeOnly(5, 0), new TimeOnly(19, 0)));
        var right = new Ranges<TimeOnly>(new TimeRange(new TimeOnly(17, 0), new TimeOnly(7, 0)));

        var union = left | right;

        Assert.AreEqual(1, union.Count);
        Assert.AreEqual(new TimeRange(TimeOnly.MinValue, TimeOnly.MaxValue), union.Intervals[0]);
    }

    [TestMethod]
    public void AddThrowsWhenIntervalIsNull()
    {
        var ranges = new Ranges<int>();

        Assert.ThrowsException<ArgumentNullException>(() => ranges.Add((IRange<int>)null!));
    }

    [TestMethod]
    public void ToStringSupportsNonFormattableRanges()
    {
        var ranges = new Ranges<TimeOnly>(new TimeRange(new TimeOnly(22, 0), new TimeOnly(2, 0)));

        var text = ranges.ToString("HH:mm", null);

        Assert.AreEqual("Utils.Range.TimeRange", text);
    }

}
