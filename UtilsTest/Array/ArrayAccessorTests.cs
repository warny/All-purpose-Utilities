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
		var span = accessor.AsSpan([1, 2]);
		CollectionAssert.AreEqual(new[] { 20, 21, 22, 23 }, span.ToArray());
	}

	[TestMethod]
	public void AsSpanWithSingleIndex()
	{
		var accessor = CreateAccessor();
		var span = accessor.AsSpan([1]);
		CollectionAssert.AreEqual(Enumerable.Range(12, 12).ToArray(), span.ToArray());
	}

	[TestMethod]
	public void AsSpanWithoutIndexReturnsAll()
	{
		var accessor = CreateAccessor();
		var span = accessor.AsSpan(System.Array.Empty<int>());
		CollectionAssert.AreEqual(Enumerable.Range(0, 24).ToArray(), span.ToArray());
	}

	[TestMethod]
	public void AsSpanThrowsOnTooManyIndexes()
	{
		var accessor = CreateAccessor();
		Assert.ThrowsExactly<ArgumentException>(() => accessor.AsSpan([0, 1, 2, 3]));
	}

	[TestMethod]
	public void AsSpanThrowsOnIndexOutOfRange()
	{
		var accessor = CreateAccessor();
		Assert.ThrowsExactly<IndexOutOfRangeException>(() => accessor.AsSpan([2]));
	}
}

