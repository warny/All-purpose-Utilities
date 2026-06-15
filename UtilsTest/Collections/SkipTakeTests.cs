using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;

namespace UtilsTest.Collections;

[TestClass]
public class SkipTakeTests
{
    // ── Array fast path ──────────────────────────────────────────────────────

    [TestMethod]
    public void Array_SkipTake_ReturnsSlice()
    {
        int[] source = [0, 1, 2, 3, 4, 5, 6];
        CollectionAssert.AreEqual(new[] { 2, 3, 4 }, source.SkipTake(2, 3).ToArray());
    }

    [TestMethod]
    public void Array_TakeExceedsLength_ClampedAtEnd()
    {
        int[] source = [0, 1, 2];
        CollectionAssert.AreEqual(new[] { 1, 2 }, source.SkipTake(1, 100).ToArray());
    }

    [TestMethod]
    public void Array_SkipEqualsLength_ReturnsEmpty()
    {
        int[] source = [0, 1, 2];
        Assert.AreEqual(0, source.SkipTake(3, 5).Count());
    }

    // ── IList fast path ──────────────────────────────────────────────────────

    [TestMethod]
    public void List_SkipTake_ReturnsSlice()
    {
        var source = new List<string> { "a", "b", "c", "d", "e" };
        CollectionAssert.AreEqual(new[] { "b", "c" }, source.SkipTake(1, 2).ToArray());
    }

    // ── IEnumerable general path ─────────────────────────────────────────────

    [TestMethod]
    public void Enumerable_SkipTake_SingleEnumerator()
    {
        // Use a generator so only the general IEnumerable path is taken.
        IEnumerable<int> Gen() { for (int i = 0; i < 10; i++) yield return i; }
        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, Gen().SkipTake(3, 3).ToArray());
    }

    [TestMethod]
    public void Enumerable_TakeZero_ReturnsEmpty()
    {
        CollectionAssert.AreEqual(System.Array.Empty<int>(), new[] { 1, 2, 3 }.SkipTake(0, 0).ToArray());
    }

    [TestMethod]
    public void Enumerable_SkipPastEnd_ReturnsEmpty()
    {
        Assert.AreEqual(0, new[] { 1, 2, 3 }.SkipTake(10, 5).Count());
    }

    // ── Semantic equivalence with Skip+Take ──────────────────────────────────

    [TestMethod]
    public void EquivalentToSkipTake_OnArray()
    {
        int[] source = Enumerable.Range(0, 20).ToArray();
        var expected = source.Skip(5).Take(7).ToArray();
        CollectionAssert.AreEqual(expected, source.SkipTake(5, 7).ToArray());
    }

    [TestMethod]
    public void EquivalentToSkipTake_OnEnumerable()
    {
        IEnumerable<int> Gen() { for (int i = 0; i < 20; i++) yield return i; }
        var expected = Gen().Skip(4).Take(6).ToArray();
        CollectionAssert.AreEqual(expected, Gen().SkipTake(4, 6).ToArray());
    }
}
