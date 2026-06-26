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
}
