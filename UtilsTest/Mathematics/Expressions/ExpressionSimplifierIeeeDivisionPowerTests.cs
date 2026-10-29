using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stages S5 P12 (IEEE-754 floating-point division by zero) and S5 P13 (constant
/// power edge cases), see <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>. P12: an ordinary
/// <see cref="double"/>/<see cref="float"/> division by <c>±0</c> no longer throws while simplifying, a constant
/// division folds to the exact runtime result (infinities, <c>NaN</c>, signed zeros), and integral and decimal
/// division by zero keep throwing. P13: a power of two constants folds to <see cref="double.Pow(double, double)"/>
/// instead of being rewritten by the zero/one and reciprocal identities first.
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

    // ------------------------------------------------------------------------------------------
    // P13 - power of constants
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A power of two constants folds to <see cref="double.Pow(double, double)"/>, which is also what the compiled
    /// <see cref="ExpressionType.Power"/> node computes. Before P13 <c>PowerOfZeroOrOne</c> returned the base for every
    /// <c>0 ^ c</c> (<c>0^0</c> was <c>0</c>, <c>0^-1</c> was <c>0</c>, <c>(-0)^2</c> was <c>-0</c>) and
    /// <c>PowerByZeroOrOne</c> rewrote a negative exponent into an unevaluated reciprocal.
    /// </summary>
    /// <param name="baseValue">The base.</param>
    /// <param name="exponent">The exponent.</param>
    /// <param name="expected">The IEEE-754 result.</param>
    [TestMethod]
    [DataRow(0.0, 0.0, 1.0)]
    [DataRow(0.0, 1.0, 0.0)]
    [DataRow(0.0, 2.0, 0.0)]
    [DataRow(0.0, -1.0, double.PositiveInfinity)]
    [DataRow(0.0, -2.0, double.PositiveInfinity)]
    [DataRow(0.0, 0.5, 0.0)]
    [DataRow(0.0, -0.5, double.PositiveInfinity)]
    [DataRow(-0.0, 0.0, 1.0)]
    [DataRow(-0.0, 1.0, -0.0)]
    [DataRow(-0.0, 2.0, 0.0)]
    [DataRow(-0.0, 3.0, -0.0)]
    [DataRow(-0.0, -1.0, double.NegativeInfinity)]
    [DataRow(-0.0, -2.0, double.PositiveInfinity)]
    [DataRow(-0.0, -3.0, double.NegativeInfinity)]
    [DataRow(0.0, -0.0, 1.0)]
    [DataRow(1.0, double.NaN, 1.0)]
    [DataRow(double.NaN, 0.0, 1.0)]
    [DataRow(double.NaN, 1.0, double.NaN)]
    [DataRow(double.NaN, -1.0, double.NaN)]
    [DataRow(0.0, double.NaN, double.NaN)]
    [DataRow(0.0, double.PositiveInfinity, 0.0)]
    [DataRow(0.0, double.NegativeInfinity, double.PositiveInfinity)]
    [DataRow(1.0, double.PositiveInfinity, 1.0)]
    [DataRow(1.0, double.NegativeInfinity, 1.0)]
    [DataRow(-1.0, double.PositiveInfinity, 1.0)]
    [DataRow(-1.0, double.NegativeInfinity, 1.0)]
    [DataRow(double.PositiveInfinity, 0.0, 1.0)]
    [DataRow(double.PositiveInfinity, -1.0, 0.0)]
    [DataRow(double.NegativeInfinity, 2.0, double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity, 3.0, double.NegativeInfinity)]
    [DataRow(double.NegativeInfinity, -2.0, 0.0)]
    [DataRow(double.NegativeInfinity, -3.0, -0.0)]
    [DataRow(2.0, -2.0, 0.25)]
    [DataRow(-2.0, -3.0, -0.125)]
    [DataRow(10.0, -400.0, 0.0)]
    [DataRow(1e-300, -2.0, double.PositiveInfinity)]
    [DataRow(-8.0, 0.5, double.NaN)]
    public void Simplify_ConstantPower_FoldsToPow(double baseValue, double exponent, double expected)
    {
        Expression source = Expression.Power(Expression.Constant(baseValue), Expression.Constant(exponent));
        Assert.AreEqual(Key(expected), Key(double.Pow(baseValue, exponent)), "Reference: double.Pow.");
        Assert.AreEqual(Key(expected), Key(Evaluate(source)), "Reference: the compiled Power node.");
        Assert.AreEqual(Key(expected), Key((double)AssertConstant(typeof(double), Simplify(source))));
    }

    /// <summary>
    /// A constant base with a negative exponent folds to <c>Pow(b, -n)</c> bit for bit, not to <c>1 / Pow(b, n)</c>,
    /// which rounds independently: for these bases the two differ in the last place.
    /// </summary>
    /// <param name="baseValue">The base.</param>
    /// <param name="exponent">The negative exponent.</param>
    [TestMethod]
    [DataRow(3.7, -3.0)]
    [DataRow(1.5, -0.5)]
    [DataRow(0.5, -2.5)]
    [DataRow(1e160, -2.0)]
    public void Simplify_ConstantBaseNegativeExponent_FoldsWithoutReciprocalRounding(double baseValue, double exponent)
    {
        double direct = double.Pow(baseValue, exponent);
        Assert.AreNotEqual(Key(direct), Key(1.0 / double.Pow(baseValue, -exponent)), "Precondition: the reciprocal form rounds differently.");
        Expression simplified = Simplify(Expression.Power(Expression.Constant(baseValue), Expression.Constant(exponent)));
        Assert.AreEqual(Key(direct), Key((double)AssertConstant(typeof(double), simplified)));
    }

    /// <summary>
    /// The symbolic power identities are unchanged on a non-constant operand (controls): <c>x^0 -&gt; 1</c>,
    /// <c>x^1 -&gt; x</c>, <c>x^-1 -&gt; 1/x</c>, <c>x^-2 -&gt; 1/x^2</c>, <c>0^x -&gt; 0</c> and <c>1^x -&gt; 1</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_SymbolicPowerIdentities_AreUnchanged()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreEqual(Key(1.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Power(x, Expression.Constant(0.0))))));
        Assert.AreSame(x, Simplify(Expression.Power(x, Expression.Constant(1.0))));
        Assert.AreEqual("(1 / x)", Simplify(Expression.Power(x, Expression.Constant(-1.0))).ToString());
        Assert.AreEqual("(1 / (x ** 2))", Simplify(Expression.Power(x, Expression.Constant(-2.0))).ToString());
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Power(Expression.Constant(0.0), x)))));
        Assert.AreEqual(Key(1.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Power(Expression.Constant(1.0), x)))));
    }

    /// <summary>A custom power of two constants is neither folded nor rewritten (control).</summary>
    [TestMethod]
    public void Simplify_CustomPowerOfConstants_IsUntouched()
    {
        Expression custom = Simplify(Expression.Power(Expression.Constant(0.0), Expression.Constant(0.0), Method(nameof(CustomPower))));
        Assert.AreEqual(Method(nameof(CustomPower)), ((BinaryExpression)custom).Method);
        Assert.AreEqual(42.0, Evaluate(custom));
    }

    /// <summary>
    /// Powers and divisions of constants combine through their exact folds: <c>1 / 0^2</c> is <c>+inf</c>,
    /// <c>(1 / 0)^-1</c> is <c>0</c>, <c>(-0)^-1 / 2</c> is <c>-inf</c>, and a symbolic factor over a folded infinity
    /// keeps evaluating like the source.
    /// </summary>
    [TestMethod]
    public void Simplify_CombinedPowerAndDivisionOfConstants_FoldExactly()
    {
        Expression c(double value) => Expression.Constant(value);
        Assert.AreEqual(double.PositiveInfinity, (double)AssertConstant(typeof(double), Simplify(Expression.Divide(c(1.0), Expression.Power(c(0.0), c(2.0))))));
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Power(Expression.Divide(c(1.0), c(0.0)), c(-1.0))))));
        Assert.AreEqual(double.NegativeInfinity, (double)AssertConstant(typeof(double), Simplify(Expression.Divide(Expression.Power(c(-0.0), c(-1.0)), c(2.0)))));

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        AssertSimplifiesEquivalently(x, Expression.Multiply(x, Expression.Power(c(0.0), c(-1.0))));
        AssertSimplifiesEquivalently(x, Expression.Add(x, Expression.Power(c(0.0), c(0.0))));
    }

    // ------------------------------------------------------------------------------------------
    // P12 follow-up - typed constant folding
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Sample operands of every type with ordinary arithmetic, limits included, so that wrap-around, truncation and
    /// the IEEE-754 special values are exercised.
    /// </summary>
    private static readonly (Type Type, object[] Values)[] TypedSamples =
    [
        (typeof(short), [(short)0, (short)1, (short)-1, (short)3, (short)-7, (short)2, short.MaxValue, short.MinValue]),
        (typeof(ushort), [(ushort)0, (ushort)1, (ushort)3, (ushort)7, (ushort)2, ushort.MaxValue]),
        (typeof(int), [0, 1, -1, 3, -7, 2, int.MaxValue, int.MinValue]),
        (typeof(uint), [0u, 1u, 3u, 7u, 2u, uint.MaxValue]),
        (typeof(long), [0L, 1L, -1L, 3L, -7L, 2L, long.MaxValue, long.MinValue]),
        (typeof(ulong), [0ul, 1ul, 3ul, 7ul, 2ul, ulong.MaxValue]),
        (typeof(decimal), [0m, 1m, -1m, 3m, -7m, 2.5m, 1.00m, decimal.MaxValue, decimal.MinValue]),
        (typeof(float), [0f, -0f, 1f, -1f, 3f, -7f, 0.1f, float.MaxValue, float.Epsilon, float.NaN, float.PositiveInfinity, float.NegativeInfinity]),
        (typeof(double), [0.0, -0.0, 1.0, -1.0, 3.0, -7.0, 0.1, double.MaxValue, double.Epsilon, double.NaN, double.PositiveInfinity, double.NegativeInfinity]),
    ];

    /// <summary>Evaluates a parameterless expression of any type by compiling it, reporting an exception by its type.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>A comparison key: see <see cref="ValueKey"/>, or the exception's type.</returns>
    private static string EvaluateKey(Expression expression)
    {
        try
        {
            return ValueKey(Expression.Lambda(expression).Compile().DynamicInvoke()!);
        }
        catch (TargetInvocationException exception)
        {
            return "throws " + exception.InnerException!.GetType().Name;
        }
    }

    /// <summary>Formats a boxed numeric value with its type and exact representation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The type name and the bits (every <c>NaN</c> equal; a decimal's sign and scale included).</returns>
    /// <remarks>
    /// A folded constant is compared through its <see cref="ConstantExpression.Value"/>, not by compiling it: the
    /// expression compiler re-emits an integral <see cref="decimal"/> constant from its integer value, which drops the
    /// sign of a decimal <c>-0</c> (the runtime result of <c>0m / -7m</c>).
    /// </remarks>
    private static string ValueKey(object value)
    {
        string bits = value switch
        {
            double d => Key(d),
            float f => Key(f),
            decimal m => string.Join(",", decimal.GetBits(m)),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!,
        };
        return value.GetType().Name + ":" + bits;
    }

    /// <summary>Simplifies <paramref name="expression"/>, reporting a rule exception by its type.</summary>
    /// <param name="expression">The source expression.</param>
    /// <param name="simplified">Receives the simplified expression, or <see langword="null"/> when simplifying threw.</param>
    /// <returns>The exception's comparison key, or <see langword="null"/> when simplifying succeeded.</returns>
    private static string? TrySimplify(Expression expression, out Expression? simplified)
    {
        try
        {
            simplified = Simplify(expression);
            return null;
        }
        catch (Exception exception)
        {
            while (exception is TargetInvocationException { InnerException: not null } wrapper) exception = wrapper.InnerException;
            simplified = null;
            return "throws " + exception.GetType().Name;
        }
    }

    /// <summary>
    /// A <see cref="short"/> or <see cref="ushort"/> division of two constants folds to a constant of the operand type
    /// (Codex review of PR #665). The <c>dynamic</c> folding applied the C# promotions and returned an
    /// <see cref="int"/>; P12 made <c>0 / 3</c> reach it (the <c>0 / x</c> identity returned the typed dividend
    /// before), and <c>7 / 2</c> already did.
    /// </summary>
    [TestMethod]
    public void Simplify_SmallIntegralConstantDivision_KeepsOperandType()
    {
        Assert.AreEqual((short)0, AssertConstant(typeof(short), Simplify(Expression.Divide(Expression.Constant((short)0), Expression.Constant((short)3)))));
        Assert.AreEqual((short)3, AssertConstant(typeof(short), Simplify(Expression.Divide(Expression.Constant((short)7), Expression.Constant((short)2)))));
        Assert.AreEqual((short)short.MinValue, AssertConstant(typeof(short), Simplify(Expression.Divide(Expression.Constant(short.MinValue), Expression.Constant((short)1)))));
        Assert.AreEqual((ushort)0, AssertConstant(typeof(ushort), Simplify(Expression.Divide(Expression.Constant((ushort)0), Expression.Constant((ushort)3)))));
        Assert.AreEqual((ushort)3, AssertConstant(typeof(ushort), Simplify(Expression.Divide(Expression.Constant((ushort)7), Expression.Constant((ushort)2)))));

        // The reported failure: the folded constant used as the body of a lambda returning short, or as the operand of
        // another short node, threw a type-mismatch exception.
        var lambda = (Expression<Func<short>>)Simplify(Expression.Lambda<Func<short>>(Expression.Divide(Expression.Constant((short)0), Expression.Constant((short)3))));
        Assert.AreEqual((short)0, lambda.Compile()());
        ParameterExpression s = Expression.Parameter(typeof(short), "s");
        Expression sum = Simplify(Expression.Add(s, Expression.Divide(Expression.Constant((short)7), Expression.Constant((short)2))));
        Assert.AreEqual((short)8, Expression.Lambda<Func<short, short>>(sum, s).Compile()(5));
    }

    /// <summary>
    /// Every ordinary division of two constants folds to a constant of the node's type whose value (or exception) is
    /// the compiled node's: truncation, <c>DivideByZeroException</c>, <c>OverflowException</c> (<c>MinValue / -1</c>,
    /// decimal overflow), IEEE-754 <c>NaN</c>, infinities and signed zeros, and divisors <c>±1</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_OrdinaryConstantDivision_FoldsInNodeType()
        => AssertConstantArithmeticFoldsInNodeType([Expression.Divide]);

    /// <summary>
    /// A constant divided by a constant <c>±1</c> folds to a constant of the node's type, whatever the type: before,
    /// the <c>±1</c> identity ran first and returned the unevaluated <c>-(5)</c> for <c>5 / -1</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_ConstantOverPlusOrMinusOne_FoldsToConstant()
    {
        Assert.AreEqual(5, AssertConstant(typeof(int), Simplify(Expression.Divide(Expression.Constant(5), Expression.Constant(1)))));
        Assert.AreEqual(-5, AssertConstant(typeof(int), Simplify(Expression.Divide(Expression.Constant(5), Expression.Constant(-1)))));
        Assert.AreEqual(0, AssertConstant(typeof(int), Simplify(Expression.Divide(Expression.Constant(0), Expression.Constant(3)))));
        Assert.AreEqual(0, AssertConstant(typeof(int), Simplify(Expression.Divide(Expression.Constant(0), Expression.Constant(-3)))));
        Assert.AreEqual(-5L, AssertConstant(typeof(long), Simplify(Expression.Divide(Expression.Constant(5L), Expression.Constant(-1L)))));
        Assert.AreEqual(5u, AssertConstant(typeof(uint), Simplify(Expression.Divide(Expression.Constant(5u), Expression.Constant(1u)))));
        Assert.AreEqual(5ul, AssertConstant(typeof(ulong), Simplify(Expression.Divide(Expression.Constant(5ul), Expression.Constant(1ul)))));
        Assert.AreEqual(-5m, AssertConstant(typeof(decimal), Simplify(Expression.Divide(Expression.Constant(5m), Expression.Constant(-1m)))));
        Assert.AreEqual((short)-7, AssertConstant(typeof(short), Simplify(Expression.Divide(Expression.Constant((short)7), Expression.Constant((short)-1)))));
        Assert.AreEqual(short.MinValue, AssertConstant(typeof(short), Simplify(Expression.Divide(Expression.Constant(short.MinValue), Expression.Constant((short)-1)))));
        Assert.AreEqual(-5f, AssertConstant(typeof(float), Simplify(Expression.Divide(Expression.Constant(5f), Expression.Constant(-1f)))));
        Assert.AreEqual(Key(-5.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Divide(Expression.Constant(5.0), Expression.Constant(-1.0))))));
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Divide(Expression.Constant(-0.0), Expression.Constant(-1.0))))));
        Assert.AreEqual(Key(-0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Divide(Expression.Constant(0.0), Expression.Constant(-1.0))))));
        Assert.IsTrue(double.IsNaN((double)AssertConstant(typeof(double), Simplify(Expression.Divide(Expression.Constant(double.NaN), Expression.Constant(-1.0))))));
    }

    /// <summary>
    /// <c>int.MinValue / -1</c> and <c>long.MinValue / -1</c> throw <see cref="OverflowException"/> while simplifying,
    /// as the compiled node does; the <c>-1</c> identity turned them into a negation that silently wraps to
    /// <c>MinValue</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_MinValueOverMinusOne_ThrowsOverflow()
    {
        Expression intSource = Expression.Divide(Expression.Constant(int.MinValue), Expression.Constant(-1));
        Assert.ThrowsExactly<OverflowException>(() => Expression.Lambda<Func<int>>(intSource).Compile()(), "Precondition: the compiled node throws.");
        AssertThrowsRuleException<OverflowException>(() => Simplify(intSource));
        Expression longSource = Expression.Divide(Expression.Constant(long.MinValue), Expression.Constant(-1L));
        Assert.ThrowsExactly<OverflowException>(() => Expression.Lambda<Func<long>>(longSource).Compile()(), "Precondition: the compiled node throws.");
        AssertThrowsRuleException<OverflowException>(() => Simplify(longSource));
    }

    /// <summary>
    /// On a non-constant dividend the <c>±1</c> identities are unchanged (controls): <c>x / 1 -&gt; x</c> and
    /// <c>x / -1 -&gt; -x</c>, exact for <see cref="double"/> (signed zeros, infinities and <c>NaN</c> included). For a
    /// signed integral dividend, <c>i / -1</c> used to become <c>-i</c>; since ETC R1b (C-06) it is kept, because
    /// <c>int.MinValue / -1</c> throws <see cref="OverflowException"/> where the negation wraps.
    /// </summary>
    [TestMethod]
    public void Simplify_SymbolicOverPlusOrMinusOne_KeepsIdentities()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreSame(x, Simplify(Expression.Divide(x, Expression.Constant(1.0))));
        Expression negated = AssertSimplifiesEquivalently(x, Expression.Divide(x, Expression.Constant(-1.0)));
        Assert.AreEqual(ExpressionType.Negate, negated.NodeType);
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression kept = Simplify(Expression.Divide(i, Expression.Constant(-1)));
        Assert.AreEqual(ExpressionType.Divide, kept.NodeType);
        Assert.ThrowsExactly<OverflowException>(() => Expression.Lambda<Func<int, int>>(kept, i).Compile()(int.MinValue));
        Assert.AreSame(i, Simplify(Expression.Divide(i, Expression.Constant(1))));
    }

    /// <summary>
    /// Every ordinary addition, subtraction and multiplication of two constants folds like the compiled node, the
    /// zero/one operands included: the additive and multiplicative identities no longer return one constant operand
    /// unevaluated.
    /// </summary>
    [TestMethod]
    public void Simplify_OrdinaryConstantAdditiveAndMultiplicativeArithmetic_FoldsInNodeType()
        => AssertConstantArithmeticFoldsInNodeType([Expression.Add, Expression.Subtract, Expression.Multiply]);

    /// <summary>
    /// The zero/one identities ran before constant folding and returned one operand: <c>-0.0 + 0.0</c> was <c>-0</c>,
    /// <c>0.0 - 0.0</c> was <c>-(0)</c>, <c>-0.0 * 0.0</c> was <c>+0</c>, <c>0m * 2.5m</c> lost the scale of
    /// <c>0.0m</c>, and <c>5 * -1</c> was <c>-(5)</c>. Each now folds to the runtime result.
    /// </summary>
    [TestMethod]
    public void Simplify_ConstantPairWithZeroOrOne_FoldsExactly()
    {
        Expression c(double value) => Expression.Constant(value);
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Add(c(-0.0), c(0.0))))));
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Add(c(0.0), c(-0.0))))));
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Subtract(c(0.0), c(0.0))))));
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Subtract(c(-0.0), c(-0.0))))));
        Assert.AreEqual(Key(-0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Multiply(c(-0.0), c(0.0))))));
        Assert.AreEqual(Key(-0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Multiply(c(0.0), c(-3.0))))));
        Assert.AreEqual(Key(-0f), Key((float)AssertConstant(typeof(float), Simplify(Expression.Multiply(Expression.Constant(-0f), Expression.Constant(1f))))));
        decimal product = (decimal)AssertConstant(typeof(decimal), Simplify(Expression.Multiply(Expression.Constant(0m), Expression.Constant(2.5m))));
        Assert.AreEqual(string.Join(",", decimal.GetBits(0m * 2.5m)), string.Join(",", decimal.GetBits(product)), "The decimal scale is kept.");
        Assert.AreEqual(-5, AssertConstant(typeof(int), Simplify(Expression.Multiply(Expression.Constant(5), Expression.Constant(-1)))));
        Assert.AreEqual(-5, AssertConstant(typeof(int), Simplify(Expression.Multiply(Expression.Constant(-1), Expression.Constant(5)))));
        Assert.IsTrue(double.IsNaN((double)AssertConstant(typeof(double), Simplify(Expression.Multiply(c(double.PositiveInfinity), c(0.0))))), "S5 P11 behavior is kept.");
    }

    /// <summary>
    /// The zero/one identities are unchanged on a non-constant operand (controls): <c>x + 0</c>, <c>0 + x</c>,
    /// <c>x - 0</c>, <c>0 - x</c>, <c>x * 0</c>, <c>x * 1</c> and <c>x * -1</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_SymbolicZeroOrOneIdentities_AreUnchanged()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression zero = Expression.Constant(0.0);
        Assert.AreSame(x, Simplify(Expression.Add(x, zero)));
        Assert.AreSame(x, Simplify(Expression.Add(zero, x)));
        Assert.AreSame(x, Simplify(Expression.Subtract(x, zero)));
        Assert.AreEqual(ExpressionType.Negate, Simplify(Expression.Subtract(zero, x)).NodeType);
        Assert.AreEqual(Key(0.0), Key((double)AssertConstant(typeof(double), Simplify(Expression.Multiply(x, zero)))));
        Assert.AreSame(x, Simplify(Expression.Multiply(x, Expression.Constant(1.0))));
        Assert.AreEqual(ExpressionType.Negate, Simplify(Expression.Multiply(Expression.Constant(-1.0), x)).NodeType);
    }

    /// <summary>
    /// <see cref="short"/> and <see cref="ushort"/> additions, subtractions and multiplications of two constants fold in
    /// the operand type with wrap-around: <c>32767 + 1</c> is <c>-32768</c> and <c>(ushort)3 - 4</c> is <c>65535</c>
    /// (the <c>dynamic</c> folding returned the <see cref="int"/> constants <c>32768</c> and <c>-1</c>).
    /// </summary>
    [TestMethod]
    public void Simplify_SmallIntegralConstantArithmetic_WrapsInOperandType()
    {
        Assert.AreEqual(short.MinValue, AssertConstant(typeof(short), Simplify(Expression.Add(Expression.Constant(short.MaxValue), Expression.Constant((short)1)))));
        Assert.AreEqual(short.MaxValue, AssertConstant(typeof(short), Simplify(Expression.Subtract(Expression.Constant(short.MinValue), Expression.Constant((short)1)))));
        Assert.AreEqual(unchecked((short)(300 * 300)), AssertConstant(typeof(short), Simplify(Expression.Multiply(Expression.Constant((short)300), Expression.Constant((short)300)))));
        Assert.AreEqual(ushort.MaxValue, AssertConstant(typeof(ushort), Simplify(Expression.Subtract(Expression.Constant((ushort)3), Expression.Constant((ushort)4)))));
        Assert.AreEqual((ushort)7, AssertConstant(typeof(ushort), Simplify(Expression.Add(Expression.Constant((ushort)3), Expression.Constant((ushort)4)))));
        Assert.AreEqual(unchecked((ushort)(300 * 300)), AssertConstant(typeof(ushort), Simplify(Expression.Multiply(Expression.Constant((ushort)300), Expression.Constant((ushort)300)))));
    }

    /// <summary>
    /// Checks every pair of <see cref="TypedSamples"/> under <paramref name="operators"/>: simplifying either throws the
    /// exception the compiled node throws, or returns a constant of the node's type with the compiled node's value.
    /// </summary>
    /// <param name="operators">The node factories to check.</param>
    private static void AssertConstantArithmeticFoldsInNodeType(Func<Expression, Expression, BinaryExpression>[] operators)
    {
        int checkedCases = 0;
        int expectedCases = 0;
        foreach ((Type type, object[] values) in TypedSamples)
        {
            foreach (Func<Expression, Expression, BinaryExpression> make in operators)
            {
                foreach (object left in values)
                {
                    foreach (object right in values)
                    {
                        BinaryExpression source = make(Expression.Constant(left, type), Expression.Constant(right, type));
                        string expected = EvaluateKey(source);
                        string? thrown = TrySimplify(source, out Expression? simplified);
                        if (thrown is not null)
                        {
                            Assert.AreEqual(expected, thrown, $"{type.Name} {source} threw while simplifying.");
                        }
                        else
                        {
                            Assert.AreEqual(type, simplified!.Type, $"{type.Name} {source} => {simplified}");
                            ConstantExpression folded = simplified as ConstantExpression
                                ?? throw new AssertFailedException($"{type.Name} {source} => {simplified} is not a constant.");
                            Assert.AreEqual(expected, ValueKey(folded.Value!), $"{type.Name} {source} => {simplified}");
                        }
                        checkedCases++;
                    }
                }
            }
        }

        foreach ((Type _, object[] values) in TypedSamples) expectedCases += operators.Length * values.Length * values.Length;
        Assert.AreEqual(expectedCases, checkedCases);
    }

    /// <summary>
    /// The constant-combining multiplication rules fold in the operand type: <c>3 * (4 * s)</c> and
    /// <c>(3 * s) * (4 * t)</c> on <see cref="short"/> threw <see cref="InvalidOperationException"/> because the
    /// <c>dynamic</c> product was an <see cref="int"/>.
    /// </summary>
    [TestMethod]
    public void Simplify_SmallIntegralConstantFactors_CombineInOperandType()
    {
        ParameterExpression s = Expression.Parameter(typeof(short), "s");
        ParameterExpression t = Expression.Parameter(typeof(short), "t");
        Expression nested = Simplify(Expression.Multiply(Expression.Constant((short)3), Expression.Multiply(Expression.Constant((short)4), s)));
        Assert.AreEqual(typeof(short), nested.Type);
        Assert.AreEqual((short)(12 * 5), Expression.Lambda<Func<short, short>>(nested, s).Compile()(5));

        Expression pair = Simplify(Expression.Multiply(Expression.Multiply(Expression.Constant((short)3), s), Expression.Multiply(Expression.Constant((short)4), t)));
        Assert.AreEqual(typeof(short), pair.Type);
        Assert.AreEqual(unchecked((short)(12 * 5 * 7)), Expression.Lambda<Func<short, short, short>>(pair, s, t).Compile()(5, 7));

        ParameterExpression u = Expression.Parameter(typeof(ushort), "u");
        Expression wrapped = Simplify(Expression.Multiply(Expression.Constant((ushort)300), Expression.Multiply(Expression.Constant((ushort)300), u)));
        Assert.AreEqual(typeof(ushort), wrapped.Type);
        Assert.AreEqual(unchecked((ushort)(300 * 300)), Expression.Lambda<Func<ushort, ushort>>(wrapped, u).Compile()(1));
    }

    /// <summary>A custom division of two short constants is neither folded nor retyped (control).</summary>
    [TestMethod]
    public void Simplify_CustomSmallIntegralDivision_IsUntouched()
    {
        MethodInfo custom = typeof(ExpressionSimplifierIeeeDivisionPowerTests).GetMethod(nameof(CustomShortDivide))!;
        Expression simplified = Simplify(Expression.Divide(Expression.Constant((short)0), Expression.Constant((short)3), custom));
        Assert.AreEqual(custom, ((BinaryExpression)simplified).Method);
        Assert.AreEqual((short)42, Expression.Lambda<Func<short>>(simplified).Compile()());
    }

    // ------------------------------------------------------------------------------------------
    // P12 follow-up - nested divisions with a zero constant divisor
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Simplifies a two-parameter body, asserting that the result computes the same values as the source on every
    /// pair of <see cref="Samples"/> (signed zeros, infinities and <c>NaN</c> included) and that simplifying it again
    /// changes nothing.
    /// </summary>
    /// <param name="x">The first parameter.</param>
    /// <param name="y">The second parameter.</param>
    /// <param name="body">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression AssertSimplifiesEquivalently(ParameterExpression x, ParameterExpression y, Expression body)
    {
        Expression simplified = Simplify(body);
        Assert.AreEqual(body.Type, simplified.Type);
        Func<double, double, double> source = Expression.Lambda<Func<double, double, double>>(body, x, y).Compile();
        Func<double, double, double> result = Expression.Lambda<Func<double, double, double>>(simplified, x, y).Compile();
        foreach (double first in Samples)
        {
            foreach (double second in Samples)
            {
                Assert.AreEqual(Key(source(first, second)), Key(result(first, second)), $"{body} => {simplified} differs at x = {first}, y = {second}.");
            }
        }

        Assert.AreEqual(simplified.ToString(), Simplify(simplified).ToString(), $"Simplifying {body} again must be stable.");
        return simplified;
    }

    /// <summary>
    /// A nested division whose divisor is a zero constant is outside the domain of the field identities of
    /// <c>DivisionOfDivision</c> and keeps its runtime value. Before, <c>x / (x / 0.0)</c> became <c>0 / x</c>
    /// (<c>x / (y / z) -&gt; (x*z) / y</c>, then <c>x * 0 -&gt; 0</c>), <c>(x / 0.0) / x</c> became <c>x / 0</c>
    /// (<c>-inf</c> instead of <c>+inf</c> for a negative <c>x</c>) and <c>(x / y) / 0.0</c> became <c>x / 0</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_NestedDivisionByZeroConstant_KeepsRuntimeSemantics()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression zero = Expression.Constant(0.0);
        Expression negativeZero = Expression.Constant(-0.0);
        Expression two = Expression.Constant(2.0);

        Assert.AreEqual("(x / (x / 0))", AssertSimplifiesEquivalently(x, y, Expression.Divide(x, Expression.Divide(x, zero))).ToString());
        AssertSimplifiesEquivalently(x, y, Expression.Divide(x, Expression.Divide(y, zero)));
        AssertSimplifiesEquivalently(x, y, Expression.Divide(x, Expression.Divide(y, negativeZero)));
        Assert.AreEqual("((x / 0) / x)", AssertSimplifiesEquivalently(x, y, Expression.Divide(Expression.Divide(x, zero), x)).ToString());
        AssertSimplifiesEquivalently(x, y, Expression.Divide(Expression.Divide(x, zero), y));
        AssertSimplifiesEquivalently(x, y, Expression.Divide(Expression.Divide(x, y), zero));
        AssertSimplifiesEquivalently(x, y, Expression.Divide(Expression.Divide(x, zero), Expression.Divide(y, two)));
        AssertSimplifiesEquivalently(x, y, Expression.Divide(Expression.Divide(x, two), Expression.Divide(y, zero)));
        AssertSimplifiesEquivalently(x, y, Expression.Divide(Expression.Divide(x, zero), Expression.Divide(x, zero)));
    }

    /// <summary>The <see cref="float"/> counterpart of <see cref="Simplify_NestedDivisionByZeroConstant_KeepsRuntimeSemantics"/>.</summary>
    [TestMethod]
    public void Simplify_NestedFloatDivisionByZeroConstant_KeepsRuntimeSemantics()
    {
        ParameterExpression x = Expression.Parameter(typeof(float), "x");
        Expression source = Expression.Divide(x, Expression.Divide(x, Expression.Constant(0f)));
        Expression simplified = Simplify(source);
        Func<float, float> expected = Expression.Lambda<Func<float, float>>(source, x).Compile();
        Func<float, float> actual = Expression.Lambda<Func<float, float>>(simplified, x).Compile();
        foreach (double sample in Samples)
        {
            Assert.AreEqual(Key(expected((float)sample)), Key(actual((float)sample)), $"{source} => {simplified} differs at x = {sample}.");
        }
    }

    /// <summary>
    /// Nested divisions with non-zero divisors keep the field rewrites (controls, "Symbolic-equivalence contract":
    /// rounding and intermediate overflow are not preserved): <c>x / (y / z) -&gt; (x*z) / y</c>,
    /// <c>(x / y) / z -&gt; x / (y*z)</c> and <c>(x / y) / (z / w) -&gt; (x*w) / (y*z)</c>, with constant divisors too.
    /// </summary>
    [TestMethod]
    public void Simplify_NestedDivisionByNonZero_KeepsFieldRewrites()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        ParameterExpression w = Expression.Parameter(typeof(double), "w");
        Assert.AreEqual("((x * z) / y)", Simplify(Expression.Divide(x, Expression.Divide(y, z))).ToString());
        Assert.AreEqual("(x / (y * z))", Simplify(Expression.Divide(Expression.Divide(x, y), z)).ToString());
        Assert.AreEqual("((x * w) / (y * z))", Simplify(Expression.Divide(Expression.Divide(x, y), Expression.Divide(z, w))).ToString());
        Assert.AreEqual("((2 * x) / y)", Simplify(Expression.Divide(x, Expression.Divide(y, Expression.Constant(2.0)))).ToString());
        Assert.AreEqual("(x / (2 * y))", Simplify(Expression.Divide(Expression.Divide(x, Expression.Constant(2.0)), y)).ToString());
    }

    /// <summary>
    /// A declined nested division by zero runs no user code while simplifying, keeps the observable dividend's single
    /// evaluation and its exception, and leaves a custom inner division untouched.
    /// </summary>
    [TestMethod]
    public void Simplify_ObservableNestedDivisionByZero_KeepsCallsExceptionsAndCustomOperators()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression zero = Expression.Constant(0.0);
        _countingIdentityCalls = 0;
        Expression counting = Simplify(Expression.Divide(Expression.Call(Method(nameof(CountingIdentity)), x), Expression.Divide(x, zero)));
        Assert.AreEqual(0, _countingIdentityCalls, "Simplifying must not call user code.");
        Assert.AreEqual(Key(0.0), Key(Expression.Lambda<Func<double, double>>(counting, x).Compile()(-2.0)), "-2 / (-2 / 0) is -2 / -inf, +0.");
        Assert.AreEqual(1, _countingIdentityCalls);

        Expression throwing = Simplify(Expression.Divide(Expression.Divide(Expression.Call(Method(nameof(Throwing)), x), zero), x));
        Func<double, double> compiled = Expression.Lambda<Func<double, double>>(throwing, x).Compile();
        Assert.ThrowsExactly<InvalidOperationException>(() => compiled(1.0));

        BinaryExpression custom = (BinaryExpression)Simplify(Expression.Divide(x, Expression.Divide(x, zero, Method(nameof(CustomDivide)))));
        Assert.AreEqual(Method(nameof(CustomDivide)), ((BinaryExpression)custom.Right).Method);
        Assert.AreEqual(1.0 / 42.0, Expression.Lambda<Func<double, double>>(custom, x).Compile()(1.0));
    }

    /// <summary>A user-defined <see cref="short"/> division, so a node built with it is never ordinary arithmetic.</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>Always <c>42</c>.</returns>
    public static short CustomShortDivide(short a, short b) => 42;
}
