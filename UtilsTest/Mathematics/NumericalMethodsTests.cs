using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class NumericalMethodsTests
{
    // -------------------------------------------------------------------------
    // Integrate
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Integrate_Constant_ExactResult()
    {
        // ∫₀¹ 3 dx = 3
        double result = NumericalMethods.Integrate<double>(_ => 3.0, 0.0, 1.0, 100);
        Assert.AreEqual(3.0, result, 1e-10);
    }

    [TestMethod]
    public void Integrate_Linear_ExactResult()
    {
        // ∫₀¹ x dx = 0.5
        double result = NumericalMethods.Integrate<double>(x => x, 0.0, 1.0, 100);
        Assert.AreEqual(0.5, result, 1e-10);
    }

    [TestMethod]
    public void Integrate_Quadratic_ExactResult()
    {
        // ∫₀¹ x² dx = 1/3
        double result = NumericalMethods.Integrate<double>(x => x * x, 0.0, 1.0, 1000);
        Assert.AreEqual(1.0 / 3.0, result, 1e-8);
    }

    [TestMethod]
    public void Integrate_Sine_CloseToExpected()
    {
        // ∫₀^π sin(x) dx = 2
        double result = NumericalMethods.Integrate<double>(Math.Sin, 0.0, Math.PI, 1000);
        Assert.AreEqual(2.0, result, 1e-7);
    }

    [TestMethod]
    public void Integrate_NegativeSteps_Throws()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => NumericalMethods.Integrate<double>(x => x, 0.0, 1.0, -1));

    [TestMethod]
    public void Integrate_OddStepsRoundedUp_StillCorrect()
    {
        // Passing odd step count should round up silently
        double result = NumericalMethods.Integrate<double>(x => x, 0.0, 1.0, 101);
        Assert.AreEqual(0.5, result, 1e-8);
    }

    [TestMethod]
    public void Integrate_OddIntMaxValueSteps_ThrowsInsteadOfOverflowing()
    {
        // int.MaxValue is odd; incrementing it to round up to an even count previously overflowed
        // to int.MinValue instead of being rejected, silently corrupting every subsequent
        // computation with a negative step count.
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => NumericalMethods.Integrate<double>(x => x, 0.0, 1.0, int.MaxValue));
    }

    [TestMethod]
    public void Integrate_NullFunction_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => NumericalMethods.Integrate<double>(null!, 0.0, 1.0, 100));

    [TestMethod]
    public void Integrate_NonFiniteBounds_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => NumericalMethods.Integrate<double>(x => x, double.NaN, 1.0, 100));
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => NumericalMethods.Integrate<double>(x => x, 0.0, double.PositiveInfinity, 100));
    }

    // -------------------------------------------------------------------------
    // Lagrange
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Lagrange_TwoPoints_ExactLinearInterpolation()
    {
        // Points (0,0) and (1,1) → line x → at 0.5 = 0.5
        (double, double)[] pts = [(0, 0), (1, 1)];
        Assert.AreEqual(0.5, NumericalMethods.Lagrange<double>(pts, 0.5), 1e-10);
    }

    [TestMethod]
    public void Lagrange_ExactAtKnownPoints()
    {
        (double, double)[] pts = [(0, 0), (1, 1), (2, 4)];
        Assert.AreEqual(0.0, NumericalMethods.Lagrange<double>(pts, 0), 1e-10);
        Assert.AreEqual(1.0, NumericalMethods.Lagrange<double>(pts, 1), 1e-10);
        Assert.AreEqual(4.0, NumericalMethods.Lagrange<double>(pts, 2), 1e-10);
    }

    [TestMethod]
    public void Lagrange_QuadraticRecovery()
    {
        // Points on y=x² → interpolated at 1.5 should be 2.25
        (double, double)[] pts = [(0, 0), (1, 1), (2, 4)];
        Assert.AreEqual(2.25, NumericalMethods.Lagrange<double>(pts, 1.5), 1e-10);
    }

    [TestMethod]
    public void Lagrange_EmptyPoints_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => NumericalMethods.Lagrange<double>([], 1.0));

    [TestMethod]
    public void Lagrange_DuplicateX_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => NumericalMethods.Lagrange<double>([(0, 0), (0, 1)], 0.5));

    [TestMethod]
    public void Lagrange_NaNAbscissa_Throws()
    {
        // Regression: a NaN x value bypassed the old denom == T.Zero distinctness check (NaN is
        // never equal to anything) and propagated NaN through the whole result instead of
        // producing the documented distinct-point error.
        Assert.ThrowsException<ArgumentException>(
            () => NumericalMethods.Lagrange<double>([(0, 0), (double.NaN, 1)], 0.5));
    }

    [TestMethod]
    public void Lagrange_InfiniteCoordinate_Throws()
        => Assert.ThrowsException<ArgumentException>(
            () => NumericalMethods.Lagrange<double>([(0, 0), (double.PositiveInfinity, 1)], 0.5));

    [TestMethod]
    public void Lagrange_NonFiniteEvaluationPoint_Throws()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => NumericalMethods.Lagrange<double>([(0, 0), (1, 1)], double.NaN));

    [TestMethod]
    public void Lagrange_DuplicateXNotAdjacent_ThrowsBeforeEvaluating()
    {
        // The duplicate is between the first and last point (not caught by an inner-loop check
        // that only compares against the immediately preceding index) - validation must scan all
        // pairs up front rather than discovering it partway through result construction.
        Assert.ThrowsException<ArgumentException>(
            () => NumericalMethods.Lagrange<double>([(0, 0), (1, 1), (2, 2), (0, 5)], 0.5));
    }
}
