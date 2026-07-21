using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.Arrays;

namespace UtilsTest.Array;

[TestClass]
public class ArrayAccessorTests
{
    private static ArrayAccessor<int> CreateAccessor()
    {
        int[] data = [.. Enumerable.Range(0, 24)];
        return new ArrayAccessor<int>(data, 0, 2, 3, 4);
    }

    [TestMethod]
    public void IndexerReturnsCorrectValue()
    {
        var accessor = CreateAccessor();
        Assert.AreEqual(23, accessor[1, 2, 3]);
    }

    [TestMethod]
    public void AsSpanReturnsSubDimension()
    {
        var accessor = CreateAccessor();
        var span = accessor.AsSpan(1, 2);
        CollectionAssert.AreEqual(new[] { 20, 21, 22, 23 }, span.ToArray());
    }

    [TestMethod]
    public void AsSpanWithSingleIndex()
    {
        var accessor = CreateAccessor();
        var span = accessor.AsSpan(1);
        CollectionAssert.AreEqual(Enumerable.Range(12, 12).ToArray(), span.ToArray());
    }

    [TestMethod]
    public void AsSpanWithoutIndexReturnsAll()
    {
        var accessor = CreateAccessor();
        var span = accessor.AsSpan();
        CollectionAssert.AreEqual(Enumerable.Range(0, 24).ToArray(), span.ToArray());
    }

    [TestMethod]
    public void AsSpanThrowsOnTooManyIndexes()
    {
        var accessor = CreateAccessor();
        Assert.ThrowsExactly<ArgumentException>(() => accessor.AsSpan(0, 1, 2, 3));
    }

    [TestMethod]
    public void AsSpanThrowsOnIndexOutOfRange()
    {
        var accessor = CreateAccessor();
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => accessor.AsSpan(2));
    }

    [TestMethod]
    public void AsSpanAppliesOffset()
    {
        int[] data = [.. Enumerable.Range(0, 30)];
        var accessor = new ArrayAccessor<int>(data, 3, 2, 3, 4);
        var span = accessor.AsSpan(1);
        CollectionAssert.AreEqual(Enumerable.Range(15, 12).ToArray(), span.ToArray());
    }

    // #48 — virtual CheckSize() must not be called from the base constructor before Offset is assigned.

    [TestMethod]
    public void ConstructionFailsWhenOffsetPlusDimensionsExceedsArray()
    {
        // Array has 24 elements. offset=3, dims=2×3×4=24 → needs 27 elements → must throw.
        int[] data = [.. Enumerable.Range(0, 24)];
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArrayAccessor<int>(data, 3, 2, 3, 4));
    }

    [TestMethod]
    public void ConstructionSucceedsWhenOffsetPlusDimensionsFitsExactly()
    {
        // Array has 27 elements. offset=3, dims=2×3×4=24 → needs exactly 27 → must succeed.
        int[] data = [.. Enumerable.Range(0, 27)];
        var accessor = new ArrayAccessor<int>(data, 3, 2, 3, 4);
        Assert.AreEqual(3, accessor.Offset);
    }

    [TestMethod]
    public void ConstructionFailsWhenOffsetIsNegative()
    {
        int[] data = [.. Enumerable.Range(0, 30)];
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArrayAccessor<int>(data, -1, 2, 3, 4));
    }

    [TestMethod]
    public void IndexingUsesCorrectOffsetAfterConstruction()
    {
        // offset=3, dims=[3]: data[3]=3, data[4]=4, data[5]=5
        int[] data = [0, 1, 2, 3, 4, 5, 6, 7];
        var accessor = new ArrayAccessor<int>(data, 3, 3);
        Assert.AreEqual(3, accessor[0]);
        Assert.AreEqual(4, accessor[1]);
        Assert.AreEqual(5, accessor[2]);
    }
}

