using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class FloatingPointComparerTests
{
    private readonly FloatingPointComparer<double> _comparer = new(precision: 2);

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
        Assert.AreEqual(0, _comparer.Compare(5.0, 5.005));
    }

    [TestMethod]
    public void Compare_OutsideTolerance_ReturnsNonZero()
    {
        // precision=2 => interval=0.01
        Assert.IsTrue(_comparer.Compare(1.0, 1.02) < 0);
        Assert.IsTrue(_comparer.Compare(1.02, 1.0) > 0);
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
        Assert.AreEqual(0, c.Compare(1.0, 1.4));
        Assert.IsTrue(c.Compare(1.0, 1.6) < 0);
    }

    [TestMethod]
    public void ForPrecision_ReturnsCachedInstance()
    {
        var a = FloatingPointComparer<double>.ForPrecision(2);
        var b = FloatingPointComparer<double>.ForPrecision(2);
        Assert.AreSame(a, b);
    }

    [TestMethod]
    public void ForPrecision_DifferentPrecisions_ReturnDifferentInstances()
    {
        var a = FloatingPointComparer<double>.ForPrecision(2);
        var b = FloatingPointComparer<double>.ForPrecision(3);
        Assert.AreNotSame(a, b);
        Assert.AreEqual(0.01, a.Interval, 1e-15);
        Assert.AreEqual(0.001, b.Interval, 1e-15);
    }
}
