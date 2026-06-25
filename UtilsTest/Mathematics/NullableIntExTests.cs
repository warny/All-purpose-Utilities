using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class NullableIntExTests
{
    // ── MinStartpoint (null = −∞) ────────────────────────────────────────────

    [TestMethod]
    public void MinStartpoint_NullMeansMinusInfinity()
    {
        // min(−∞, x) = −∞ → null wins
        Assert.IsNull(NullableIntEx.MinStartpoint<int>(null, 5));
        Assert.IsNull(NullableIntEx.MinStartpoint<int>(5, null));
        Assert.IsNull(NullableIntEx.MinStartpoint<int>(null, null));
    }

    [TestMethod]
    public void MinStartpoint_BothFinite_ReturnsSmaller()
    {
        Assert.AreEqual(3, NullableIntEx.MinStartpoint<int>(3, 7));
        Assert.AreEqual(3, NullableIntEx.MinStartpoint<int>(7, 3));
    }

    // ── MinEndpoint (null = +∞) ──────────────────────────────────────────────

    [TestMethod]
    public void MinEndpoint_NullMeansPlusInfinity()
    {
        // min(+∞, x) = x → finite wins
        Assert.AreEqual(5, NullableIntEx.MinEndpoint<int>(null, 5));
        Assert.AreEqual(5, NullableIntEx.MinEndpoint<int>(5, null));
        Assert.IsNull(NullableIntEx.MinEndpoint<int>(null, null));
    }

    [TestMethod]
    public void MinEndpoint_BothFinite_ReturnsSmaller()
    {
        Assert.AreEqual(3, NullableIntEx.MinEndpoint<int>(3, 7));
        Assert.AreEqual(3, NullableIntEx.MinEndpoint<int>(7, 3));
    }

    // ── MaxStartpoint (null = −∞) ────────────────────────────────────────────

    [TestMethod]
    public void MaxStartpoint_NullMeansMinusInfinity()
    {
        // max(−∞, x) = x → finite wins
        Assert.AreEqual(5, NullableIntEx.MaxStartpoint<int>(null, 5));
        Assert.AreEqual(5, NullableIntEx.MaxStartpoint<int>(5, null));
        Assert.IsNull(NullableIntEx.MaxStartpoint<int>(null, null));
    }

    [TestMethod]
    public void MaxStartpoint_BothFinite_ReturnsLarger()
    {
        Assert.AreEqual(7, NullableIntEx.MaxStartpoint<int>(3, 7));
        Assert.AreEqual(7, NullableIntEx.MaxStartpoint<int>(7, 3));
    }

    // ── MaxEndpoint (null = +∞) ──────────────────────────────────────────────

    [TestMethod]
    public void MaxEndpoint_NullMeansPlusInfinity()
    {
        // max(+∞, x) = +∞ → null wins
        Assert.IsNull(NullableIntEx.MaxEndpoint<int>(null, 5));
        Assert.IsNull(NullableIntEx.MaxEndpoint<int>(5, null));
        Assert.IsNull(NullableIntEx.MaxEndpoint<int>(null, null));
    }

    [TestMethod]
    public void MaxEndpoint_BothFinite_ReturnsLarger()
    {
        Assert.AreEqual(7, NullableIntEx.MaxEndpoint<int>(3, 7));
        Assert.AreEqual(7, NullableIntEx.MaxEndpoint<int>(7, 3));
    }

    // ── CompareStartpoint (null = −∞) ───────────────────────────────────────

    [TestMethod]
    public void CompareStartpoint_BothNull_ReturnsZero()
    {
        Assert.AreEqual(0, NullableIntEx.CompareStartpoint<int>(null, null));
    }

    [TestMethod]
    public void CompareStartpoint_NullLeftIsLess()
    {
        Assert.IsTrue(NullableIntEx.CompareStartpoint<int>(null, 5) < 0);  // −∞ < 5
    }

    [TestMethod]
    public void CompareStartpoint_NullRightIsGreater()
    {
        Assert.IsTrue(NullableIntEx.CompareStartpoint<int>(5, null) > 0);  // 5 > −∞
    }

    [TestMethod]
    public void CompareStartpoint_BothFinite_OrdersByValue()
    {
        Assert.IsTrue(NullableIntEx.CompareStartpoint<int>(3, 7) < 0);
        Assert.IsTrue(NullableIntEx.CompareStartpoint<int>(7, 3) > 0);
        Assert.AreEqual(0, NullableIntEx.CompareStartpoint<int>(5, 5));
    }

    // ── CompareEndpoint (null = +∞) ──────────────────────────────────────────

    [TestMethod]
    public void CompareEndpoint_BothNull_ReturnsZero()
    {
        Assert.AreEqual(0, NullableIntEx.CompareEndpoint<int>(null, null));
    }

    [TestMethod]
    public void CompareEndpoint_NullLeftIsGreater()
    {
        Assert.IsTrue(NullableIntEx.CompareEndpoint<int>(null, 5) > 0);   // +∞ > 5
    }

    [TestMethod]
    public void CompareEndpoint_NullRightIsLess()
    {
        Assert.IsTrue(NullableIntEx.CompareEndpoint<int>(5, null) < 0);   // 5 < +∞
    }

    [TestMethod]
    public void CompareEndpoint_BothFinite_OrdersByValue()
    {
        Assert.IsTrue(NullableIntEx.CompareEndpoint<int>(3, 7) < 0);
        Assert.IsTrue(NullableIntEx.CompareEndpoint<int>(7, 3) > 0);
        Assert.AreEqual(0, NullableIntEx.CompareEndpoint<int>(5, 5));
    }

    // ── LessOrEqual / Less ──────────────────────────────────────────────────

    [TestMethod]
    public void LessOrEqual_NullTreatedAsPlusInfinity()
    {
        Assert.IsTrue(((int?)5).LessOrEqual(null));    // 5 ≤ +∞
        Assert.IsFalse(((int?)null).LessOrEqual(5));   // +∞ ≤ 5 → false
        Assert.IsTrue(((int?)null).LessOrEqual(null)); // +∞ ≤ +∞ → true
    }

    [TestMethod]
    public void Less_ExcludesEquality()
    {
        Assert.IsFalse(((int?)5).Less(5));
        Assert.IsTrue(((int?)4).Less(5));
        Assert.IsFalse(((int?)null).Less(null));
    }

    // ── Increment / Decrement ───────────────────────────────────────────────

    [TestMethod]
    public void Increment_FiniteValue_AddsOne()
    {
        Assert.AreEqual((int?)6, ((int?)5).Increment());
    }

    [TestMethod]
    public void Increment_MaxValue_ReturnsNull()
    {
        Assert.IsNull(((int?)int.MaxValue).Increment());
    }

    [TestMethod]
    public void Decrement_FiniteValue_SubtractsOne()
    {
        Assert.AreEqual((int?)4, ((int?)5).Decrement());
    }

    [TestMethod]
    public void Decrement_MinValue_ReturnsNull()
    {
        Assert.IsNull(((int?)int.MinValue).Decrement());
    }
}
