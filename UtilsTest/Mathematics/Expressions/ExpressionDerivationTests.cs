using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides compiler-based regression coverage for legacy derivation parser tests.
/// </summary>
[TestClass]
public class ExpressionDerivationTests
{

    CSyntaxExpressionCompiler compiler = new CSyntaxExpressionCompiler();
    ExpressionDerivation<double> derivation = new ExpressionDerivation<double>("x");

    /// <summary>
    /// A sample unknown function used to validate finite-difference fallback derivatives.
    /// </summary>
    /// <param name="x">Input value.</param>
    /// <returns>Function value at <paramref name="x"/>.</returns>
    private static double CustomUnknown(double x) => x * x * x + 1.0;

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

    /// <summary>
    /// Ensures unknown single-argument double functions use centered finite differences during derivation.
    /// </summary>
    [TestMethod]
    public void Derivate_UnknownDoubleFunction_UsesFiniteDifferenceFallback()
    {
        Expression<Func<double, double>> function = x => CustomUnknown(x);
        var result = (Expression<Func<double, double>>)derivation.Derivate(function);
        var derivative = result.Compile();

        double[] samples = [-2.0, -0.5, 0.25, 1.5];
        foreach (var sample in samples)
        {
            double expected = 3.0 * sample * sample;
            double actual = derivative(sample);
            Assert.AreEqual(expected, actual, 1e-4, $"Fallback derivative mismatch at x={sample}.");
        }
    }

    /// <summary>
    /// Ensures finite-difference fallback applies the chain rule for composed unknown functions.
    /// </summary>
    [TestMethod]
    public void Derivate_ComposedUnknownDoubleFunction_AppliesChainRule()
    {
        Expression<Func<double, double>> function = x => CustomUnknown(x * x);
        var result = (Expression<Func<double, double>>)derivation.Derivate(function);
        var derivative = result.Compile();

        double[] samples = [-1.5, -0.75, 0.5, 1.25];
        foreach (var sample in samples)
        {
            double expected = 6.0 * sample * double.Pow(sample * sample, 2);
            double actual = derivative(sample);
            Assert.AreEqual(expected, actual, 5e-4, $"Composed fallback derivative mismatch at x={sample}.");
        }
    }


    /// <summary>
    /// Ensures generic derivation also works for <see cref="float"/> lambdas.
    /// </summary>
    [TestMethod]
    public void Derivate_FloatLambda_WorksWithGenericDerivation()
    {
        ExpressionDerivation<float> floatDerivation = new("x");
        Expression<Func<float, float>> function = x => x;

        var result = (Expression<Func<float, float>>)floatDerivation.Derivate(function);
        var derivative = result.Compile();

        float[] samples = [-2f, -0.5f, 0f, 1.25f];
        foreach (float sample in samples)
        {
            float expected = 1f;
            float actual = derivative(sample);
            Assert.AreEqual(expected, actual, 5e-3f, $"Float derivative mismatch at x={sample}.");
        }
    }

    /// <summary>
    /// Ensures the generic extension entry-point can derive with a float type parameter.
    /// </summary>
    [TestMethod]
    public void DerivateExtension_GenericFloat_Works()
    {
        Expression<Func<float, float>> function = x => x;
        var derived = (Expression<Func<float, float>>)function.Derivate<float>("x");
        Func<float, float> compiled = derived.Compile();

        Assert.AreEqual(1f, compiled(4f), 5e-3f);
    }

    // ── Logarithms ───────────────────────────────────────────────────────────

    /// <summary>
    /// d/dx[ln(x)] = 1/x.
    /// </summary>
    [TestMethod]
    public void Derivate_Log_ReturnsOneOverX()
    {
        Expression<Func<double, double>> f = x => double.Log(x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        foreach (double xv in new[] { 0.5, 1.0, 2.0, Math.E })
            Assert.AreEqual(1.0 / xv, compiled(xv), 1e-9, $"d/dx[ln(x)] at x={xv}");
    }

    /// <summary>
    /// d/dx[log10(x)] = 1/(x·ln(10)) — verifies the Log10 numerator/denominator fix.
    /// </summary>
    [TestMethod]
    public void Derivate_Log10_ReturnsOneOverXLn10()
    {
        Expression<Func<double, double>> f = x => double.Log10(x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        foreach (double xv in new[] { 0.5, 1.0, 2.0, 10.0 })
            Assert.AreEqual(1.0 / (xv * Math.Log(10)), compiled(xv), 1e-9, $"d/dx[log10(x)] at x={xv}");
    }

    // ── Trigonometry ─────────────────────────────────────────────────────────

    /// <summary>
    /// d/dx[tan(x)] = 1/cos²(x).
    /// </summary>
    [TestMethod]
    public void Derivate_Tan_ReturnsSecSquared()
    {
        Expression<Func<double, double>> f = x => double.Tan(x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        foreach (double xv in new[] { 0.0, 0.3, -0.5, 1.0 })
            Assert.AreEqual(1.0 / (Math.Cos(xv) * Math.Cos(xv)), compiled(xv), 1e-9, $"d/dx[tan(x)] at x={xv}");
    }

    // ── Hyperbolic ───────────────────────────────────────────────────────────

    /// <summary>
    /// d/dx[sinh(x)] = cosh(x), verified via finite-difference fallback.
    /// </summary>
    [TestMethod]
    public void Derivate_Sinh_MatchesCosh()
    {
        Expression<Func<double, double>> f = x => double.Sinh(x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        foreach (double xv in new[] { -1.0, 0.0, 0.5, 2.0 })
            Assert.AreEqual(Math.Cosh(xv), compiled(xv), 1e-4, $"d/dx[sinh(x)] at x={xv}");
    }

    /// <summary>
    /// d/dx[cosh(x)] = sinh(x), verified via finite-difference fallback.
    /// </summary>
    [TestMethod]
    public void Derivate_Cosh_MatchesSinh()
    {
        Expression<Func<double, double>> f = x => double.Cosh(x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        foreach (double xv in new[] { -1.0, 0.0, 0.5, 2.0 })
            Assert.AreEqual(Math.Sinh(xv), compiled(xv), 1e-4, $"d/dx[cosh(x)] at x={xv}");
    }

    /// <summary>
    /// d/dx[tanh(x)] = sech²(x) = 1/cosh²(x), verified via finite-difference fallback.
    /// </summary>
    [TestMethod]
    public void Derivate_Tanh_MatchesSechSquared()
    {
        Expression<Func<double, double>> f = x => double.Tanh(x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        foreach (double xv in new[] { -1.0, 0.0, 0.5, 2.0 })
            Assert.AreEqual(1.0 / (Math.Cosh(xv) * Math.Cosh(xv)), compiled(xv), 1e-4, $"d/dx[tanh(x)] at x={xv}");
    }

    // ── Quotient rule ─────────────────────────────────────────────────────────

    /// <summary>
    /// d/dx[x/(x²+1)] = (1−x²)/(x²+1)².
    /// </summary>
    [TestMethod]
    public void Derivate_Quotient_XOverXSquaredPlusOne()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Divide(
            x,
            Expression.Add(Expression.Multiply(x, x), Expression.Constant(1.0)));
        var f = Expression.Lambda<Func<double, double>>(body, x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        foreach (double xv in new[] { -2.0, -1.0, 0.0, 1.0, 2.0 })
        {
            double expected = (1.0 - xv * xv) / Math.Pow(xv * xv + 1.0, 2);
            Assert.AreEqual(expected, compiled(xv), 1e-9, $"d/dx[x/(x²+1)] at x={xv}");
        }
    }

    // ── Constant derivative ───────────────────────────────────────────────────

    /// <summary>
    /// d/dx[5] = 0.
    /// </summary>
    [TestMethod]
    public void Derivate_Constant_ReturnsZero()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var f = Expression.Lambda<Func<double, double>>(Expression.Constant(5.0), x);
        var df = (Expression<Func<double, double>>)derivation.Derivate(f);
        var compiled = df.Compile();

        Assert.AreEqual(0.0, compiled(42.0), 1e-9);
    }

    // ── Float unknown function fallback ───────────────────────────────────────

    /// <summary>
    /// After fixing DeriveUnknownMethodCall to use typeof(T), unknown float functions use finite difference.
    /// </summary>
    [TestMethod]
    public void Derivate_UnknownFloatFunction_UsesFiniteDifferenceFallback()
    {
        static float FloatCube(float x) => x * x * x;
        ExpressionDerivation<float> floatDerivation = new("x");
        Expression<Func<float, float>> f = x => FloatCube(x);
        var df = (Expression<Func<float, float>>)floatDerivation.Derivate(f);
        var compiled = df.Compile();

        foreach (float xv in new[] { -2f, -0.5f, 0.5f, 1.5f })
            Assert.AreEqual(3f * xv * xv, compiled(xv), 5e-2f, $"Float fallback derivative at x={xv}");
    }

}
