using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics;

[TestClass]
public class PolynomialTests
{
    private const double Tol = 1e-9;

    // 1 + 2x + 3x²
    private static Polynomial<double> P123 => new(1.0, 2.0, 3.0);

    [TestMethod]
    public void Evaluate_QuadraticAtZero_ReturnsConstantTerm()
        => Assert.AreEqual(1.0, P123.Evaluate(0.0), Tol);

    [TestMethod]
    public void Evaluate_QuadraticAtOne_ReturnsSumOfCoefficients()
        => Assert.AreEqual(6.0, P123.Evaluate(1.0), Tol);

    [TestMethod]
    public void Evaluate_QuadraticAtTwo()
        // 1 + 4 + 12 = 17
        => Assert.AreEqual(17.0, P123.Evaluate(2.0), Tol);

    [TestMethod]
    public void Degree_LeadingZerosTrimmed()
    {
        var p = new Polynomial<double>(1.0, 2.0, 0.0, 0.0);
        Assert.AreEqual(1, p.Degree);
    }

    [TestMethod]
    public void Derive_Quadratic_ReturnsLinear()
    {
        // d/dx (1 + 2x + 3x²) = 2 + 6x
        var d = P123.Derive();
        Assert.AreEqual(1, d.Degree);
        Assert.AreEqual(2.0, d.Evaluate(0.0), Tol);
        Assert.AreEqual(8.0, d.Evaluate(1.0), Tol);
    }

    [TestMethod]
    public void Derive_Constant_ReturnsZero()
    {
        var p = new Polynomial<double>(5.0);
        Assert.AreEqual(0.0, p.Derive().Evaluate(100.0), Tol);
    }

    [TestMethod]
    public void Integrate_Linear_ReturnsQuadratic()
    {
        // ∫ (2 + 6x) dx = 0 + 2x + 3x² (constant=0)
        var lin = new Polynomial<double>(2.0, 6.0);
        var integral = lin.Integrate();
        Assert.AreEqual(2, integral.Degree);
        Assert.AreEqual(0.0, integral.Evaluate(0.0), Tol);
        Assert.AreEqual(5.0, integral.Evaluate(1.0), Tol);  // 0 + 2 + 3
    }

    [TestMethod]
    public void Integrate_WithConstant()
    {
        var lin = new Polynomial<double>(2.0, 6.0);
        var integral = lin.Integrate(constant: 7.0);
        Assert.AreEqual(7.0, integral.Evaluate(0.0), Tol);
    }

    [TestMethod]
    public void Add_TwoPolynomials()
    {
        var p = new Polynomial<double>(1.0, 2.0);      // 1 + 2x
        var q = new Polynomial<double>(3.0, 0.0, 4.0); // 3 + 4x²
        var sum = p + q;
        Assert.AreEqual(2, sum.Degree);
        Assert.AreEqual(4.0, sum.Evaluate(0.0), Tol);  // 1+3
        Assert.AreEqual(10.0, sum.Evaluate(1.0), Tol); // 4+2+4
    }

    [TestMethod]
    public void Multiply_TwoLinears_ProducesQuadratic()
    {
        // (1+x)*(1+x) = 1 + 2x + x²
        var p = new Polynomial<double>(1.0, 1.0);
        var product = p * p;
        Assert.AreEqual(2, product.Degree);
        Assert.AreEqual(1.0, product[0], Tol);
        Assert.AreEqual(2.0, product[1], Tol);
        Assert.AreEqual(1.0, product[2], Tol);
    }

    [TestMethod]
    public void ScalarMultiply()
    {
        var p = new Polynomial<double>(1.0, 2.0);
        var scaled = 3.0 * p;
        Assert.AreEqual(3.0, scaled[0], Tol);
        Assert.AreEqual(6.0, scaled[1], Tol);
    }

    [TestMethod]
    public void FindRoot_Quadratic_ReturnsRoot()
    {
        // x² - 4 = 0 → roots ±2; start near positive root
        var p = new Polynomial<double>(-4.0, 0.0, 1.0);
        double? root = p.FindRoot(1.5);
        Assert.IsNotNull(root);
        Assert.AreEqual(0.0, p.Evaluate(root.Value), 1e-9);
    }

    [TestMethod]
    public void Equals_SameCoefficients_ReturnsTrue()
    {
        var p = new Polynomial<double>(1.0, 2.0, 3.0);
        var q = new Polynomial<double>(1.0, 2.0, 3.0);
        Assert.IsTrue(p.Equals(q));
    }

    [TestMethod]
    public void NoCoefficients_Throws()
        => Assert.ThrowsException<ArgumentException>(() => new Polynomial<double>(System.Array.Empty<double>()));
}
