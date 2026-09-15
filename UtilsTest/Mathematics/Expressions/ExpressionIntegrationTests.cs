using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides low-level regression coverage for symbolic integration mechanics and edge cases.
/// </summary>
[TestClass]
public class ExpressionIntegrationTests
{
    readonly ExpressionIntegration<double> integration = new ExpressionIntegration<double>("x");
    readonly ExpressionSimplifier simplifier = new ExpressionSimplifier();

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

    // ── Power rule ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures a manually represented power of minus one integrates to the natural logarithm.
    /// The C-syntax compiler currently represents this shape as a method call, so this structural rule remains in C#.
    /// </summary>
    [TestMethod]
    public void Integrate_PowerMinusOne_ReturnsLog()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Power(x, Expression.Constant(-1.0));
        var function = Expression.Lambda<Func<double, double>>(body, x);
        var result = (Expression<Func<double, double>>)integration.Integrate(function);
        var compiled = result.Compile();

        foreach (double value in new[] { 0.5, 1.0, Math.E, 5.0 })
        {
            Assert.AreEqual(Math.Log(value), compiled(value), 1e-9, $"Integral of x^(-1) at x={value}");
        }
    }

    // ── Trigonometry ─────────────────────────────────────────────────────────

    // ── Exponential ───────────────────────────────────────────────────────────

    // ── Logarithms ────────────────────────────────────────────────────────────

    // ── Linear combinations ───────────────────────────────────────────────────

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

        Assert.ThrowsExactly<SymbolicParameterException>(() => integration.Integrate(f));
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

    // ── Conversion type preservation (item 33) ────────────────────────────────

    /// <summary>
    /// Integrating a widening numeric conversion (here <c>decimal</c> to <c>double</c>) must preserve
    /// the conversion's declared result type, so the produced lambda still matches the delegate type
    /// the caller compiled the source expression against.
    /// </summary>
    [TestMethod]
    public void Integrate_WideningConversion_PreservesDeclaredResultType()
    {
        // n is a foreign (non-integration-variable) parameter, so integrating it takes the
        // "constant factor" branch (n * x) rather than the target-parameter branch (x**2/2, which
        // relies on Expression.Power and is unsupported for decimal regardless of this fix).
        ExpressionIntegration<decimal> decimalIntegration = new("x");
        var x = Expression.Parameter(typeof(decimal), "x");
        var n = Expression.Parameter(typeof(decimal), "n");
        var body = Expression.Convert(n, typeof(double));
        var f = Expression.Lambda<Func<decimal, decimal, double>>(body, x, n);

        var result = (Expression<Func<decimal, decimal, double>>)decimalIntegration.Integrate(f);
        var integral = result.Compile();

        Assert.AreEqual((double)(5m * 3m), integral(3m, 5m), 1e-9);
    }

    /// <summary>
    /// A checked conversion that actually changes the type (here <c>double</c> to <c>int</c>) has no
    /// well-defined symbolic integral and must be rejected explicitly rather than silently stripped,
    /// which would otherwise return a value of the wrong type.
    /// </summary>
    [TestMethod]
    public void Integrate_NarrowingCheckedConversion_ThrowsClearException()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.ConvertChecked(x, typeof(int));
        var f = Expression.Lambda<Func<double, int>>(body, x);

        var invocationException = Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => integration.Integrate(f));
        Assert.IsInstanceOfType(invocationException.InnerException, typeof(NotSupportedException));
    }

    // ── Widening-only numeric conversions (PR 448 review) ─────────────────────

    /// <summary>
    /// An unchecked <c>double</c> to <c>float</c> conversion loses precision and is not a recognized
    /// widening; it must be rejected rather than silently accepted just because both endpoints are
    /// native numeric types (the same bug as <c>ExpressionDerivation{T}.PreserveConversion</c>, since
    /// this method is a duplicate of it).
    /// </summary>
    [TestMethod]
    public void Integrate_DoubleToFloatConversion_ThrowsClearException()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Convert(x, typeof(float));
        var f = Expression.Lambda<Func<double, float>>(body, x);

        var invocationException = Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => integration.Integrate(f));
        Assert.IsInstanceOfType(invocationException.InnerException, typeof(NotSupportedException));
    }

    /// <summary>
    /// An unchecked <c>double</c> to <c>int</c> conversion truncates and has no well-defined symbolic
    /// integral; it must be rejected instead of silently producing a value of the wrong type.
    /// </summary>
    [TestMethod]
    public void Integrate_DoubleToIntConversion_ThrowsClearException()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Convert(x, typeof(int));
        var f = Expression.Lambda<Func<double, int>>(body, x);

        var invocationException = Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => integration.Integrate(f));
        Assert.IsInstanceOfType(invocationException.InnerException, typeof(NotSupportedException));
    }

    /// <summary>
    /// A genuine widening (<c>float</c> to <c>double</c>) must still be preserved after tightening the
    /// widening check, so the fix does not overcorrect into rejecting legitimate conversions.
    /// </summary>
    [TestMethod]
    public void Integrate_FloatToDoubleConversion_PreservesDeclaredResultType()
    {
        ExpressionIntegration<float> floatIntegration = new("x");
        var x = Expression.Parameter(typeof(float), "x");
        var n = Expression.Parameter(typeof(float), "n");
        var body = Expression.Convert(n, typeof(double));
        var f = Expression.Lambda<Func<float, float, double>>(body, x, n);

        var result = (Expression<Func<float, float, double>>)floatIntegration.Integrate(f);
        var integral = result.Compile();

        Assert.AreEqual((double)(5f * 3f), integral(3f, 5f), 1e-4);
    }

    // ── Generic-T constants in Power-rule integration (item 34) ───────────────

    /// <summary>
    /// Integrating the bare target parameter (∫x dx = x²/2) previously built
    /// <c>Expression.Power(e, ...)</c> directly; that overload only ever works when the operand is
    /// literally <see cref="double"/>, so it threw for any other scalar type even though the exponent
    /// constant itself was already correctly typed. It now resolves a type-appropriate <c>Pow</c> method
    /// through <see cref="MathMethodResolver"/> instead, so this works for <see cref="float"/> too.
    /// </summary>
    [TestMethod]
    public void Integrate_TargetParameter_WorksForFloat()
    {
        ExpressionIntegration<float> floatIntegration = new("x");
        var x = Expression.Parameter(typeof(float), "x");
        var f = Expression.Lambda<Func<float, float>>(x, x);

        var result = (Expression<Func<float, float>>)floatIntegration.Integrate(f);
        var integral = result.Compile();

        foreach (float xv in new[] { -2f, -0.5f, 0f, 1.25f, 3f })
            Assert.AreEqual(xv * xv / 2f, integral(xv), 1e-3f, $"∫x dx (float) at x={xv}");
    }

    /// <summary>
    /// ∫c/x² dx = -c/x for a non-<see cref="double"/> scalar type. The shifted exponent (<c>1-n</c>)
    /// must be a <typeparamref name="float"/>-typed constant shared between the numerator's power call
    /// and the denominator, rather than a raw <see cref="double"/> constant combined with
    /// <typeparamref name="float"/>-typed expressions (which fails to construct).
    /// </summary>
    [TestMethod]
    public void Integrate_ConstantDividedByPower_WorksForFloat()
    {
        ExpressionIntegration<float> floatIntegration = new("x");
        var x = Expression.Parameter(typeof(float), "x");
        var powMethod = typeof(float).GetMethod(nameof(float.Pow), [typeof(float), typeof(float)]);
        var body = Expression.Divide(
            Expression.Constant(3f),
            Expression.Power(x, Expression.Constant(2f), powMethod));
        var f = Expression.Lambda<Func<float, float>>(body, x);

        var result = (Expression<Func<float, float>>)floatIntegration.Integrate(f);
        var integral = result.Compile();

        foreach (float xv in new[] { -2f, -1f, 0.5f, 2f })
            Assert.AreEqual(-3f / xv, integral(xv), 0.05f, $"∫3/x² dx (float) at x={xv}");
    }

    /// <summary>
    /// ∫c/√x dx = 2c·√x for a non-<see cref="double"/> scalar type. The doubled-constant factor must be
    /// <typeparamref name="float"/>-typed rather than a raw <see cref="double"/> constant multiplied
    /// against the <typeparamref name="float"/>-typed <c>Sqrt</c> call result.
    /// </summary>
    [TestMethod]
    public void Integrate_ConstantDividedBySqrt_WorksForFloat()
    {
        ExpressionIntegration<float> floatIntegration = new("x");
        var x = Expression.Parameter(typeof(float), "x");
        var sqrtMethod = typeof(float).GetMethod(nameof(float.Sqrt), [typeof(float)]);
        var body = Expression.Divide(Expression.Constant(3f), Expression.Call(sqrtMethod, x));
        var f = Expression.Lambda<Func<float, float>>(body, x);

        var result = (Expression<Func<float, float>>)floatIntegration.Integrate(f);
        var integral = result.Compile();

        foreach (float xv in new[] { 0.5f, 1f, 2f, 4f })
            Assert.AreEqual(6f * MathF.Sqrt(xv), integral(xv), 0.05f, $"∫3/√x dx (float) at x={xv}");
    }

    // ── Domain restrictions (item 35) ─────────────────────────────────────────

    /// <summary>
    /// ∫1/x dx = ln|x|, which (unlike a bare <c>Log(x)</c>) is defined for negative <c>x</c> too, just
    /// like the original <c>1/x</c> it replaces.
    /// </summary>
    [TestMethod]
    public void Integrate_ReciprocalX_IsValidOnNegativeDomain()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Divide(Expression.Constant(1.0), x);
        var f = Expression.Lambda<Func<double, double>>(body, x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        foreach (double xv in new[] { -5.0, -1.0, -0.25 })
        {
            double actual = compiled(xv);
            Assert.IsFalse(double.IsNaN(actual), $"∫1/x dx at x={xv} should not be NaN");
            Assert.AreEqual(Math.Log(Math.Abs(xv)), actual, 1e-9, $"∫1/x dx at x={xv}");
        }
    }

    /// <summary>
    /// ∫tan(x) dx = -ln|cos(x)|. At <c>x = 2.0</c>, <c>cos(x)</c> is negative
    /// (<c>cos(2.0) ≈ -0.416</c>), so a bare <c>Log(Cos(x))</c> would return NaN even though
    /// <c>tan(x)</c> itself is perfectly well-defined there.
    /// </summary>
    [TestMethod]
    public void Integrate_Tan_IsValidWhenCosineIsNegative()
    {
        Expression<Func<double, double>> f = x => double.Tan(x);
        var result = (Expression<Func<double, double>>)integration.Integrate(f);
        var compiled = result.Compile();

        const double xv = 2.0;
        Assert.IsTrue(Math.Cos(xv) < 0, "Test premise: cos(2.0) must be negative.");
        double actual = compiled(xv);
        Assert.IsFalse(double.IsNaN(actual), $"∫tan(x) dx at x={xv} should not be NaN");
        Assert.AreEqual(-Math.Log(Math.Abs(Math.Cos(xv))), actual, 1e-9, $"∫tan(x) dx at x={xv}");
    }

    // ── Parameter-instance resolution (TODO-pass4 item #47) ────────────────────

    /// <summary>
    /// Two parameters sharing the same declared name (including <see langword="null"/> for unnamed
    /// parameters) previously made name-based resolution ambiguous even when only one of them was
    /// intended. Targeting the exact instance sidesteps the name entirely.
    /// </summary>
    [TestMethod]
    public void Integrate_ByParameterInstance_UnnamedParameter_Works()
    {
        var x = Expression.Parameter(typeof(double));
        var body = Expression.Multiply(Expression.Constant(2.0), x);
        var f = Expression.Lambda<Func<double, double>>(body, x);

        ExpressionIntegration<double> integrationByX = new(x);
        var result = (Expression<Func<double, double>>)integrationByX.Integrate(f);

        // integral of 2x dx = x^2; at x=4 -> 16
        Assert.AreEqual(16.0, result.Compile()(4.0), 1e-9);
    }

    /// <summary>
    /// A parameter instance that does not belong to the lambda being integrated must fail with a
    /// contextual diagnostic instead of silently matching nothing or the wrong variable.
    /// </summary>
    [TestMethod]
    public void Integrate_ByParameterInstance_ParameterNotInLambda_Throws()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var foreign = Expression.Parameter(typeof(double), "x");
        var f = Expression.Lambda<Func<double, double>>(x, x);

        ExpressionIntegration<double> integrationByForeign = new(foreign);

        Assert.ThrowsExactly<SymbolicParameterException>(() => integrationByForeign.Integrate(f));
    }

    /// <summary>
    /// <see cref="ExpressionTransformer.Transform(Expression)"/> is the public contract every transformer
    /// must implement. For <see cref="ExpressionIntegration{T}"/>, a <see cref="LambdaExpression"/> carries
    /// the parameter list needed to resolve the integration variable, so <c>Transform</c> must actually
    /// integrate against <c>x</c> - not silently treat it as an unrelated free variable (which would
    /// crash or produce a wrong result, since the target parameter is only resolved inside
    /// <see cref="ExpressionIntegration{T}.Integrate(LambdaExpression)"/>, never by the public constructors).
    /// </summary>
    [TestMethod]
    public void Transform_OnLambdaExpression_IntegratesAgainstItsParameter()
    {
        Expression<Func<double, double>> f = x => 1.0;
        var localIntegration = new ExpressionIntegration<double>("x");

        var viaTransform = (Expression<Func<double, double>>)localIntegration.Transform(f);
        var viaIntegrate = (Expression<Func<double, double>>)localIntegration.Integrate(f);

        Assert.AreEqual(3.0, viaTransform.Compile()(3.0), 1e-9);
        Assert.AreEqual(viaIntegrate, viaTransform, ExpressionComparer.Default);
    }

    /// <summary>
    /// Without a <see cref="LambdaExpression"/>, <see cref="ExpressionIntegration{T}.Transform(Expression)"/>
    /// has no declared parameter list to resolve the integration variable from, and must fail explicitly
    /// rather than silently guessing or corrupting shared state.
    /// </summary>
    [TestMethod]
    public void Transform_OnBareExpression_ThrowsNotSupported()
    {
        var localIntegration = new ExpressionIntegration<double>("x");

        Assert.ThrowsExactly<NotSupportedException>(() => localIntegration.Transform(Expression.Constant(5.0)));
    }

    /// <summary>
    /// When this instance was constructed with an exact <see cref="ParameterExpression"/> identity, the
    /// integration variable is already unambiguously known, so <c>Transform</c> can integrate a bare
    /// (non-lambda) expression directly instead of requiring a carrier <see cref="LambdaExpression"/>.
    /// </summary>
    [TestMethod]
    public void Transform_OnBareExpression_WithParameterIdentityConstructor_Integrates()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var localIntegration = new ExpressionIntegration<double>(x);

        Expression result = localIntegration.Transform(x);
        var compiled = Expression.Lambda<Func<double, double>>(result, x).Compile();

        const double xv = 3.0;
        Assert.AreEqual(xv * xv / 2.0, compiled(xv), 1e-9, "∫x dx = x²/2");
    }

}
