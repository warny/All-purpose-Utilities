using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for the S5 P14 follow-up "local aggregation of additive constants", see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>. The additive canonicalization kept constants that the
/// source adds at different places as separate terms (<c>(a + 5) + 6 -&gt; 5 + (6 + a)</c>, at every call), and the
/// factoring rules combined a non-constant coefficient with constants unevaluated
/// (<c>(5 + a)*x + 5*x + 6*x -&gt; (((5 + a) + 5) + 6) * x</c>, the benchmark J family). Both now sum the constants of
/// the collected terms with the typed constant folding of S5 P12.
/// </summary>
/// <remarks>
/// Every assertion is made after ONE public <see cref="ExpressionSimplifier.Simplify(Expression)"/> call. Summing
/// constants that the source adds at different places is a reassociation: exact for the integral types, and the
/// rounding latitude of the "Symbolic-equivalence contract" for floating-point types.
/// </remarks>
[TestClass]
public class ExpressionSimplifierAdditiveConstantAggregationTests
{
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

    /// <summary>A user-defined addition, so a node built with it is never ordinary arithmetic.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>Always <c>42</c>.</returns>
    public static double CustomAdd(double a, double b) => 42.0;

    /// <summary>Gets a public static method of this class by name.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Method(string name) => typeof(ExpressionSimplifierAdditiveConstantAggregationTests).GetMethod(name)!;

    /// <summary>Simplifies <paramref name="expression"/> once with a fresh built-in simplifier.</summary>
    /// <param name="expression">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Simplify(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Builds a <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static Expression C(double value) => Expression.Constant(value);

    /// <summary>Counts the constant leaves of an arithmetic tree.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The number of constants.</returns>
    private static int CountConstants(Expression expression) => expression switch
    {
        BinaryExpression binary => CountConstants(binary.Left) + CountConstants(binary.Right),
        UnaryExpression unary => CountConstants(unary.Operand),
        ConstantExpression => 1,
        _ => 0,
    };

    /// <summary>Formats a result so that every <c>NaN</c> compares equal and signed zeros differ.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A comparison key.</returns>
    private static string Key(double value) => double.IsNaN(value) ? "NaN" : BitConverter.DoubleToInt64Bits(value).ToString("X16");

    /// <summary>Asserts that <paramref name="expression"/> is <c>constant + term</c> and returns the constant's value.</summary>
    /// <param name="expression">The simplified expression.</param>
    /// <param name="term">The expected non-constant term.</param>
    /// <returns>The constant value.</returns>
    private static object AssertConstantPlus(Expression expression, Expression term)
    {
        var sum = expression as BinaryExpression ?? throw new AssertFailedException($"Expected a sum, got {expression}.");
        Assert.AreEqual(ExpressionType.Add, sum.NodeType, expression.ToString());
        Assert.AreSame(term, sum.Right, expression.ToString());
        var constant = sum.Left as ConstantExpression ?? throw new AssertFailedException($"Expected a constant first, got {expression}.");
        Assert.AreEqual(expression.Type, constant.Type);
        return constant.Value!;
    }

    // ------------------------------------------------------------------------------------------
    // Nested additions with constants
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Constants split by a non-constant term are summed in one call, in every association and operand order; before,
    /// each case kept two or three separate constants (<c>5 + (6 + a)</c>, <c>(0 - 3) + (5 + a)</c>, ...) at every call.
    /// </summary>
    [TestMethod]
    public void Simplify_NestedAdditionsWithConstants_SumTheConstants()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        (Expression Source, double Constant)[] cases =
        [
            (Expression.Add(Expression.Add(a, C(5)), C(6)), 11),
            (Expression.Add(Expression.Add(C(5), a), C(6)), 11),
            (Expression.Add(C(6), Expression.Add(a, C(5))), 11),
            (Expression.Add(C(6), Expression.Add(C(5), a)), 11),
            (Expression.Add(Expression.Add(Expression.Add(a, C(5)), C(6)), C(7)), 18),
            (Expression.Subtract(Expression.Add(a, C(5)), C(3)), 2),
            (Expression.Add(Expression.Subtract(a, C(5)), C(3)), -2),
            (Expression.Add(Expression.Add(Expression.Add(C(5), a), C(5)), C(6)), 16),
            (Expression.Subtract(Expression.Subtract(a, C(5)), C(3)), -8),
        ];
        foreach ((Expression source, double constant) in cases)
        {
            Expression simplified = Simplify(source);
            Assert.AreEqual(constant, (double)AssertConstantPlus(simplified, a), $"{source} => {simplified}");
            Assert.AreEqual(simplified.ToString(), Simplify(simplified).ToString(), $"Simplifying {source} again must not change it.");
            Func<double, double> expected = Expression.Lambda<Func<double, double>>(source, a).Compile();
            Func<double, double> actual = Expression.Lambda<Func<double, double>>(simplified, a).Compile();
            foreach (double sample in new[] { 1.0, -2.0, 0.0, 1e6, double.NaN, double.PositiveInfinity })
            {
                Assert.AreEqual(Key(expected(sample)), Key(actual(sample)), $"{source} => {simplified} differs at a = {sample}.");
            }
        }
    }

    /// <summary>
    /// A non-constant term between the constants keeps its place among the other terms: <c>((a + 5) + b) + 6</c> and
    /// <c>(a + 5) + (b + 6)</c> give <c>11 + (a + b)</c>; the opaque terms are not normalized beyond the existing
    /// canonical order.
    /// </summary>
    [TestMethod]
    public void Simplify_ConstantsAroundOpaqueTerms_SumOnlyTheConstants()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Assert.AreEqual("(11 + (a + b))", Simplify(Expression.Add(Expression.Add(Expression.Add(a, C(5)), b), C(6))).ToString());
        Assert.AreEqual("(11 + (a + b))", Simplify(Expression.Add(Expression.Add(a, C(5)), Expression.Add(b, C(6)))).ToString());
        Assert.AreEqual("(a + b)", Simplify(Expression.Add(a, b)).ToString(), "Control: no constant, no change.");
        Assert.AreEqual("(5 + a)", Simplify(Expression.Add(a, C(5))).ToString(), "Control: a single constant is unchanged.");
    }

    // ------------------------------------------------------------------------------------------
    // Factored coefficients (J family)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Factored sums whose coefficients mix a non-constant term with constants fold the constants in one call: before,
    /// <c>(5 + a)*x + 5*x + 6*x</c> gave <c>(((5 + a) + 5) + 6) * x</c> (9 nodes, 3 constants), and
    /// <c>(a + 2)*x + 3*x</c> gave <c>((2 + a) + 3) * x</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_MixedFactoredCoefficients_SumTheirConstants()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression Term(Expression coefficient) => Expression.Multiply(coefficient, x);
        (Expression Source, string Expected)[] cases =
        [
            (Expression.Add(Expression.Add(Term(Expression.Add(C(5), a)), Term(C(5))), Term(C(6))), "((16 + a) * x)"),
            (Expression.Add(Term(Expression.Add(a, C(2))), Term(C(3))), "((5 + a) * x)"),
            (Expression.Add(Term(Expression.Add(C(2), a)), Term(C(3))), "((5 + a) * x)"),
            (Expression.Subtract(Term(Expression.Add(a, C(2))), Term(C(3))), "((-1 + a) * x)"),
            (Expression.Add(Term(Expression.Add(C(2), a)), Term(Expression.Add(b, C(3)))), "((5 + (a + b)) * x)"),
            (Expression.Add(Expression.Add(Term(C(2)), Term(a)), Term(C(3))), "((5 + a) * x)"),
            (Expression.Add(Term(C(2)), Expression.Add(Term(a), Term(C(3)))), "((5 + a) * x)"),
            (Expression.Add(Expression.Add(Expression.Add(Term(C(2)), Term(C(3))), Term(a)), Term(C(5))), "((10 + a) * x)"),
        ];
        foreach ((Expression source, string expected) in cases)
        {
            Expression simplified = Simplify(source);
            Assert.AreEqual(expected, simplified.ToString(), source.ToString());
            Assert.AreEqual(1, CountConstants(simplified), source.ToString());
            Func<double, double, double, double> before = Expression.Lambda<Func<double, double, double, double>>(source, a, b, x).Compile();
            Func<double, double, double, double> after = Expression.Lambda<Func<double, double, double, double>>(simplified, a, b, x).Compile();
            Assert.AreEqual(before(1.5, -2, 3), after(1.5, -2, 3), 1e-12, source.ToString());
        }
    }

    /// <summary>
    /// A coefficient with fewer than two constants keeps the ordinary factoring, and the unevaluated coefficient of the
    /// P3 second-pass scenario (no constant at all) is left as built (controls).
    /// </summary>
    [TestMethod]
    public void Simplify_CoefficientsWithoutTwoConstants_AreUnchanged()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreEqual("((a + 3) * x)", Simplify(Expression.Add(Expression.Multiply(a, x), Expression.Multiply(C(3), x))).ToString());
        Assert.AreEqual("((a + b) * x)", Simplify(Expression.Add(Expression.Multiply(a, x), Expression.Multiply(b, x))).ToString());
    }

    // ------------------------------------------------------------------------------------------
    // Numeric typing, overflow and exceptional values
    // ------------------------------------------------------------------------------------------

    /// <summary>Simplifies <paramref name="build"/> applied to a <typeparamref name="T"/> parameter and checks every sample against the source.</summary>
    /// <typeparam name="T">The operand type.</typeparam>
    /// <param name="build">Builds the source from the parameter.</param>
    /// <param name="samples">The inputs to evaluate.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression SimplifyAndCompare<T>(Func<ParameterExpression, Expression> build, params T[] samples)
    {
        ParameterExpression a = Expression.Parameter(typeof(T), "a");
        Expression source = build(a);
        Expression simplified = Simplify(source);
        Assert.AreEqual(typeof(T), simplified.Type);
        Func<T, T> expected = Expression.Lambda<Func<T, T>>(source, a).Compile();
        Func<T, T> actual = Expression.Lambda<Func<T, T>>(simplified, a).Compile();
        foreach (T sample in samples)
        {
            Assert.AreEqual(expected(sample), actual(sample), $"{source} => {simplified} differs at a = {sample}.");
        }

        return simplified;
    }

    /// <summary>
    /// Integral sums are exact (unchecked modular arithmetic is associative) and keep their type: <see cref="short"/>
    /// wraps in <see cref="short"/>, <see cref="int"/> and <see cref="long"/> wrap, and an unsigned type keeps the sign
    /// as the term's sign (<c>(a - 5u) + 3u</c> gives <c>(0 - 2) + a</c>, not <c>4294967294 + a</c>).
    /// </summary>
    [TestMethod]
    public void Simplify_IntegralConstants_SumExactlyInTheirType()
    {
        Expression K<T>(T value) => Expression.Constant(value, typeof(T));
        Assert.AreEqual("(-32536 + a)", SimplifyAndCompare<short>(a => Expression.Add(Expression.Add(a, K((short)32000)), K((short)1000)), 1, -5000).ToString());
        Assert.AreEqual("((0 - 2) + a)", SimplifyAndCompare<ushort>(a => Expression.Add(Expression.Subtract(a, K((ushort)5)), K((ushort)3)), 10, 1).ToString());
        Assert.AreEqual("(2 + a)", SimplifyAndCompare<ushort>(a => Expression.Subtract(Expression.Add(a, K((ushort)5)), K((ushort)3)), 10, ushort.MaxValue).ToString());
        Assert.AreEqual("(-2147483648 + a)", SimplifyAndCompare<int>(a => Expression.Add(Expression.Add(a, K(int.MaxValue)), K(1)), 1, -5, 0).ToString());
        Assert.AreEqual("(-2 + a)", SimplifyAndCompare<int>(a => Expression.Add(Expression.Subtract(a, K(5)), K(3)), 1, int.MinValue).ToString());
        Assert.AreEqual("((0 - 2) + a)", SimplifyAndCompare<uint>(a => Expression.Add(Expression.Subtract(a, K(5u)), K(3u)), 10u, 1u).ToString());
        Assert.AreEqual("(-2 + a)", SimplifyAndCompare<long>(a => Expression.Add(Expression.Add(a, K(long.MaxValue)), K(long.MaxValue)), 1L, -3L).ToString());
        Assert.AreEqual("((0 - 18446744073709551614) + a)", SimplifyAndCompare<ulong>(a => Expression.Add(Expression.Subtract(a, K(ulong.MaxValue)), K(1ul)), 5ul, 0ul).ToString());
    }

    /// <summary>
    /// Decimal constants separated by a parameter used to be summed (<c>(a + 1.5m) + 2.25m -&gt; 3.75m + a</c>), and a sum
    /// that overflowed left the terms unchanged instead of throwing while simplifying. Since ETC R1b (C-06) decimal addition
    /// may throw, and regrouping its constants can remove an overflow (<c>(a + MaxValue) - 1m</c> overflows for
    /// <c>a = 1</c>, <c>a + (MaxValue - 1m)</c> does not), so decimal chains are never reassociated: every chain keeps its
    /// constants, evaluates like the source, and still throws where the source throws.
    /// </summary>
    [TestMethod]
    public void Simplify_DecimalConstants_SumOrDeclineOnOverflow()
    {
        Expression K(decimal value) => Expression.Constant(value);
        Expression sum = SimplifyAndCompare<decimal>(a => Expression.Add(Expression.Add(a, K(1.5m)), K(2.25m)), 1m, -1.1m);
        Assert.AreEqual(2, CountConstants(sum), $"The decimal constants must not be regrouped: {sum}");
        Expression overflow = SimplifyAndCompare<decimal>(a => Expression.Add(Expression.Add(K(decimal.MaxValue), a), K(1m)), -10m, -1m);
        Assert.AreEqual(2, CountConstants(overflow), $"The overflowing sum must not be folded: {overflow}");
        ParameterExpression p = Expression.Parameter(typeof(decimal), "a");
        Expression nearMax = Simplify(Expression.Add(Expression.Add(p, K(decimal.MaxValue)), K(-1m)));
        Assert.AreEqual(2, CountConstants(nearMax), $"Regrouping would hide the overflow of a + MaxValue: {nearMax}");
        Assert.ThrowsExactly<OverflowException>(() => Expression.Lambda<Func<decimal, decimal>>(nearMax, p).Compile()(1m));
    }

    /// <summary>
    /// Floating-point constants combine with IEEE-754 rules: <c>inf - inf</c> is <c>NaN</c> (as the source gives for
    /// every <c>a</c>), <c>inf + 5</c> is <c>inf</c>, subnormal constants add exactly, a zero sum is dropped.
    /// </summary>
    [TestMethod]
    public void Simplify_FloatingConstants_CombineWithIeeeRules()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        Expression infinities = Simplify(Expression.Subtract(Expression.Add(a, C(double.PositiveInfinity)), C(double.PositiveInfinity)));
        Assert.IsTrue(double.IsNaN((double)((ConstantExpression)infinities).Value!));
        Assert.AreEqual(double.PositiveInfinity, (double)AssertConstantPlus(Simplify(Expression.Add(Expression.Add(a, C(double.PositiveInfinity)), C(5))), a));
        Assert.AreEqual(2 * double.Epsilon, (double)AssertConstantPlus(Simplify(Expression.Add(Expression.Add(a, C(double.Epsilon)), C(double.Epsilon))), a));
        Assert.AreSame(a, Simplify(Expression.Add(Expression.Add(a, C(5)), C(-5))));
        Assert.AreSame(a, Simplify(Expression.Add(Expression.Add(a, C(-0.0)), C(-0.0))));
        ParameterExpression f = Expression.Parameter(typeof(float), "f");
        Expression floatSum = Simplify(Expression.Add(Expression.Add(f, Expression.Constant(0.1f)), Expression.Constant(0.2f)));
        Assert.AreEqual(0.1f + 0.2f, (float)AssertConstantPlus(floatSum, f), "Float constants are summed in single precision.");
    }

    /// <summary>
    /// The reassociation is not bit-exact for floating-point types (characterization, allowed by the
    /// "Symbolic-equivalence contract"): <c>(a + 1e16) + 1</c> becomes <c>1e16 + a</c> (the folded <c>1e16 + 1</c>
    /// rounds to <c>1e16</c>), which gives <c>0</c> instead of <c>1</c> for <c>a = -1e16</c>; <c>(a + 5) + -5</c>
    /// becomes <c>a</c>, which keeps <c>-0</c> where the source gives <c>+0</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_FloatingReassociation_IsSymbolicNotBitExact()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        Expression source = Expression.Add(Expression.Add(a, C(1e16)), C(1));
        Expression simplified = Simplify(source);
        Assert.AreEqual(1e16, (double)AssertConstantPlus(simplified, a));
        Assert.AreEqual(1.0, Expression.Lambda<Func<double, double>>(source, a).Compile()(-1e16));
        Assert.AreEqual(0.0, Expression.Lambda<Func<double, double>>(simplified, a).Compile()(-1e16));

        Expression cancelling = Expression.Add(Expression.Add(a, C(5)), C(-5));
        Assert.AreEqual(Key(0.0), Key(Expression.Lambda<Func<double, double>>(cancelling, a).Compile()(-0.0)));
        Assert.AreEqual(Key(-0.0), Key(Expression.Lambda<Func<double, double>>(Simplify(cancelling), a).Compile()(-0.0)));
    }

    /// <summary>
    /// Unsigned chains with several positive and negative constants near <c>MaxValue</c> evaluate exactly like the
    /// source on every sample, keep their type and are left with one constant term whose sign is kept apart
    /// (review of #666).
    /// </summary>
    [TestMethod]
    public void Simplify_UnsignedConstantsNearMaxValue_SumExactly()
    {
        Expression K<T>(T value) => Expression.Constant(value, typeof(T));
        uint[] uintSamples = [0u, 1u, 5u, uint.MaxValue, uint.MaxValue - 3];
        Expression mixed = SimplifyAndCompare<uint>(
            a => Expression.Add(Expression.Subtract(Expression.Add(Expression.Subtract(Expression.Add(a, K(uint.MaxValue - 1)), K(5u)), K(7u)), K(uint.MaxValue)), K(2u)),
            uintSamples);
        Assert.AreEqual("((0 - 4294967293) + a)", mixed.ToString());
        Assert.AreEqual("((0 - 6) + a)", SimplifyAndCompare<uint>(a => Expression.Subtract(Expression.Subtract(Expression.Add(a, K(3u)), K(uint.MaxValue)), K(10u)), uintSamples).ToString());
        // The pre-existing factoring of e - c into (e - c) * 1 (also on master) adds a constant 1 around the sum.
        Expression positive = SimplifyAndCompare<uint>(a => Expression.Subtract(Expression.Add(Expression.Add(a, K(uint.MaxValue)), K(uint.MaxValue)), K(1u)), uintSamples);
        Assert.IsTrue(ContainsConstant(positive, uint.MaxValue - 2), positive.ToString());
        Assert.IsFalse(ContainsConstant(positive, uint.MaxValue), $"MaxValue + MaxValue - 1 must be one constant: {positive}");

        ulong[] ulongSamples = [0ul, 1ul, ulong.MaxValue, ulong.MaxValue - 7];
        Assert.AreEqual("((0 - 7) + a)", SimplifyAndCompare<ulong>(
            a => Expression.Add(Expression.Subtract(Expression.Add(Expression.Subtract(a, K(ulong.MaxValue)), K(ulong.MaxValue - 2)), K(9ul)), K(4ul)),
            ulongSamples).ToString());
        Assert.AreEqual("((0 - 18446744073709551614) + a)", SimplifyAndCompare<ulong>(a => Expression.Subtract(Expression.Subtract(a, K(ulong.MaxValue)), K(ulong.MaxValue)), ulongSamples).ToString());
    }

    /// <summary>
    /// Decimal chains whose constants cancel through the limits used to fold when the running sum stayed representable
    /// (<c>((a + MaxValue) - MaxValue) + MaxValue -&gt; MaxValue + a</c>); since ETC R1b (C-06) a decimal chain is not
    /// reassociated (its additions may throw), so the three constants are kept. A chain whose total overflows
    /// (<c>(a + MinValue) - 1</c>) is still simplified without throwing, and both evaluate like the source wherever the
    /// source evaluates (review of #666).
    /// </summary>
    [TestMethod]
    public void Simplify_DecimalConstantsNearTheLimits_FoldOrDeclineWithoutThrowing()
    {
        Expression K(decimal value) => Expression.Constant(value);
        Expression folded = SimplifyAndCompare<decimal>(
            a => Expression.Add(Expression.Subtract(Expression.Add(a, K(decimal.MaxValue)), K(decimal.MaxValue)), K(decimal.MaxValue)),
            -decimal.MaxValue, -1m, 0m);
        Assert.AreEqual(3, CountConstants(folded), $"The decimal chain must not be reassociated: {folded}");

        Expression declined = SimplifyAndCompare<decimal>(a => Expression.Subtract(Expression.Add(a, K(decimal.MinValue)), K(1m)), 1m, 10m, decimal.MaxValue);
        Assert.IsTrue(ContainsConstant(declined, decimal.MinValue) && ContainsConstant(declined, 1m), $"The overflowing total must not be folded: {declined}");
    }

    /// <summary>Determines whether <paramref name="expression"/> holds a constant equal to <paramref name="value"/>.</summary>
    /// <param name="expression">The tree.</param>
    /// <param name="value">The value to look for.</param>
    /// <returns><see langword="true"/> when a constant leaf equals <paramref name="value"/>.</returns>
    private static bool ContainsConstant(Expression expression, object value) => expression switch
    {
        BinaryExpression binary => ContainsConstant(binary.Left, value) || ContainsConstant(binary.Right, value),
        UnaryExpression unary => ContainsConstant(unary.Operand, value),
        ConstantExpression constant => Equals(constant.Value, value),
        _ => false,
    };

    /// <summary>
    /// Floating constants mixing <c>+inf</c>, <c>-inf</c>, <c>NaN</c>, <c>+0</c> and <c>-0</c> evaluate like the source
    /// on special samples, with the two documented S3 differences: a signed zero (<c>(a + -0) + 0 -&gt; a</c> keeps
    /// <c>-0</c>) and an overflowing reassociation (<c>((a + max) + max) - max -&gt; inf + a</c>, <c>NaN</c> instead of
    /// <c>-inf</c> for <c>a = -inf</c>) (review of #666).
    /// </summary>
    [TestMethod]
    public void Simplify_NonFiniteAndSignedZeroConstants_CombineLikeTheSource()
    {
        double[] samples = [0.0, -0.0, 1.5, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.Epsilon];
        Func<ParameterExpression, Expression>[] exact =
        [
            a => Expression.Add(Expression.Add(a, C(double.PositiveInfinity)), C(double.NegativeInfinity)),
            a => Expression.Subtract(Expression.Add(Expression.Add(a, C(double.PositiveInfinity)), C(5)), C(double.PositiveInfinity)),
            a => Expression.Add(Expression.Add(Expression.Add(a, C(double.PositiveInfinity)), C(-0.0)), C(0.0)),
            a => Expression.Add(Expression.Add(Expression.Add(a, C(double.NaN)), C(double.PositiveInfinity)), C(-0.0)),
            a => Expression.Subtract(Expression.Add(a, C(double.NegativeInfinity)), C(double.PositiveInfinity)),
            a => Expression.Subtract(Expression.Add(a, C(-0.0)), C(0.0)),
        ];
        foreach (Func<ParameterExpression, Expression> build in exact)
        {
            ParameterExpression a = Expression.Parameter(typeof(double), "a");
            Expression source = build(a);
            Expression simplified = Simplify(source);
            Func<double, double> expected = Expression.Lambda<Func<double, double>>(source, a).Compile();
            Func<double, double> actual = Expression.Lambda<Func<double, double>>(simplified, a).Compile();
            foreach (double sample in samples)
            {
                Assert.AreEqual(Key(expected(sample)), Key(actual(sample)), $"{source} => {simplified} differs at a = {sample}.");
            }
        }

        ParameterExpression p = Expression.Parameter(typeof(double), "a");
        Expression signedZero = Expression.Add(Expression.Add(p, C(-0.0)), C(0.0));
        Assert.AreSame(p, Simplify(signedZero));
        Assert.AreEqual(Key(0.0), Key(Expression.Lambda<Func<double, double>>(signedZero, p).Compile()(-0.0)), "Characterization: the source gives +0.");

        Expression overflowing = Expression.Subtract(Expression.Add(Expression.Add(p, C(double.MaxValue)), C(double.MaxValue)), C(double.MaxValue));
        Expression reassociated = Simplify(overflowing);
        Assert.AreEqual(double.PositiveInfinity, (double)AssertConstantPlus(reassociated, p));
        Assert.AreEqual(double.NegativeInfinity, Expression.Lambda<Func<double, double>>(overflowing, p).Compile()(double.NegativeInfinity));
        Assert.IsTrue(double.IsNaN(Expression.Lambda<Func<double, double>>(reassociated, p).Compile()(double.NegativeInfinity)), "Characterization: S3 reassociation.");
    }

    /// <summary>
    /// Factored coefficients mixing several subtractions aggregate their constants with the right signs, with free and
    /// bound lambda parameters: <c>((a - 2)*x - (3 - b)*x) - 4*x -&gt; (-9 + (a + b)) * x</c> (review of #666).
    /// </summary>
    [TestMethod]
    public void Simplify_CoefficientsWithSeveralSubtractions_AggregateWithSigns()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Expression body = Expression.Subtract(
            Expression.Subtract(Expression.Multiply(Expression.Subtract(a, C(2)), x), Expression.Multiply(Expression.Subtract(C(3), b), x)),
            Expression.Multiply(C(4), x));
        Assert.AreEqual("((-9 + (a + b)) * x)", Simplify(body).ToString());

        var source = Expression.Lambda<Func<double, double, double, double>>(body, x, a, b);
        var lambda = (Expression<Func<double, double, double, double>>)Simplify(source);
        Assert.AreEqual("((-9 + (a + b)) * x)", lambda.Body.ToString());
        CollectionAssert.AreEqual(new[] { x, a, b }, lambda.Parameters.ToArray());
        Func<double, double, double, double> before = source.Compile();
        Func<double, double, double, double> after = lambda.Compile();
        foreach ((double xv, double av, double bv) in new[] { (2.0, 3.0, 5.0), (-1.5, 0.25, 4.0), (0.0, 1.0, -1.0) })
        {
            Assert.AreEqual(before(xv, av, bv), after(xv, av, bv), 1e-12);
        }
    }

    /// <summary>A call whose argument is a custom constant, used to observe invocations independently of comparisons.</summary>
    /// <param name="value">The custom constant.</param>
    /// <returns>Always <c>1</c>.</returns>
    public static double CountingCall(object value)
    {
        _countingIdentityCalls++;
        return 1.0;
    }

    /// <summary>A custom constant value that counts the <see cref="object.Equals(object?)"/> calls made on it.</summary>
    private sealed class CountingConstant
    {
        /// <summary>Gets the number of <see cref="Equals(object?)"/> calls.</summary>
        public int EqualsCalls { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            EqualsCalls++;
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>
    /// The aggregation never invokes a <see cref="MethodCallExpression"/> of the tree, in a sum and in a factored
    /// coefficient, whether or not structural comparisons reach the <see cref="object.Equals(object?)"/> of a custom
    /// constant in its arguments (the S3 remarks allow the latter; here they make none); the call runs once per
    /// evaluation (review of #666). Since ETC R1b (C-06) the call is a pinned term: the constants around it are still
    /// summed in the sum, but the factored form <c>(call + 2)*x + 3*x</c>, whose terms contain the call, is no longer
    /// factored; it keeps the call once and evaluates like the source.
    /// </summary>
    [TestMethod]
    public void Simplify_Aggregation_NeverInvokesTreeCalls()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var custom = new CountingConstant();
        Expression call = Expression.Call(Method(nameof(CountingCall)), Expression.Constant(custom, typeof(object)));
        _countingIdentityCalls = 0;
        Expression sum = Simplify(Expression.Add(Expression.Add(call, C(5)), C(6)));
        Expression factored = Simplify(Expression.Add(Expression.Multiply(Expression.Add(call, C(2)), x), Expression.Multiply(C(3), x)));
        Assert.AreEqual(0, _countingIdentityCalls, "No call of the tree may run while simplifying.");
        Assert.AreEqual(11.0, (double)((ConstantExpression)((BinaryExpression)sum).Left).Value!);
        Assert.AreEqual(1, factored.ToString().Split("CountingCall(").Length - 1, factored.ToString());
        Assert.AreEqual(12.0, Expression.Lambda<Func<double>>(sum).Compile()());
        Assert.AreEqual(1, _countingIdentityCalls);
        Assert.AreEqual(18.0, Expression.Lambda<Func<double, double>>(factored, x).Compile()(3.0));
        Assert.AreEqual(2, _countingIdentityCalls);
        // custom.EqualsCalls is deliberately not constrained: structural comparisons may call it (S3 remarks).
    }

    // ------------------------------------------------------------------------------------------
    // User code, custom operators, scope and derived simplifiers
    // ------------------------------------------------------------------------------------------

    /// <summary>A call term is kept, no user code runs while simplifying, and the call still runs once per evaluation.</summary>
    [TestMethod]
    public void Simplify_CallTerm_SumsConstantsWithoutRunningUserCode()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        Expression call = Expression.Call(Method(nameof(CountingIdentity)), a);
        _countingIdentityCalls = 0;
        Expression simplified = Simplify(Expression.Add(Expression.Add(call, C(5)), C(6)));
        Assert.AreEqual(0, _countingIdentityCalls);
        Assert.AreEqual(11.0, (double)AssertConstantPlus(simplified, ((BinaryExpression)simplified).Right));
        Assert.AreEqual(13.0, Expression.Lambda<Func<double, double>>(simplified, a).Compile()(2.0));
        Assert.AreEqual(1, _countingIdentityCalls);
    }

    /// <summary>Custom and lifted additions are never aggregated (controls).</summary>
    [TestMethod]
    public void Simplify_CustomOrLiftedAdditions_AreNotAggregated()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        Expression inner = Simplify(Expression.Add(Expression.Add(a, C(5), Method(nameof(CustomAdd))), C(6)));
        Assert.AreEqual(48.0, Expression.Lambda<Func<double, double>>(inner, a).Compile()(1.0));
        Expression outer = Simplify(Expression.Add(Expression.Add(a, C(5)), C(6), Method(nameof(CustomAdd))));
        Assert.AreEqual(42.0, Expression.Lambda<Func<double, double>>(outer, a).Compile()(1.0));

        ParameterExpression n = Expression.Parameter(typeof(double?), "n");
        Expression lifted = Simplify(Expression.Add(Expression.Add(n, Expression.Constant(5.0, typeof(double?))), Expression.Constant(6.0, typeof(double?))));
        Assert.AreEqual(2, CountConstants(lifted));
        Assert.IsNull(Expression.Lambda<Func<double?, double?>>(lifted, n).Compile()(null));
    }

    /// <summary>Lambda bodies aggregate in one call and keep their parameters; a derived simplifier behaves the same.</summary>
    [TestMethod]
    public void Simplify_LambdaAndDerivedSimplifier_Aggregate()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        var lambda = (LambdaExpression)Simplify(Expression.Lambda(Expression.Add(Expression.Add(a, C(5)), C(6)), a));
        Assert.AreEqual(11.0, (double)AssertConstantPlus(lambda.Body, a));
        Assert.AreSame(a, lambda.Parameters[0]);

        Expression derived = new MinimalDerivedSimplifier().Simplify(Expression.Add(Expression.Add(a, C(5)), C(6)));
        Assert.AreEqual(11.0, (double)AssertConstantPlus(derived, a));
    }

    /// <summary>A derived simplifier with no overrides.</summary>
    private sealed class MinimalDerivedSimplifier : ExpressionSimplifier
    {
    }
}
