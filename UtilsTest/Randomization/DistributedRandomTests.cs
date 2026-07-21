using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Randomization;

namespace UtilsTest.Randomization;

[TestClass]
public class DistributedRandomTests
{
    [TestMethod]
    public void NextDoubleWithRangeReturnsContinuousValuesTest()
    {
        var random = new DistributedRandom(x => x, seed: 12345);
        bool foundFractional = false;
        for (int i = 0; i < 20; i++)
        {
            double value = random.NextDouble(0.0, 100.0);
            Assert.IsTrue(value >= 0.0 && value < 100.0);
            if (value != Math.Floor(value))
            {
                foundFractional = true;
            }
        }
        Assert.IsTrue(foundFractional, "NextDouble(min, max) must be able to return non-integer values.");
    }

    [TestMethod]
    public void NextDoubleWithIdentityDistributionMatchesUnderlyingUniformRandomTest()
    {
        double expectedUniform = new Random(42).NextDouble();
        double expected = expectedUniform * (10.0 - 5.0) + 5.0;

        var distributed = new DistributedRandom(x => x, seed: 42);
        double actual = distributed.NextDouble(5.0, 10.0);

        Assert.AreEqual(expected, actual, 1e-12);
    }

    [TestMethod]
    public void NextIntStaysWithinRangeAndTruncatesTest()
    {
        var distributed = new DistributedRandom(x => x, seed: 7);
        for (int i = 0; i < 20; i++)
        {
            int value = distributed.NextInt(0, 100);
            Assert.IsTrue(value >= 0 && value < 100);
        }
    }

    // ------------------------------------------------------------------ #18 normalization validation

    [TestMethod]
    public void Constructor_ThrowsOnConstantFunction()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DistributedRandom(_ => 42.0));
    }

    [TestMethod]
    public void Constructor_ThrowsOnDecreasingFunction()
    {
        // f(0)=1, f(1)=0 — decreasing violates the f(1) > f(0) contract.
        Assert.ThrowsExactly<ArgumentException>(() => new DistributedRandom(x => 1.0 - x));
    }

    [TestMethod]
    public void Constructor_ThrowsWhenIntervalOverflowsToInfinity()
    {
        // f(0)=-MaxValue, f(1)=+MaxValue — f(1)-f(0) overflows to PositiveInfinity.
        Assert.ThrowsExactly<ArgumentException>(
            () => new DistributedRandom(x => x == 0 ? -double.MaxValue : double.MaxValue));
    }

    [TestMethod]
    public void Constructor_ThrowsWhenF0IsInfinity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DistributedRandom(x => x == 0 ? double.PositiveInfinity : x));
    }

    [TestMethod]
    public void Constructor_ThrowsWhenF1IsNaN()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DistributedRandom(x => x == 1 ? double.NaN : x));
    }

    [TestMethod]
    public void Constructor_ThrowsWhenF0IsNaN()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DistributedRandom(x => double.NaN));
    }

    [TestMethod]
    public void Constructor_ThrowsWhenBothEndpointsAreInfinity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DistributedRandom(x => double.PositiveInfinity));
    }

    // ------------------------------------------------------------------ #19 NextInt range correctness

    [TestMethod]
    public void NextInt_NeverReturnsUpperBound()
    {
        var distributed = new DistributedRandom(x => x, seed: 0);
        for (int i = 0; i < 1000; i++)
        {
            int value = distributed.NextInt(0, 10);
            Assert.IsTrue(value >= 0 && value < 10,
                $"NextInt(0,10) returned {value}, which is outside [0,10).");
        }
    }

    [TestMethod]
    public void NextInt_WorksWithLargeRange_NoOverflow()
    {
        // Range = int.MaxValue - int.MinValue which overflows in int arithmetic.
        var distributed = new DistributedRandom(x => x, seed: 1);
        for (int i = 0; i < 100; i++)
        {
            int value = distributed.NextInt(int.MinValue, int.MaxValue);
            // Just verify it does not throw and is within bounds.
            Assert.IsTrue(value >= int.MinValue && value < int.MaxValue,
                $"NextInt(int.MinValue, int.MaxValue) returned {value}.");
        }
    }

    [TestMethod]
    public void NextInt_WorksAtMinimumRange_ReturnsMin()
    {
        // When min and max differ by 1, result must always equal min.
        var distributed = new DistributedRandom(x => x, seed: 2);
        for (int i = 0; i < 20; i++)
        {
            int value = distributed.NextInt(5, 6);
            Assert.AreEqual(5, value);
        }
    }

    [TestMethod]
    public void NextInt_ThrowsWhenMaxEqualsMin()
    {
        var distributed = new DistributedRandom(x => x, seed: 0);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distributed.NextInt(5, 5));
    }

    // ------------------------------------------------------------------ mid-function non-finite guard (#18)

    [TestMethod]
    public void NextDouble_ThrowsInvalidOperation_WhenFunctionReturnsNaN()
    {
        // Function is valid at endpoints (f(0)=0, f(1)=1) but returns NaN for
        // intermediate inputs — must be rejected at call time, not silently propagated.
        var distributed = new DistributedRandom(x => x == 0 || x == 1 ? x : double.NaN);
        Assert.ThrowsExactly<InvalidOperationException>(() => distributed.NextDouble());
    }

    [TestMethod]
    public void NextDouble_ThrowsInvalidOperation_WhenFunctionReturnsInfinity()
    {
        // Valid endpoints, but returns +∞ for an intermediate value.
        var distributed = new DistributedRandom(x => x == 0 || x == 1 ? x : double.PositiveInfinity);
        Assert.ThrowsExactly<InvalidOperationException>(() => distributed.NextDouble());
    }

    [TestMethod]
    public void NextDouble_ThrowsInvalidOperation_WhenFunctionIsNonMonotone()
    {
        // f(0)=0, f(1)≈1, but f(0.5)≈10.5 — strongly non-monotone.
        // For any uniformRandom value strictly between 0 and 1 (which NextDouble always returns),
        // the normalized value will exceed [0, 1+1e-12] and must be rejected.
        var distributed = new DistributedRandom(
            x => x + 10 * Math.Sin(Math.PI * x), seed: 42);
        Assert.ThrowsExactly<InvalidOperationException>(() => distributed.NextDouble());
    }

    [TestMethod]
    public void NextDouble_MonotoneFunction_ReturnsClamped01()
    {
        // A strictly monotone function must always produce values in [0, 1].
        var distributed = new DistributedRandom(x => x * x, seed: 7); // f(0)=0, f(1)=1, monotone
        for (int i = 0; i < 100; i++)
        {
            double v = distributed.NextDouble();
            Assert.IsTrue(v >= 0.0 && v <= 1.0,
                $"NextDouble() returned {v} which is outside [0, 1].");
        }
    }
}
