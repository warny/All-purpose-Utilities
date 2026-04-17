using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides compiler-based regression coverage for legacy integration parser tests.
/// </summary>
[TestClass]
public class ExpressionIntegrationTests
{
    CStyleExpressionCompiler compiler = new CStyleExpressionCompiler();
    readonly ExpressionIntegration integration = new ExpressionIntegration("x");
    readonly ExpressionSimplifier simplifier = new ExpressionSimplifier();

    [TestMethod]
    public void ExpressionsIntegration()
    {
        var parameters = new ParameterExpression[]
        {
                Expression.Parameter(typeof(double), "x"),
        };

        var tests = new (string function, string integral)[]
        {
            ("1/x", "Log(x)"),
            ("1/(x**2)", "-(1.0/x)"),
            ("1/Sqrt(x)", "2.0*Sqrt(x)"),
            ("Sinh(x)", "Cosh(x)"),
            ("Cosh(x)", "Sinh(x)"),
            ("Tanh(x)", "Log(Cosh(x))"),
        };

        foreach (var test in tests)
        {
            var func = compiler.Compile<Func<double, double>>(test.function, parameters, typeof(double), false);
            var expected = simplifier.Simplify(compiler.Compile<Func<double, double>>(test.integral, parameters, typeof(double), false));
            var result = simplifier.Simplify(integration.Integrate(func));
            Assert.AreEqual(expected, result, ExpressionComparer.Default);
        }
    }


    /// <summary>
    /// Ensures trigonometric expressions still compile for integration workflows.
    /// </summary>
    [TestMethod]
    public void Compile_TrigExpression_ForIntegrationWorkflow()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var expression = compiler.Compile("x * x + 1", new Dictionary<string, Expression> { ["x"] = x });
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Convert(expression, typeof(double)), x).Compile();

        Assert.AreEqual(10d, lambda(3d), 1e-9);
    }
}
