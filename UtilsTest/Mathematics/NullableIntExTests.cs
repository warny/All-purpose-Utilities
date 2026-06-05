using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class NullableIntExTests
{
    // ── Min ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Min_IsMinTrue_NullMeansMinusInfinity()
    {
        // min(-∞, 5) = -∞
        Assert.IsNull(NullableIntEx.Min<int>(null, 5, isMin: true));
        Assert.IsNull(NullableIntEx.Min<int>(5, null, isMin: true));
        Assert.IsNull(NullableIntEx.Min<int>(null, null, isMin: true));
    }

    [TestMethod]
    public void Min_IsMinFalse_NullMeansPlusInfinity()
    {
        // min(+∞, 5) = 5
        Assert.AreEqual(5, NullableIntEx.Min<int>(null, 5, isMin: false));
        Assert.AreEqual(5, NullableIntEx.Min<int>(5, null, isMin: false));
        Assert.IsNull(NullableIntEx.Min<int>(null, null, isMin: false));
    }

    [TestMethod]
    public void Min_BothFinite_ReturnsSmaller()
    {
        Assert.AreEqual(3, NullableIntEx.Min<int>(3, 7, isMin: true));
        Assert.AreEqual(3, NullableIntEx.Min<int>(3, 7, isMin: false));
    }

    // ── Max ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Max_IsMaxTrue_NullMeansPlusInfinity()
    {
        // max(+∞, 5) = +∞
        Assert.IsNull(NullableIntEx.Max<int>(null, 5, isMax: true));
        Assert.IsNull(NullableIntEx.Max<int>(5, null, isMax: true));
        Assert.IsNull(NullableIntEx.Max<int>(null, null, isMax: true));
    }

    [TestMethod]
    public void Max_IsMaxFalse_NullMeansMinusInfinity()
    {
        // max(-∞, 5) = 5
        Assert.AreEqual(5, NullableIntEx.Max<int>(null, 5, isMax: false));
        Assert.AreEqual(5, NullableIntEx.Max<int>(5, null, isMax: false));
        Assert.IsNull(NullableIntEx.Max<int>(null, null, isMax: false));
    }

    [TestMethod]
    public void Max_BothFinite_ReturnsLarger()
    {
        Assert.AreEqual(7, NullableIntEx.Max<int>(3, 7, isMax: true));
        Assert.AreEqual(7, NullableIntEx.Max<int>(3, 7, isMax: false));
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
