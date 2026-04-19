using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides compiler-based regression coverage for legacy derivation parser tests.
/// </summary>
[TestClass]
public class ExpressionDerivationTests
{
    
    CStyleExpressionCompiler compiler = new CStyleExpressionCompiler();
    ExpressionDerivation derivation = new ExpressionDerivation("x");

    [TestMethod]
    public void ExpressionsTests()
    {
        var parameters = new ParameterExpression[] {
                Expression.Parameter(typeof(double), "x"),
            };

        var tests = new (string function, string derivative)[]
        {
                ("1", "0"),
                ("Exp(x)", "Exp(x)"),
                ("x", "1"),
                ("x**2", "2*x"),
                ("x**3", "3*x**2"),
                ("x**3 + x**2 + x+1 ", "3*x**2 + 2*x + 1"),
                ("Cos(x)", "0-Sin(x)"),
                ("Sin(x)", "Cos(x)"),
                ("Sin(2*x)", "2*Cos(2*x)"),
                ("(Sin(x)) * (Cos(x))", "(Cos(x))**2-(Sin(x))**2"),
                ("Exp(x**2)", "2*x*Exp(x**2)"),
        };

        foreach (var test in tests)
        {
            var function = compiler.Compile<Func<double, double>>(test.function, parameters, typeof(double), false);
            var derivative = compiler.Compile<Func<double, double>>(test.derivative, parameters, typeof(double), false);

            var result = derivation.Derivate(function);

            var expected = derivative;
            var actual = (LambdaExpression)result;

            var expectedFunc = (Func<double, double>)expected.Compile();
            var actualFunc = (Func<double, double>)actual.Compile();
            double[] samples = [-3.5, -1.0, -0.2, 0.2, 1.0, 2.5];

            foreach (var sample in samples)
            {
                Assert.AreEqual(expectedFunc(sample), actualFunc(sample), 1e-9, $"Mismatch for '{test.function}' at x={sample}.");
            }
        }
    }

    /// <summary>
    /// Ensures polynomial expressions can still be compiled for derivative workflows.
    /// </summary>
    [TestMethod]
    public void Compile_PolynomialExpression_ForDerivativeWorkflow()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var expression = compiler.Compile("x * x + 2 * x", new Dictionary<string, Expression> { ["x"] = x });
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Convert(expression, typeof(double)), x).Compile();

        Assert.AreEqual(15d, lambda(3d), 1e-9);
    }
}
