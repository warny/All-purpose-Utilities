using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class FloatingPointComparerTests
{
    private readonly FloatingPointComparer<double> _comparer = new(precision: 2);

    [TestMethod]
    public void Equals_WithinTolerance_ReturnsTrue()
    {
        Assert.IsTrue(_comparer.Equals(1.001, 1.002));
        Assert.IsTrue(_comparer.Equals(5.0, 5.009));
        Assert.IsTrue(_comparer.Equals(0.0, 0.009));
    }

    [TestMethod]
    public void Equals_OutsideTolerance_ReturnsFalse()
    {
        Assert.IsFalse(_comparer.Equals(1.0, 1.02));
        Assert.IsFalse(_comparer.Equals(5.0, 5.1));
    }

    [TestMethod]
    public void Equals_ExactlyAtTolerance_ReturnsTrue()
    {
        // precision=2 => interval=0.01; x.Between(y-0.01, y+0.01) is inclusive
        Assert.IsTrue(_comparer.Equals(1.0, 1.01));
        Assert.IsTrue(_comparer.Equals(1.01, 1.0));
    }

    [TestMethod]
    public void GetHashCode_EqualValues_SameHash()
    {
        // Values within tolerance must produce the same hash (contract: Equals→same hash).
        // We return 0 for all values to satisfy this invariant.
        Assert.AreEqual(_comparer.GetHashCode(1.001), _comparer.GetHashCode(1.002));
        Assert.AreEqual(_comparer.GetHashCode(0.0), _comparer.GetHashCode(0.009));
        Assert.AreEqual(0, _comparer.GetHashCode(42.0));
    }

    [TestMethod]
    public void Compare_OrdersCorrectly()
    {
        Assert.IsTrue(_comparer.Compare(1.0, 2.0) < 0);
        Assert.IsTrue(_comparer.Compare(2.0, 1.0) > 0);
        Assert.AreEqual(0, _comparer.Compare(1.5, 1.5));
    }

    [TestMethod]
    public void Compare_WithinTolerance_ReturnsZero()
    {
        // precision=2 => interval=0.01
        Assert.AreEqual(0, _comparer.Compare(1.0, 1.009));
        Assert.AreEqual(0, _comparer.Compare(1.009, 1.0));
        Assert.AreEqual(0, _comparer.Compare(1.0, 1.01));  // exactement à la limite
    }

    [TestMethod]
    public void PrecisionConstructor_SetsInterval()
    {
        var c = new FloatingPointComparer<double>(precision: 3);
        Assert.AreEqual(0.001, c.Interval, delta: 1e-12);
    }

    [TestMethod]
    public void IntervalConstructor_SetsInterval()
    {
        var c = new FloatingPointComparer<double>(interval: 0.5);
        Assert.AreEqual(0.5, c.Interval);
        Assert.IsTrue(c.Equals(1.0, 1.4));
        Assert.IsFalse(c.Equals(1.0, 1.6));
    }
}
