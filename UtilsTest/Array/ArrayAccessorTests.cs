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

    // ------------------------------------------------------------------ #49 overflow in dimension arithmetic

    [TestMethod]
    public void Construction_ThrowsOnNegativeDimension()
    {
        int[] data = new int[100];
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArrayAccessor<int>(data, 0, 3, -1, 4));
    }

    [TestMethod]
    public void Construction_ThrowsOnZeroDimension()
    {
        int[] data = new int[100];
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArrayAccessor<int>(data, 0, 3, 0, 4));
    }

    [TestMethod]
    public void Construction_ThrowsOnOverflowingDimensionProduct()
    {
        // int.MaxValue / 2 * 3 overflows int but not long — should still throw (> int.MaxValue).
        int[] data = new int[10];
        Assert.ThrowsExactly<OverflowException>(() => new ArrayAccessor<int>(data, 0, int.MaxValue / 2, 3));
    }

    [TestMethod]
    public void Construction_ThrowsOnEmptyDimensions()
    {
        int[] data = new int[10];
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArrayAccessor<int>(data, 0));
    }

    // ------------------------------------------------------------------ #50 Sizes is a defensive copy

    [TestMethod]
    public void MutatingSizesArray_DoesNotAffectAccessorLayout()
    {
        int[] data = [.. Enumerable.Range(0, 24)];
        int[] dims = [2, 3, 4];
        var accessor = new ArrayAccessor<int>(data, 0, dims);

        // Mutate the original dimensions array.
        dims[0] = 99;
        dims[1] = 99;
        dims[2] = 99;

        // The accessor must still work correctly with the original layout.
        Assert.AreEqual(23, accessor[1, 2, 3]);
        Assert.AreEqual(2, accessor.Sizes[0]);
        Assert.AreEqual(3, accessor.Sizes[1]);
        Assert.AreEqual(4, accessor.Sizes[2]);
    }

    [TestMethod]
    public void Sizes_IsReadOnly_CannotBeDowncastToArray()
    {
        // Sizes must not be a raw int[] — callers must not be able to mutate it
        // through an array cast and thereby desync it from the internal caches (#50).
        int[] data = new int[6];
        var accessor = new ArrayAccessor<int>(data, 0, 2, 3);

        // The property type is IReadOnlyList<int>; a direct cast to int[] must fail.
        Assert.IsNotInstanceOfType<int[]>(accessor.Sizes,
            "Sizes must not expose a raw int[] that callers could mutate.");

        // Values are still readable through the interface.
        Assert.AreEqual(2, accessor.Sizes[0]);
        Assert.AreEqual(3, accessor.Sizes[1]);
        Assert.AreEqual(2, accessor.Sizes.Count);
    }

    // ------------------------------------------------------------------ #52 enumeration covers only the logical slice

    [TestMethod]
    public void Enumeration_CoversOnlyLogicalSlice()
    {
        // data = [0..29], offset=3, dims=[3,4]=12 elements → logical slice = data[3..14]
        int[] data = [.. Enumerable.Range(0, 30)];
        var accessor = new ArrayAccessor<int>(data, 3, 3, 4);

        int[] enumerated = [.. accessor];
        int[] expected = [.. Enumerable.Range(3, 12)];

        CollectionAssert.AreEqual(expected, enumerated,
            "Enumeration must yield only elements within the logical slice, not the entire backing array.");
    }

    [TestMethod]
    public void Enumeration_WithZeroOffset_CoversWholeLogicalRange()
    {
        int[] data = [.. Enumerable.Range(0, 24)];
        var accessor = new ArrayAccessor<int>(data, 0, 2, 3, 4);

        int[] enumerated = [.. accessor];
        CollectionAssert.AreEqual(Enumerable.Range(0, 24).ToArray(), enumerated);
    }
}

