using System.Linq.Expressions;
using System.Reflection;
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

    // ── Parameter-instance resolution (TODO-pass4 item #47) ────────────────────

    /// <summary>
    /// Before the fix, <c>Gradient()</c> resolved each partial derivative by
    /// <c>ParameterExpression.Name</c>; two unnamed parameters both have a <see langword="null"/> name, so
    /// name-based resolution would find two "candidates" for either target and throw an ambiguity error
    /// even though the parameters are perfectly identifiable by position/instance. Routing through the
    /// parameter-instance overloads fixes this.
    /// </summary>
    [TestMethod]
    public void Gradient_UnnamedParameters_ResolvesEachUnambiguously()
    {
        var x = Expression.Parameter(typeof(double));
        var y = Expression.Parameter(typeof(double));
        var f = Expression.Lambda<Func<double, double, double>>(Expression.Multiply(x, y), x, y);

        LambdaExpression[] grad = f.Gradient();
        Assert.AreEqual(2, grad.Length);

        var dfdx = (Expression<Func<double, double, double>>)grad[0];
        var dfdy = (Expression<Func<double, double, double>>)grad[1];
        Assert.AreEqual(3.0, dfdx.Compile()(2.0, 3.0), 1e-9);
        Assert.AreEqual(2.0, dfdy.Compile()(2.0, 3.0), 1e-9);
    }

    /// <summary>
    /// <see cref="MathExpressionExtensions.Derivate(LambdaExpression, ParameterExpression)"/> targets the
    /// exact parameter instance directly, without going through name-based resolution at all.
    /// </summary>
    [TestMethod]
    public void Derivate_ByParameterInstance_UnnamedParameter_Works()
    {
        var x = Expression.Parameter(typeof(double));
        Expression<Func<double, double>> f = Expression.Lambda<Func<double, double>>(Expression.Multiply(x, x), x);

        var df = (Expression<Func<double, double>>)f.Derivate(x);

        Assert.AreEqual(6.0, df.Compile()(3.0), 1e-9);
    }

    /// <summary>
    /// <see cref="MathExpressionExtensions.Integrate(LambdaExpression, ParameterExpression)"/> targets the
    /// exact parameter instance directly, without going through name-based resolution at all.
    /// </summary>
    [TestMethod]
    public void Integrate_ByParameterInstance_UnnamedParameter_Works()
    {
        var x = Expression.Parameter(typeof(double));
        Expression<Func<double, double>> f = Expression.Lambda<Func<double, double>>(x, x);

        var intF = (Expression<Func<double, double>>)f.Integrate(x);

        // integral of x dx = x^2/2; at x=4 -> 8
        Assert.AreEqual(8.0, intF.Compile()(4.0), 1e-9);
    }

    // ── Reflection dispatch must not wrap exceptions (PR 450 review) ───────────

    /// <summary>
    /// The non-generic overloads dispatch to their <c>&lt;T&gt;</c> counterpart through
    /// <see cref="MethodInfo.Invoke(object?, object?[]?)"/>, which by default wraps any exception thrown by
    /// the invoked method in a <see cref="TargetInvocationException"/>. That would silently change the
    /// public exception contract of these convenience overloads (their generic counterparts throw
    /// <see cref="InvalidOperationException"/> directly); the dispatch helper must unwrap it.
    /// </summary>
    [TestMethod]
    public void Derivate_UnknownParameterName_ThrowsInvalidOperationExceptionDirectly()
    {
        Expression<Func<double, double>> f = x => x;
        Assert.ThrowsExactly<SymbolicParameterException>(() => f.Derivate("missing"));
    }

    /// <summary>
    /// Same as <see cref="Derivate_UnknownParameterName_ThrowsInvalidOperationExceptionDirectly"/>, but for
    /// the parameter-instance overload: a parameter that does not belong to the lambda must surface the
    /// underlying <see cref="InvalidOperationException"/> unwrapped.
    /// </summary>
    [TestMethod]
    public void Derivate_ByParameterInstance_ForeignParameter_ThrowsInvalidOperationExceptionDirectly()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var foreign = Expression.Parameter(typeof(double), "x");
        Expression<Func<double, double>> f = Expression.Lambda<Func<double, double>>(x, x);

        Assert.ThrowsExactly<SymbolicParameterException>(() => f.Derivate(foreign));
    }

    /// <summary>
    /// Same as <see cref="Derivate_ByParameterInstance_ForeignParameter_ThrowsInvalidOperationExceptionDirectly"/>,
    /// for the integration side.
    /// </summary>
    [TestMethod]
    public void Integrate_ByParameterInstance_ForeignParameter_ThrowsInvalidOperationExceptionDirectly()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var foreign = Expression.Parameter(typeof(double), "x");
        Expression<Func<double, double>> f = Expression.Lambda<Func<double, double>>(x, x);

        Assert.ThrowsExactly<SymbolicParameterException>(() => f.Integrate(foreign));
    }
}
