using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for TODO-pass4 item #46: the non-generic convenience overloads must infer the
/// lambda's own scalar type instead of silently forcing <see cref="double"/>.
/// </summary>
[TestClass]
public class MathExpressionExtensionsTests
{
    [TestMethod]
    public void Derivate_FloatLambda_InfersFloatInsteadOfDouble()
    {
        Expression<Func<float, float>> f = x => x * x;
        var df = (Expression<Func<float, float>>)f.Derivate();

        // d/dx x^2 = 2x; at x=3 -> 6
        Assert.AreEqual(6f, df.Compile()(3f), 1e-4f);
    }

    [TestMethod]
    public void Derivate_DecimalLambda_InfersDecimalInsteadOfDouble()
    {
        Expression<Func<decimal, decimal>> f = x => x;
        var df = (Expression<Func<decimal, decimal>>)f.Derivate();

        Assert.AreEqual(1m, df.Compile()(3m));
    }

    [TestMethod]
    public void Integrate_FloatLambda_InfersFloatInsteadOfDouble()
    {
        Expression<Func<float, float>> f = x => x;
        var intF = (Expression<Func<float, float>>)f.Integrate();
        var compiled = intF.Compile();

        // integral of x dx = x^2/2; at x=4 -> 8
        Assert.AreEqual(8f, compiled(4f), 1e-3f);
    }

    [TestMethod]
    public void Gradient_FloatLambda_InfersFloatInsteadOfDouble()
    {
        Expression<Func<float, float, float>> f = (x, y) => x * y;
        LambdaExpression[] grad = f.Gradient();
        Assert.AreEqual(2, grad.Length);

        var dfdx = (Expression<Func<float, float, float>>)grad[0];
        Assert.AreEqual(3f, dfdx.Compile()(2f, 3f), 1e-4f);
    }

    /// <summary>
    /// <see cref="int"/> does not implement <c>IFloatingPoint&lt;T&gt;</c>, so the non-generic overload
    /// must fail explicitly instead of forcing the expression through <see cref="double"/> rules that
    /// don't match the lambda's declared parameter type.
    /// </summary>
    [TestMethod]
    public void Derivate_NonFloatingPointParameterType_ThrowsNotSupportedException()
    {
        Expression<Func<int, int>> f = x => x;
        Assert.ThrowsException<NotSupportedException>(() => f.Derivate());
    }

    /// <summary>
    /// A gradient over parameters of two different types has no single scalar type to infer and must be
    /// rejected explicitly rather than picking one arbitrarily.
    /// </summary>
    [TestMethod]
    public void Gradient_MixedParameterTypes_ThrowsNotSupportedException()
    {
        Expression<Func<float, double, double>> f = (x, y) => x + y;
        Assert.ThrowsException<NotSupportedException>(() => f.Gradient());
    }
}
