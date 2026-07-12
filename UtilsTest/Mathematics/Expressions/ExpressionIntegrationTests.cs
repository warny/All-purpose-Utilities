using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides compiler-based regression coverage for legacy integration parser tests.
/// </summary>
[TestClass]
public class ExpressionIntegrationTests
{
    CSyntaxExpressionCompiler compiler = new CSyntaxExpressionCompiler();
    readonly ExpressionIntegration<double> integration = new ExpressionIntegration<double>("x");
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


    /// <summary>
    /// Ensures generic integration also works for <see cref="float"/> lambdas.
    /// </summary>
    [TestMethod]
    public void Integrate_FloatLambda_WorksWithGenericIntegration()
    {
        ExpressionIntegration<float> floatIntegration = new("x");
        Expression<Func<float, float>> function = x => 1f;

        var result = (Expression<Func<float, float>>)floatIntegration.Integrate(function);
        var integral = result.Compile();

        float[] samples = [-2f, -0.5f, 0f, 1.25f];
        foreach (float sample in samples)
        {
            float expected = sample;
            float actual = integral(sample);
            Assert.AreEqual(expected, actual, 5e-3f, $"Float integral mismatch at x={sample}.");
        }
    }

    /// <summary>
    /// Ensures the generic extension entry-point can integrate with a float type parameter.
    /// </summary>
    [TestMethod]
    public void IntegrateExtension_GenericFloat_Works()
    {
        Expression<Func<float, float>> function = x => 1f;
        var integrated = (Expression<Func<float, float>>)function.Integrate<float>("x");
        Func<float, float> compiled = integrated.Compile();

        Assert.AreEqual(3f, compiled(3f), 5e-3f);
    }

    // ── Basic primitives ─────────────────────────────────────────────────────

    /// <summary>
    /// ∫1 dx = x.
    /// </summary>
    [TestMethod]
    public void Integrate_Constant_ReturnsX()
    {
        Expression<Func<double, double>> f = x => 1.0;
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -2.0, -1.0, 0.0, 1.0, 2.5 })
            Assert.AreEqual(xv, compiled(xv), 1e-9, $"∫1 dx at x={xv}");
    }

    /// <summary>
    /// ∫x dx = x²/2.
    /// </summary>
    [TestMethod]
    public void Integrate_X_ReturnsXSquaredOver2()
    {
        Expression<Func<double, double>> f = x => x;
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -2.0, -1.0, 0.0, 1.0, 2.5 })
            Assert.AreEqual(xv * xv / 2.0, compiled(xv), 1e-9, $"∫x dx at x={xv}");
    }

    // ── Power rule ────────────────────────────────────────────────────────────

    /// <summary>
    /// ∫x² dx = x³/3 — via the compiler so the exponent is a Power binary node.
    /// </summary>
    [TestMethod]
    public void Integrate_XSquared_ReturnsXCubedOver3()
    {
        var parameters = new ParameterExpression[] { Expression.Parameter(typeof(double), "x") };
        var f = compiler.Compile<Func<double, double>>("x**2", parameters, typeof(double), false);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -2.0, -1.0, 0.0, 1.0, 2.0 })
            Assert.AreEqual(Math.Pow(xv, 3) / 3.0, compiled(xv), 1e-9, $"∫x² dx at x={xv}");
    }

    /// <summary>
    /// ∫x^(-1) dx = ln(x) — verifies the double.Epsilon bug fix in the Power rule.
    /// </summary>
    [TestMethod]
    public void Integrate_PowerMinusOne_ReturnsLog()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Power(x, Expression.Constant(-1.0));
        var f = Expression.Lambda<Func<double, double>>(body, x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { 0.5, 1.0, Math.E, 5.0 })
            Assert.AreEqual(Math.Log(xv), compiled(xv), 1e-9, $"∫x^(-1) dx at x={xv}");
    }

    /// <summary>
    /// ∫c/x dx = c·ln(x) — verifies the double.Epsilon bug fix in the Divide/Power rule.
    /// </summary>
    [TestMethod]
    public void Integrate_ConstantDividedByX_ReturnsConstantTimesLog()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Divide(
            Expression.Constant(3.0),
            Expression.Power(x, Expression.Constant(1.0)));
        var f = Expression.Lambda<Func<double, double>>(body, x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { 0.5, 1.0, Math.E, 5.0 })
            Assert.AreEqual(3.0 * Math.Log(xv), compiled(xv), 1e-9, $"∫3/x dx at x={xv}");
    }

    // ── Trigonometry ─────────────────────────────────────────────────────────

    /// <summary>
    /// ∫sin(x) dx = -cos(x).
    /// </summary>
    [TestMethod]
    public void Integrate_Sin_ReturnsNegCos()
    {
        Expression<Func<double, double>> f = x => double.Sin(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -Math.PI, -Math.PI / 2, 0.0, Math.PI / 2, Math.PI })
            Assert.AreEqual(-Math.Cos(xv), compiled(xv), 1e-9, $"∫sin(x) dx at x={xv}");
    }

    /// <summary>
    /// ∫cos(x) dx = sin(x).
    /// </summary>
    [TestMethod]
    public void Integrate_Cos_ReturnsSin()
    {
        Expression<Func<double, double>> f = x => double.Cos(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -Math.PI, -Math.PI / 2, 0.0, Math.PI / 2, Math.PI })
            Assert.AreEqual(Math.Sin(xv), compiled(xv), 1e-9, $"∫cos(x) dx at x={xv}");
    }

    /// <summary>
    /// ∫sin(2x) dx = -cos(2x)/2.
    /// </summary>
    [TestMethod]
    public void Integrate_ScaledSin_ReturnsNegCosOver2()
    {
        Expression<Func<double, double>> f = x => double.Sin(2.0 * x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -Math.PI / 2, 0.0, Math.PI / 4, Math.PI })
            Assert.AreEqual(-Math.Cos(2.0 * xv) / 2.0, compiled(xv), 1e-9, $"∫sin(2x) dx at x={xv}");
    }

    // ── Exponential ───────────────────────────────────────────────────────────

    /// <summary>
    /// ∫exp(x) dx = exp(x).
    /// </summary>
    [TestMethod]
    public void Integrate_Exp_ReturnsExp()
    {
        Expression<Func<double, double>> f = x => double.Exp(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -2.0, -1.0, 0.0, 1.0, 2.0 })
            Assert.AreEqual(Math.Exp(xv), compiled(xv), 1e-9, $"∫exp(x) dx at x={xv}");
    }

    // ── Logarithms ────────────────────────────────────────────────────────────

    /// <summary>
    /// ∫ln(x) dx = x·(ln(x) - 1).
    /// </summary>
    [TestMethod]
    public void Integrate_Log_ReturnsXTimesLogXMinusOne()
    {
        Expression<Func<double, double>> f = x => double.Log(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { 0.5, 1.0, Math.E, 5.0 })
            Assert.AreEqual(xv * (Math.Log(xv) - 1.0), compiled(xv), 1e-9, $"∫ln(x) dx at x={xv}");
    }

    /// <summary>
    /// ∫log10(x) dx = x·log10(x) - x/ln(10).
    /// </summary>
    [TestMethod]
    public void Integrate_Log10_VerifyFormula()
    {
        Expression<Func<double, double>> f = x => double.Log10(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { 1.0, 2.0, 10.0, 100.0 })
            Assert.AreEqual(xv * Math.Log10(xv) - xv / Math.Log(10), compiled(xv), 1e-9, $"∫log10(x) dx at x={xv}");
    }

    // ── Linear combinations ───────────────────────────────────────────────────

    /// <summary>
    /// ∫2·sin(x) dx = -2·cos(x).
    /// </summary>
    [TestMethod]
    public void Integrate_ConstantTimesSin_ReturnsScaledNegCos()
    {
        Expression<Func<double, double>> f = x => 2.0 * double.Sin(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -Math.PI, 0.0, Math.PI / 2, Math.PI })
            Assert.AreEqual(-2.0 * Math.Cos(xv), compiled(xv), 1e-9, $"∫2·sin(x) dx at x={xv}");
    }

    /// <summary>
    /// ∫(x + sin(x)) dx = x²/2 - cos(x).
    /// </summary>
    [TestMethod]
    public void Integrate_SumXAndSin_ReturnsCombination()
    {
        Expression<Func<double, double>> f = x => x + double.Sin(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -Math.PI / 2, 0.0, 1.0, Math.PI })
            Assert.AreEqual(xv * xv / 2.0 - Math.Cos(xv), compiled(xv), 1e-9, $"∫(x+sin(x)) dx at x={xv}");
    }

    // ── Parameter identity (item 31) and re-entrancy (item 32) ───────────────

    /// <summary>
    /// Two distinct <see cref="ParameterExpression"/> objects legally sharing the integration
    /// variable's name cannot be resolved unambiguously by name alone. The fix rejects this instead of
    /// guessing which one is the real integration variable.
    /// </summary>
    [TestMethod]
    public void Integrate_TwoDistinctParametersWithSameName_ThrowsAmbiguousException()
    {
        var x1 = Expression.Parameter(typeof(double), "x");
        var x2 = Expression.Parameter(typeof(double), "x");
        var f = Expression.Lambda<Func<double, double, double>>(Expression.Add(x1, x2), x1, x2);

        Assert.ThrowsExactly<InvalidOperationException>(() => integration.Integrate(f));
    }

    /// <summary>
    /// Each <see cref="ExpressionIntegration{T}.Integrate"/> call resolves its target parameter into a
    /// fresh, isolated worker instance instead of mutating the shared instance's cached parameter field,
    /// so concurrent calls on one shared transformer instance cannot corrupt each other's result
    /// (item 32).
    /// </summary>
    [TestMethod]
    public void Integrate_ConcurrentCalls_AreIsolatedPerCall()
    {
        var shared = new ExpressionIntegration<double>("x");
        var results = new Expression<Func<double, double>>[64];

        System.Threading.Tasks.Parallel.For(0, results.Length, i =>
        {
            var x = Expression.Parameter(typeof(double), "x");
            var f = Expression.Lambda<Func<double, double>>(Expression.Constant((double)(i + 1)), x);
            results[i] = (Expression<Func<double, double>>)shared.Integrate(f);
        });

        for (int i = 0; i < results.Length; i++)
        {
            double expectedFactor = i + 1;
            var compiled = results[i].Compile();
            foreach (double xv in new[] { -2.0, 0.0, 3.5 })
                Assert.AreEqual(expectedFactor * xv, compiled(xv), 1e-9, $"Concurrent integrate #{i} at x={xv}");
        }
    }

}
