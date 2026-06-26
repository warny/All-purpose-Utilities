using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

[TestClass]
public class GradientTests
{
    [TestMethod]
    public void Gradient_SingleVariable_SameAsDerivate()
    {
        Expression<Func<double, double>> f = x => x * x;
        LambdaExpression[] grad = f.Gradient();
        Assert.AreEqual(1, grad.Length);

        var df = (Expression<Func<double, double>>)grad[0];
        var compiled = df.Compile();
        // d/dx x² = 2x; at x=3 → 6
        Assert.AreEqual(6.0, compiled(3.0), 1e-9);
    }

    [TestMethod]
    public void Gradient_TwoVariables_BothPartials()
    {
        // f(x, y) = x*y  → ∂f/∂x = y, ∂f/∂y = x
        Expression<Func<double, double, double>> f = (x, y) => x * y;
        LambdaExpression[] grad = f.Gradient();
        Assert.AreEqual(2, grad.Length);

        var dfdx = (Expression<Func<double, double, double>>)grad[0];
        var dfdy = (Expression<Func<double, double, double>>)grad[1];

        // ∂/∂x at (2, 3) = 3
        Assert.AreEqual(3.0, dfdx.Compile()(2.0, 3.0), 1e-9);
        // ∂/∂y at (2, 3) = 2
        Assert.AreEqual(2.0, dfdy.Compile()(2.0, 3.0), 1e-9);
    }

    [TestMethod]
    public void Gradient_WithExplicitParamNames_SubsetOfParameters()
    {
        // Only request ∂/∂x for f(x,y) = x² + y
        Expression<Func<double, double, double>> f = (x, y) => x * x + y;
        LambdaExpression[] grad = f.Gradient<double>(["x"]);
        Assert.AreEqual(1, grad.Length);

        var dfdx = (Expression<Func<double, double, double>>)grad[0];
        // d/dx (x²+y) = 2x; at x=4, y=0 → 8
        Assert.AreEqual(8.0, dfdx.Compile()(4.0, 0.0), 1e-9);
    }

    [TestMethod]
    public void Gradient_Constant_IsZero()
    {
        Expression<Func<double, double>> f = x => 5.0;
        LambdaExpression[] grad = f.Gradient();
        var df = (Expression<Func<double, double>>)grad[0];
        Assert.AreEqual(0.0, df.Compile()(99.0), 1e-9);
    }
}
