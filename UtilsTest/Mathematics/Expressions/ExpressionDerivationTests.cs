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
        ExpressionDerivation<float> floatDerivation = new("x");
        Expression<Func<float, float>> f = x => x * x * x;
        var df = (Expression<Func<float, float>>)floatDerivation.Derivate(f);
        var compiled = df.Compile();

        foreach (float xv in new[] { -2f, -0.5f, 0.5f, 1.5f })
            Assert.AreEqual(3f * xv * xv, compiled(xv), 5e-2f, $"Float fallback derivative at x={xv}");
    }

    /// <summary>
    /// Computes a cubic value for expression-tree method-call fallback tests.
    /// </summary>
    /// <param name="x">Input value.</param>
    /// <returns>The cubic value of <paramref name="x"/>.</returns>
    private static float FloatCube(float x) => x * x * x;

    // ── Unsupported scalar-type capability (item 30) ─────────────────────────

    /// <summary>
    /// <see cref="decimal"/> satisfies <see cref="System.Numerics.IFloatingPoint{TSelf}"/> but declares no
    /// <c>Exp</c> method. Differentiating a <c>double.Exp</c> call node against a decimal-configured
    /// transformer must fail with a clear <see cref="NotSupportedException"/> instead of an incidental
    /// reflection null failure deep inside <see cref="Expression.Call(System.Reflection.MethodInfo, Expression[])"/>.
    /// </summary>
    [TestMethod]
    public void Derivate_ExpCall_UnsupportedScalarType_ThrowsClearException()
    {
        ExpressionDerivation<decimal> decimalDerivation = new("x");
        var x = Expression.Parameter(typeof(decimal), "x");
        var expCall = Expression.Call(typeof(double).GetMethod(nameof(double.Exp), [typeof(double)]), Expression.Convert(x, typeof(double)));
        var f = Expression.Lambda<Func<decimal, double>>(expCall, x);

        // Transformation rules are dispatched through reflection (see ExpressionTransformer.Transform),
        // so the NotSupportedException surfaces wrapped in a TargetInvocationException.
        var invocationException = Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => decimalDerivation.Derivate(f));
        Assert.IsInstanceOfType(invocationException.InnerException, typeof(NotSupportedException));
        var ex = (NotSupportedException)invocationException.InnerException!;
        StringAssert.Contains(ex.Message, "Exp");
        StringAssert.Contains(ex.Message, "Decimal");
    }

    /// <summary>
    /// Arithmetic-only expressions (no transcendental functions) remain differentiable for
    /// <see cref="decimal"/> even though it lacks Log/Sin/Exp/etc.
    /// </summary>
    [TestMethod]
    public void Derivate_ArithmeticOnlyExpression_WorksForDecimal()
    {
        ExpressionDerivation<decimal> decimalDerivation = new("x");
        Expression<Func<decimal, decimal>> f = x => x * x;
        var result = (Expression<Func<decimal, decimal>>)decimalDerivation.Derivate(f);
        var derivative = result.Compile();

        foreach (decimal xv in new[] { -2m, -0.5m, 0m, 1.25m, 3m })
            Assert.AreEqual(2m * xv, derivative(xv), $"d/dx[x*x] for decimal at x={xv}");
    }

    // ── Parameter identity (item 31) and re-entrancy (item 32) ───────────────

    /// <summary>
    /// Two distinct <see cref="ParameterExpression"/> objects legally sharing the differentiation
    /// variable's name cannot be resolved unambiguously by name alone; the previous name-based lookup
    /// would silently differentiate as if both were the target variable. The fix rejects this instead
    /// of guessing.
    /// </summary>
    [TestMethod]
    public void Derivate_TwoDistinctParametersWithSameName_ThrowsAmbiguousException()
    {
        var x1 = Expression.Parameter(typeof(double), "x");
        var x2 = Expression.Parameter(typeof(double), "x");
        var f = Expression.Lambda<Func<double, double, double>>(Expression.Add(x1, x2), x1, x2);

        Assert.ThrowsExactly<InvalidOperationException>(() => derivation.Derivate(f));
    }

    /// <summary>
    /// When the configured parameter name is absent from the lambda's own parameter list, the target
    /// variable cannot be resolved at all and differentiation must fail clearly rather than silently
    /// matching an unrelated same-named parameter deep in the expression tree.
    /// </summary>
    [TestMethod]
    public void Derivate_ParameterNameNotInLambda_ThrowsClearException()
    {
        var y = Expression.Parameter(typeof(double), "y");
        var f = Expression.Lambda<Func<double, double>>(y, y);

        Assert.ThrowsExactly<InvalidOperationException>(() => derivation.Derivate(f));
    }

    /// <summary>
    /// Each <see cref="ExpressionDerivation{T}.Derivate"/> call resolves its target parameter into a
    /// fresh, isolated worker instance rather than mutating shared instance state, so concurrent calls
    /// on one shared transformer instance cannot corrupt each other's result (item 32).
    /// </summary>
    [TestMethod]
    public void Derivate_ConcurrentCalls_AreIsolatedPerCall()
    {
        var shared = new ExpressionDerivation<double>("x");
        var results = new Expression<Func<double, double>>[64];

        System.Threading.Tasks.Parallel.For(0, results.Length, i =>
        {
            var x = Expression.Parameter(typeof(double), "x");
            var f = Expression.Lambda<Func<double, double>>(Expression.Multiply(Expression.Constant((double)(i + 1)), x), x);
            results[i] = (Expression<Func<double, double>>)shared.Derivate(f);
        });

        for (int i = 0; i < results.Length; i++)
        {
            double expected = i + 1;
            double actual = results[i].Compile()(2.5);
            Assert.AreEqual(expected, actual, 1e-9, $"Concurrent derivate #{i}");
        }
    }

    // ── Conversion type preservation (item 33) ────────────────────────────────

    /// <summary>
    /// Differentiating a widening numeric conversion (here <c>decimal</c> to <c>double</c>) must
    /// preserve the conversion's declared result type, so the produced lambda still matches the
    /// delegate type the caller compiled the source expression against.
    /// </summary>
    [TestMethod]
    public void Derivate_WideningConversion_PreservesDeclaredResultType()
    {
        ExpressionDerivation<decimal> decimalDerivation = new("x");
        var x = Expression.Parameter(typeof(decimal), "x");
        var body = Expression.Convert(x, typeof(double));
        var f = Expression.Lambda<Func<decimal, double>>(body, x);

        var result = (Expression<Func<decimal, double>>)decimalDerivation.Derivate(f);
        var derivative = result.Compile();

        Assert.AreEqual(1.0, derivative(3m), 1e-9);
    }

    /// <summary>
    /// A checked conversion that actually changes the type (here <c>double</c> to <c>int</c>) has no
    /// well-defined symbolic derivative and must be rejected explicitly rather than silently stripped,
    /// which would otherwise return a value of the wrong type.
    /// </summary>
    [TestMethod]
    public void Derivate_NarrowingCheckedConversion_ThrowsClearException()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.ConvertChecked(x, typeof(int));
        var f = Expression.Lambda<Func<double, int>>(body, x);

        var invocationException = Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => derivation.Derivate(f));
        Assert.IsInstanceOfType(invocationException.InnerException, typeof(NotSupportedException));
    }

}
