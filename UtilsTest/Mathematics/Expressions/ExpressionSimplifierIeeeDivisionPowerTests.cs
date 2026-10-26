using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S5 P12 (IEEE-754 floating-point division by zero), see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>. P12: an ordinary
/// <see cref="double"/>/<see cref="float"/> division by <c>±0</c> no longer throws while simplifying, a constant
/// division folds to the exact runtime result (infinities, <c>NaN</c>, signed zeros), and integral and decimal
/// division by zero keep throwing.
/// </summary>
/// <remarks>
/// Every reference value is computed by compiling the same <see cref="System.Linq.Expressions"/> tree, the semantics
/// the simplified tree must keep; signed zeros are compared by their bits and every <c>NaN</c> is treated as equal.
/// </remarks>
[TestClass]
public class ExpressionSimplifierIeeeDivisionPowerTests
{
    // ------------------------------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------------------------------

    /// <summary>Inputs the symbolic cases are evaluated on, special values included.</summary>
    private static readonly double[] Samples = [1.5, -2.0, 0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon, double.MaxValue];

    /// <summary>Counts the calls of <see cref="CountingIdentity"/>.</summary>
    [ThreadStatic]
    private static int _countingIdentityCalls;

    /// <summary>A method with an observable side effect: it counts its calls.</summary>
    /// <param name="value">The value returned unchanged.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double CountingIdentity(double value)
    {
        _countingIdentityCalls++;
        return value;
    }

    /// <summary>A method that always throws.</summary>
    /// <param name="value">Ignored.</param>
    /// <returns>Never returns.</returns>
    public static double Throwing(double value) => throw new InvalidOperationException("observable failure");

    /// <summary>A user-defined division, so a node built with it is never ordinary arithmetic.</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>Always <c>42</c>, so an erased or folded custom operator is observable.</returns>
    public static double CustomDivide(double a, double b) => 42.0;

    /// <summary>A user-defined power, so a node built with it is never ordinary arithmetic.</summary>
    /// <param name="a">The base.</param>
    /// <param name="b">The exponent.</param>
    /// <returns>Always <c>42</c>, so an erased or folded custom operator is observable.</returns>
    public static double CustomPower(double a, double b) => 42.0;

    /// <summary>Gets a public static method of this class by name.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Method(string name) => typeof(ExpressionSimplifierIeeeDivisionPowerTests).GetMethod(name)!;

    /// <summary>Simplifies <paramref name="expression"/> with a fresh built-in simplifier.</summary>
    /// <param name="expression">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Simplify(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Formats a result so that every <c>NaN</c> compares equal and signed zeros differ.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A comparison key.</returns>
    private static string Key(double value) => double.IsNaN(value) ? "NaN" : BitConverter.DoubleToInt64Bits(value).ToString("X16");

    /// <summary>Formats a <see cref="float"/> result like <see cref="Key(double)"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A comparison key.</returns>
    private static string Key(float value) => float.IsNaN(value) ? "NaN" : BitConverter.SingleToInt32Bits(value).ToString("X8");

    /// <summary>Evaluates a parameterless <see cref="double"/> expression by compiling it.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>Its runtime value.</returns>
    private static double Evaluate(Expression expression) => Expression.Lambda<Func<double>>(expression).Compile()();

    /// <summary>Asserts that <paramref name="expression"/> is a constant of <paramref name="type"/> and returns its value.</summary>
    /// <param name="type">The expected static type.</param>
    /// <param name="expression">The simplified expression.</param>
    /// <returns>The constant's value.</returns>
    private static object AssertConstant(Type type, Expression expression)
    {
        ConstantExpression constant = expression as ConstantExpression ?? throw new AssertFailedException($"Expected a constant, got {expression}.");
        Assert.AreEqual(type, constant.Type, $"Unexpected constant type for {expression}.");
        return constant.Value!;
    }

    /// <summary>
    /// Simplifies <paramref name="body"/>, asserting that it does not throw, and checks that the result computes the
    /// same values as the source on <see cref="Samples"/>, signed zeros and infinities included.
    /// </summary>
    /// <param name="x">The only parameter of <paramref name="body"/>.</param>
    /// <param name="body">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression AssertSimplifiesEquivalently(ParameterExpression x, Expression body)
    {
        Expression simplified = Simplify(body);
        Assert.AreEqual(body.Type, simplified.Type);
        Func<double, double> source = Expression.Lambda<Func<double, double>>(body, x).Compile();
        Func<double, double> result = Expression.Lambda<Func<double, double>>(simplified, x).Compile();
        foreach (double sample in Samples)
        {
            Assert.AreEqual(Key(source(sample)), Key(result(sample)), $"{body} => {simplified} differs at x = {sample}.");
        }

        return simplified;
    }

    /// <summary>Asserts that <paramref name="action"/> throws <typeparamref name="TException"/> from a rule, unwrapping the reflective invocation.</summary>
    /// <typeparam name="TException">The exception the rule throws.</typeparam>
    /// <param name="action">The simplification to run.</param>
    private static void AssertThrowsRuleException<TException>(Action action) where TException : Exception
    {
        Exception thrown = Assert.Throws<Exception>(action);
        while (thrown is TargetInvocationException { InnerException: not null } wrapper) thrown = wrapper.InnerException;
        Assert.IsInstanceOfType<TException>(thrown);
    }

    // ------------------------------------------------------------------------------------------
    // P12 - floating-point division by zero
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A <see cref="double"/> division of two constants folds to the exact runtime result. Before P12 every
    /// <c>±0</c> divisor threw <see cref="DivideByZeroException"/>, and <c>0 / -3</c> became <c>+0</c> because the
    /// <c>0 / x -&gt; 0</c> identity ran before constant folding.
    /// </summary>
    /// <param name="dividend">The dividend.</param>
    /// <param name="divisor">The divisor.</param>
    /// <param name="expected">The IEEE-754 result.</param>
    [TestMethod]
    [DataRow(1.0, 0.0, double.PositiveInfinity)]
    [DataRow(1.0, -0.0, double.NegativeInfinity)]
    [DataRow(-1.0, 0.0, double.NegativeInfinity)]
    [DataRow(-1.0, -0.0, double.PositiveInfinity)]
    [DataRow(0.0, 0.0, double.NaN)]
    [DataRow(-0.0, 0.0, double.NaN)]
    [DataRow(0.0, -0.0, double.NaN)]
    [DataRow(double.NaN, 0.0, double.NaN)]
    [DataRow(double.NaN, -0.0, double.NaN)]
    [DataRow(double.PositiveInfinity, double.PositiveInfinity, double.NaN)]
    [DataRow(double.PositiveInfinity, -0.0, double.NegativeInfinity)]
    [DataRow(double.PositiveInfinity, 0.0, double.PositiveInfinity)]
    [DataRow(0.0, double.PositiveInfinity, 0.0)]
    [DataRow(-0.0, double.PositiveInfinity, -0.0)]
    [DataRow(0.0, double.NegativeInfinity, -0.0)]
    [DataRow(0.0, -3.0, -0.0)]
    [DataRow(-0.0, 3.0, -0.0)]
    [DataRow(double.Epsilon, 0.0, double.PositiveInfinity)]
    [DataRow(double.Epsilon, 2.0, 0.0)]
    [DataRow(1.0, double.Epsilon, double.PositiveInfinity)]
    [DataRow(double.MaxValue, 0.5, double.PositiveInfinity)]
    [DataRow(double.MaxValue, 0.0, double.PositiveInfinity)]
    [DataRow(double.PositiveInfinity, 2.0, double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity, -3.0, double.PositiveInfinity)]
    public void Simplify_DoubleConstantDivision_FoldsToRuntimeResult(double dividend, double divisor, double expected)
    {
        Expression source = Expression.Divide(Expression.Constant(dividend), Expression.Constant(divisor));
        Assert.AreEqual(Key(expected), Key(Evaluate(source)), "Reference: the compiled expression.");
        Assert.AreEqual(Key(expected), Key((double)AssertConstant(typeof(double), Simplify(source))));
    }

    /// <summary>The <see cref="float"/> counterpart of <see cref="Simplify_DoubleConstantDivision_FoldsToRuntimeResult"/>: the folded constant stays a <see cref="float"/>.</summary>
    /// <param name="dividend">The dividend.</param>
    /// <param name="divisor">The divisor.</param>
    /// <param name="expected">The IEEE-754 result.</param>
    [TestMethod]
    [DataRow(1f, 0f, float.PositiveInfinity)]
    [DataRow(1f, -0f, float.NegativeInfinity)]
    [DataRow(-1f, 0f, float.NegativeInfinity)]
    [DataRow(-1f, -0f, float.PositiveInfinity)]
    [DataRow(0f, 0f, float.NaN)]
    [DataRow(-0f, 0f, float.NaN)]
    [DataRow(float.NaN, 0f, float.NaN)]
    [DataRow(float.PositiveInfinity, float.PositiveInfinity, float.NaN)]
    [DataRow(float.PositiveInfinity, -0f, float.NegativeInfinity)]
    [DataRow(0f, float.PositiveInfinity, 0f)]
    [DataRow(-0f, float.PositiveInfinity, -0f)]
    [DataRow(0f, -3f, -0f)]
    [DataRow(float.Epsilon, 0f, float.PositiveInfinity)]
    [DataRow(float.MaxValue, 0f, float.PositiveInfinity)]
    public void Simplify_FloatConstantDivision_FoldsToRuntimeResult(float dividend, float divisor, float expected)
    {
        Expression source = Expression.Divide(Expression.Constant(dividend), Expression.Constant(divisor));
        Assert.AreEqual(Key(expected), Key(Expression.Lambda<Func<float>>(source).Compile()()), "Reference: the compiled expression.");
        Assert.AreEqual(Key(expected), Key((float)AssertConstant(typeof(float), Simplify(source))));
    }

    /// <summary>
    /// A non-constant dividend over a floating zero keeps its division node and evaluates exactly like the source,
    /// <c>±inf</c> and <c>NaN</c> included (<c>0 / 0</c>, <c>NaN / 0</c>).
    /// </summary>
    [TestMethod]
    public void Simplify_SymbolicDividendOverFloatingZero_KeepsRuntimeSemantics()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        Expression[] sources =
        [
            Expression.Divide(x, Expression.Constant(0.0)),
            Expression.Divide(x, Expression.Constant(-0.0)),
            Expression.Divide(Expression.Add(x, Expression.Constant(1.0)), Expression.Constant(0.0)),
            Expression.Divide(Expression.Call(sin, x), Expression.Constant(0.0)),
            Expression.Divide(Expression.Negate(x), Expression.Constant(0.0)),
            Expression.Divide(Expression.Divide(x, Expression.Constant(0.0)), Expression.Constant(2.0)),
        ];
        foreach (Expression source in sources)
        {
            Expression simplified = AssertSimplifiesEquivalently(x, source);
            Assert.AreEqual(simplified.ToString(), Simplify(simplified).ToString(), $"Simplifying {source} again must be stable.");
        }

        Assert.AreEqual(ExpressionType.Divide, Simplify(Expression.Divide(x, Expression.Constant(0.0))).NodeType);
    }

    /// <summary>
    /// The <see cref="float"/> counterpart of <see cref="Simplify_SymbolicDividendOverFloatingZero_KeepsRuntimeSemantics"/>.
    /// </summary>
    [TestMethod]
    public void Simplify_SymbolicFloatDividendOverZero_KeepsRuntimeSemantics()
    {
        ParameterExpression x = Expression.Parameter(typeof(float), "x");
        Expression source = Expression.Divide(x, Expression.Constant(-0f));
        Expression simplified = Simplify(source);
        Assert.AreEqual(typeof(float), simplified.Type);
        Func<float, float> expected = Expression.Lambda<Func<float, float>>(source, x).Compile();
        Func<float, float> actual = Expression.Lambda<Func<float, float>>(simplified, x).Compile();
        foreach (double sample in Samples)
        {
            Assert.AreEqual(Key(expected((float)sample)), Key(actual((float)sample)), $"Differs at x = {sample}.");
        }
    }

    /// <summary>
    /// P11 integration: a <c>NaN</c> dividend over a floating zero now folds to <c>NaN</c> (it threw before P12 because
    /// the zero-divisor rule ran before the post-rule <c>NaN</c> fold), directly or once the dividend has folded.
    /// </summary>
    [TestMethod]
    public void Simplify_NaNOverFloatingZero_FoldsToNaN()
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Assert.IsTrue(double.IsNaN((double)AssertConstant(typeof(double), Simplify(Expression.Divide(Expression.Constant(double.NaN), Expression.Constant(0.0))))));
        Expression folded = Simplify(Expression.Divide(Expression.Add(y, Expression.Constant(double.NaN)), Expression.Constant(0.0)));
        Assert.IsTrue(double.IsNaN((double)AssertConstant(typeof(double), folded)));
        Assert.IsTrue(float.IsNaN((float)AssertConstant(typeof(float), Simplify(Expression.Divide(Expression.Constant(float.NaN), Expression.Constant(-0f))))));
    }

    /// <summary>
    /// The symbolic <c>0 / x -&gt; 0</c> identity is unchanged on a non-constant divisor (control: the S3 contract
    /// does not preserve the sign of the zero, nor <c>NaN</c> for <c>x = 0</c>).
    /// </summary>
    [TestMethod]
    public void Simplify_ZeroOverSymbolicDivisor_KeepsSymbolicIdentity()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Divide(Expression.Constant(0.0), x)))));
    }

    /// <summary>
    /// Integral and decimal division by a zero constant keep throwing while simplifying, as they do at runtime,
    /// including when the dividend is a constant.
    /// </summary>
    [TestMethod]
    public void Simplify_IntegralAndDecimalZeroDivisor_StillThrow()
    {
        AssertThrowsRuleException<DivideByZeroException>(() => Simplify(Expression.Divide(Expression.Constant(1), Expression.Constant(0))));
        AssertThrowsRuleException<DivideByZeroException>(() => Simplify(Expression.Divide(Expression.Constant(0L), Expression.Constant(0L))));
        AssertThrowsRuleException<DivideByZeroException>(() => Simplify(Expression.Divide(Expression.Constant(1u), Expression.Constant(0u))));
        AssertThrowsRuleException<DivideByZeroException>(() => Simplify(Expression.Divide(Expression.Constant(1m), Expression.Constant(0m))));
        Assert.ThrowsExactly<DivideByZeroException>(() => Expression.Lambda<Func<int>>(Expression.Divide(Expression.Constant(1), Expression.Constant(0))).Compile()());
    }

    /// <summary>
    /// A floating zero divisor runs no user code while simplifying, and the simplified tree still evaluates the
    /// dividend exactly once, or propagates its exception.
    /// </summary>
    [TestMethod]
    public void Simplify_ObservableDividendOverZero_KeepsCallsAndExceptions()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        _countingIdentityCalls = 0;
        Expression counting = Simplify(Expression.Divide(Expression.Call(Method(nameof(CountingIdentity)), x), Expression.Constant(0.0)));
        Assert.AreEqual(0, _countingIdentityCalls, "Simplifying must not call user code.");
        Assert.AreEqual(double.NegativeInfinity, Expression.Lambda<Func<double, double>>(counting, x).Compile()(-2.0));
        Assert.AreEqual(1, _countingIdentityCalls);

        Expression throwing = Simplify(Expression.Divide(Expression.Call(Method(nameof(Throwing)), x), Expression.Constant(0.0)));
        Func<double, double> compiled = Expression.Lambda<Func<double, double>>(throwing, x).Compile();
        Assert.ThrowsExactly<InvalidOperationException>(() => compiled(1.0));
    }

    /// <summary>Custom and lifted divisions by a zero constant are left untouched (controls).</summary>
    [TestMethod]
    public void Simplify_CustomOrLiftedDivisionByZero_IsUntouched()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression custom = Simplify(Expression.Divide(x, Expression.Constant(0.0), Method(nameof(CustomDivide))));
        Assert.AreEqual(Method(nameof(CustomDivide)), ((BinaryExpression)custom).Method);
        Assert.AreEqual(42.0, Expression.Lambda<Func<double, double>>(custom, x).Compile()(1.0));

        ParameterExpression nx = Expression.Parameter(typeof(double?), "nx");
        Expression lifted = Simplify(Expression.Divide(nx, Expression.Constant(0.0, typeof(double?))));
        Assert.AreEqual(typeof(double?), lifted.Type);
        Func<double?, double?> evaluate = Expression.Lambda<Func<double?, double?>>(lifted, nx).Compile();
        Assert.IsNull(evaluate(null));
        Assert.AreEqual(double.PositiveInfinity, evaluate(1.0));
    }

    /// <summary>Lambda bodies and derived simplifiers get the same division behavior.</summary>
    [TestMethod]
    public void Simplify_FloatingZeroDivisorInLambdaAndDerivedSimplifier_DoesNotThrow()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var lambda = (LambdaExpression)Simplify(Expression.Lambda<Func<double, double>>(Expression.Divide(x, Expression.Constant(0.0)), x));
        Assert.AreEqual(double.PositiveInfinity, ((Func<double, double>)lambda.Compile())(3.0));

        Expression derived = new MinimalDerivedSimplifier().Simplify(Expression.Divide(Expression.Constant(-1.0), Expression.Constant(0.0)));
        Assert.AreEqual(double.NegativeInfinity, (double)AssertConstant(typeof(double), derived));
    }

    /// <summary>A derived simplifier with no overrides, used to check that subclasses get the same rules.</summary>
    private sealed class MinimalDerivedSimplifier : ExpressionSimplifier
    {
    }
}
