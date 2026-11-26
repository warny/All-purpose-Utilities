using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Mission B (<c>docs/expressions/SimplifierFlatten.md</c> §23): the last canonicalization of a refused additive chain builds pairs
/// that only the next call offered to <see cref="ExpressionSimplifier"/>'s binary factoring rules (<c>AdditionOfEqualsElements</c>,
/// <c>SubstractionOfEqualsElements</c>): the two operands of the last spine node (a sum or a quotient as a factor) and the groups
/// of identical terms that are no monomials (<c>(z / z) + (z / z)</c>). Those nodes are now run again at the boundary of the chain,
/// as the next call ran them, so that one call gives the second call of <c>master</c> <c>7664b9ca</c> (reference H: the binary
/// finishing switched off). Nothing is admitted to the global factoring: a quotient or a sum stays a factor of the binary rules only.
/// </summary>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierBinaryFactoringFinalizationTests
{
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");
    private static readonly ParameterExpression W = Expression.Parameter(typeof(double), "w");
    private static readonly ParameterExpression[] Variables = [X, Y, Z, W];

    private static readonly MethodInfo SinMethod = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!;
    private static readonly MethodInfo CosMethod = typeof(double).GetMethod(nameof(double.Cos), [typeof(double)])!;
    private static readonly MethodInfo LogMethod = typeof(double).GetMethod(nameof(double.Log), [typeof(double)])!;

    /// <summary>A derived simplifier without any member: it keeps the historical treatment.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Observable calls that log themselves, so that an evaluation count and order can be compared.</summary>
    public static class Probe
    {
        /// <summary>The calls made on the current thread, in order.</summary>
        [ThreadStatic]
        private static List<string>? t_log;

        /// <summary>Gets the calls made on the current thread, in order.</summary>
        public static List<string> Log => t_log ??= [];

        /// <summary>An observable call: logs itself and returns 3.</summary>
        /// <returns>3.</returns>
        public static double F()
        {
            Log.Add("F");
            return 3;
        }

        /// <summary>An observable call that throws when its argument is zero, and logs itself otherwise.</summary>
        /// <param name="value">The argument.</param>
        /// <returns><paramref name="value"/>.</returns>
        public static double G(double value)
        {
            Log.Add("G");
            return value == 0 ? throw new InvalidOperationException("G(0)") : value;
        }
    }

    /// <summary>Simplifies with a new built-in simplifier.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression S(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Simplifies with the binary finishing switched off: the result of <c>master</c> <c>7664b9ca</c> (reference H).</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression H(Expression expression)
    {
        ExpressionSimplifier.RefusedBinaryFinishingDisabled = true;
        try
        {
            return S(expression);
        }
        finally
        {
            ExpressionSimplifier.RefusedBinaryFinishingDisabled = false;
        }
    }

    /// <summary>Formats with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>A constant of <see cref="double"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression K(double value) => Expression.Constant(value);

    /// <summary>A sum.</summary>
    /// <param name="left">The left term.</param>
    /// <param name="right">The right term.</param>
    /// <returns>The sum.</returns>
    private static Expression Add(Expression left, Expression right) => Expression.Add(left, right);

    /// <summary>A difference.</summary>
    /// <param name="left">The left term.</param>
    /// <param name="right">The right term.</param>
    /// <returns>The difference.</returns>
    private static Expression Sub(Expression left, Expression right) => Expression.Subtract(left, right);

    /// <summary>A product.</summary>
    /// <param name="left">The left factor.</param>
    /// <param name="right">The right factor.</param>
    /// <returns>The product.</returns>
    private static Expression M(Expression left, Expression right) => Expression.Multiply(left, right);

    /// <summary>A quotient.</summary>
    /// <param name="left">The numerator.</param>
    /// <param name="right">The denominator.</param>
    /// <returns>The quotient.</returns>
    private static Expression Q(Expression left, Expression right) => Expression.Divide(left, right);

    /// <summary>A power.</summary>
    /// <param name="left">The base.</param>
    /// <param name="right">The exponent.</param>
    /// <returns>The power.</returns>
    private static Expression P(Expression left, Expression right) => Expression.Power(left, right);

    /// <summary><c>sin(a)^2 + cos(a)^2</c>, which the trigonometric rule folds to <c>1</c>.</summary>
    /// <param name="argument">The argument.</param>
    /// <returns>The sum.</returns>
    private static Expression TrigonometricOne(Expression argument)
        => Add(P(Expression.Call(SinMethod, argument), K(2)), P(Expression.Call(CosMethod, argument), K(2)));

    /// <summary>
    /// Asserts that one call gives <paramref name="expected"/>, that the reference needed a second call to reach it (H, then H
    /// again), that it is a structural fixed point, and that it evaluates like the source.
    /// </summary>
    /// <param name="source">The source over <see cref="Variables"/>.</param>
    /// <param name="historical">The expected text of the reference's first result.</param>
    /// <param name="expected">The expected text of the first result.</param>
    private static void AssertSecondCallInOne(Expression source, string historical, string expected)
    {
        Expression reference = H(source);
        Assert.AreEqual(historical, Text(reference), "The reference's first result.");
        Assert.AreEqual(expected, Text(H(reference)), "The reference's second call.");
        Expression once = S(source);
        Assert.AreEqual(expected, Text(once), Text(source));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), $"Not a fixed point: {Text(once)} -> {Text(S(once))}");
        AssertSameValues(source, once);
    }

    /// <summary>Asserts that one call gives the reference (nothing to finish), a fixed point, with the source's values.</summary>
    /// <param name="source">The source over <see cref="Variables"/>.</param>
    private static void AssertHistorical(Expression source)
    {
        Expression once = S(source);
        Assert.AreEqual(Text(H(source)), Text(once), Text(source));
        AssertSameValues(source, once);
    }

    /// <summary>
    /// Asserts that two trees over the variables evaluate alike on moderate samples (equal, or within a relative 1e-12 for the
    /// reassociation latitude of the symbolic contract).
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="simplified">The simplified tree.</param>
    private static void AssertSameValues(Expression source, Expression simplified)
    {
        Delegate before = Expression.Lambda(source, Variables).Compile();
        Delegate after = Expression.Lambda(simplified, Variables).Compile();
        double[][] samples = [[0.5, 1.5, 2.5, -1.25], [1.25, -3, 0.75, 2], [-2, 0.5, 1.5, 0.25], [3, -0.5, -4, 1.5]];
        foreach (double[] sample in samples)
        {
            object[] arguments = sample.Cast<object>().ToArray();
            double a = (double)before.DynamicInvoke(arguments)!, b = (double)after.DynamicInvoke(arguments)!;
            bool same = a.Equals(b) || Math.Abs(a - b) <= 1e-12 * Math.Max(Math.Abs(a), Math.Abs(b));
            Assert.IsTrue(same, $"{Text(source)} -> {Text(simplified)}: {a} against {b}");
        }
    }

    /// <summary>Simplifies with the admission counters installed.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The counters.</returns>
    private static FlattenAdmissionStatistics Statistics(Expression source)
    {
        FlattenAdmissionStatistics? previous = ExpressionSimplifier.AdmissionStatistics;
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            S(source);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = previous;
        }

        return statistics;
    }

    /// <summary>
    /// nested#3633 of the S5 campaign (family A, a sum factor): the levels sort <c>-(-2 * x)</c> and <c>-(x * (2 + z))</c> next to
    /// each other after the quotient, and only the next call's <c>AdditionOfEqualsElements</c> extracted <c>x</c>, the sum
    /// <c>2 + z</c> staying in the residue.
    /// </summary>
    [TestMethod]
    public void Nested3633_SumFactor_FactoredInOneCall()
    {
        Expression source = Sub(
            Sub(Sub(Q(Q(K(1E+300), Z), Q(Z, K(-2))), Sub(Sub(Sub(K(-1), X), Y), X)), Sub(M(Z, X), M(K(-2), X))),
            K(-2));
        AssertSecondCallInOne(
            source,
            "3 + (y + ((-2E+300 / (z ** 2)) + ((0 - (-2 * x)) + (0 - (x * (2 + z))))))",
            "3 + (y + ((-2E+300 / (z ** 2)) + (0 - (z * x))))");

        // The reduced form of the documentation is factored by the level itself; behind a quotient, by the finishing.
        Assert.AreEqual("z * x", Text(S(Add(M(K(-2), X), M(X, Add(K(2), Z))))));
        AssertSecondCallInOne(
            Add(Add(Q(Y, W), M(K(-2), X)), M(X, Add(K(2), Z))),
            "(y / w) + ((-2 * x) + (x * (2 + z)))",
            "(y / w) + (z * x)");
        AssertSecondCallInOne(
            Sub(Sub(Q(Y, W), M(K(2), X)), M(X, Add(K(2), Z))),
            "(y / w) + ((0 - (2 * x)) + (0 - (x * (2 + z))))",
            "(y / w) + (0 - (x * (4 + z)))");
    }

    /// <summary>
    /// nested#3674 (family B, a quotient in a residue) and nested#3002 (family C, a sum and a quotient): on <c>master</c> both are
    /// fixed points after one call; their documented residues (§21.9, §22.9) only appear under the sign normalizations V1b and
    /// V1b + C1. The factor of the reduced forms is the coefficient <c>2</c>, the quotient staying in the residue.
    /// </summary>
    [TestMethod]
    public void Nested3674AndNested3002_AreFixedPointsOnMaster_AndFactorTheirCoefficient()
    {
        Expression nested3674 = Sub(
            Q(Q(K(0.5), K(-1)), Q(Y, K(0.5))),
            Sub(Sub(K(-2), Sub(K(0.5), Sub(K(0.5), K(1E+300)))), Sub(M(K(-2), Q(Z, K(-2))), Add(M(K(1E+300), K(2)), M(Y, K(2))))));
        Expression nested3002 = Sub(
            M(Q(Q(X, Z), Q(X, K(-1))), Sub(Sub(Sub(X, X), K(0)), K(2))),
            M(Sub(Sub(Sub(Z, Z), K(2)), Z), Sub(Sub(Sub(X, X), K(0)), K(2))));
        foreach (Expression source in new[] { nested3674, nested3002 })
        {
            AssertHistorical(source);
            Expression once = S(source);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), Text(once));
        }

        Assert.AreEqual("-1E+300 + ((-0.25 / y) + ((-2 * (z / -2)) + (0 - (2 * y))))", Text(S(nested3674)));
        Assert.AreEqual("-(-2 * (-2 + ((0 - z) + (x / (z * x)))))", Text(S(nested3002)));

        Assert.AreEqual("2 * (y + (z / -2))", Text(S(Add(M(K(2), Y), M(K(2), Q(Z, K(-2)))))));
        Assert.AreEqual("2 * (-2 + ((0 - z) + (x / (z * x))))", Text(S(Add(M(K(2), Sub(K(-2), Z)), M(K(2), Q(X, M(Z, X)))))));
        AssertSecondCallInOne(
            Add(Add(Q(X, W), M(K(2), Y)), M(K(2), Q(Z, K(-2)))),
            "(x / w) + ((2 * y) + (2 * (z / -2)))",
            "(x / w) + (2 * (y + (z / -2)))");
    }

    /// <summary>
    /// gen#947 (family D, a group of quotients): the canonicalization groups the identical quotients <c>z / z</c> without merging
    /// them (they are no monomials); the next call merged the group into <c>2 * (z / z)</c>, then factored the complete quotient
    /// with <c>y * (z / z)</c>.
    /// </summary>
    [TestMethod]
    public void Gen947_QuotientGroup_IsMergedThenFactoredInOneCall()
    {
        Expression source = Sub(
            Sub(M(Sub(Y, Y), Expression.Negate(Y)), M(Q(Z, Z), Expression.Negate(Y))),
            Sub(
                Sub(Sub(Sub(Add(K(-1), K(-2)), P(K(-2), K(3))), Sub(K(-2), Y)), Q(Z, Z)),
                Sub(Add(M(X, TrigonometricOne(Z)), M(Q(Z, Z), TrigonometricOne(Z))), K(0.5))));
        AssertSecondCallInOne(
            source,
            "-7.5 + ((0 - y) + (x + (((z / z) + (z / z)) + (y * (z / z)))))",
            "-7.5 + ((0 - y) + (x + ((2 + y) * (z / z))))");
        AssertSecondCallInOne(
            Add(Add(Add(X, Q(Z, Z)), Q(Z, Z)), M(Y, Q(Z, Z))),
            "x + (((z / z) + (z / z)) + (y * (z / z)))",
            "x + ((2 + y) * (z / z))");
    }

    /// <summary>
    /// gen#1727 (family D, a subtracted group): <c>(0 - (x / z)) - (x / z)</c>, inside the base of a power, was merged by the next
    /// call's <c>SubstractionOfEqualsElements</c> into <c>0 - (2 * (x / z))</c>.
    /// </summary>
    [TestMethod]
    public void Gen1727_SubtractedQuotientGroup_IsMergedInOneCall()
    {
        Expression source = P(
            Sub(Sub(P(P(K(0.5), K(0)), K(3)), Sub(Q(X, Z), Sub(Expression.Call(CosMethod, K(3)), Q(X, Z)))), M(Expression.Negate(M(Y, Y)), Sub(X, X))),
            Q(Expression.Negate(Expression.Negate(K(-1))), M(Expression.Call(LogMethod, Sub(K(-2), K(10))), TrigonometricOne(K(0)))));
        AssertSecondCallInOne(
            source,
            "(1 + (((0 - (x / z)) - (x / z)) + Double.Cos(3))) ** (-1 / Double.Log(-12))",
            "(1 + ((0 - (2 * (x / z))) + Double.Cos(3))) ** (-1 / Double.Log(-12))");
        AssertSecondCallInOne(
            Sub(Sub(K(1), Q(X, Z)), Sub(Expression.Call(CosMethod, K(3)), Sub(K(0), Q(X, Z)))),
            "1 + (((0 - (x / z)) - (x / z)) + (0 - Double.Cos(3)))",
            "1 + ((0 - (2 * (x / z))) + (0 - Double.Cos(3)))");
        AssertSecondCallInOne(Add(Add(Q(X, Z), Y), Q(X, Z)), "y + ((x / z) + (x / z))", "y + (2 * (x / z))");
    }

    /// <summary>
    /// The four elementary families behind a quotient that refuses the chain: a complete quotient as the common factor, a common
    /// coefficient of terms holding quotients, a common sum factor, and a sum in the coefficient of one term; a pair without any
    /// common factor keeps the historical result.
    /// </summary>
    [TestMethod]
    public void ElementaryFamilies_FactorAsTheNextCall()
    {
        AssertSecondCallInOne(
            Add(Add(Add(Q(Y, W), M(X, Q(Z, W))), Y), M(Y, Q(Z, W))),
            "y + ((y / w) + ((x * (z / w)) + (y * (z / w))))",
            "y + ((y / w) + ((x + y) * (z / w)))");
        AssertSecondCallInOne(
            Add(Add(Q(X, W), M(K(3), Q(Y, Z))), M(K(3), Q(X, Z))),
            "(x / w) + ((3 * (y / z)) + (3 * (x / z)))",
            "(x / w) + (3 * ((y / z) + (x / z)))");
        AssertSecondCallInOne(
            Add(Add(Q(X, W), M(Add(Y, K(1)), X)), M(Z, Add(Y, K(1)))),
            "(x / w) + ((x * (1 + y)) + (z * (1 + y)))",
            "(x / w) + ((1 + y) * (x + z))");
        AssertSecondCallInOne(
            Add(Add(Q(X, W), M(K(2), Add(Y, Z))), M(K(2), X)),
            "(x / w) + ((2 * x) + (2 * (y + z)))",
            "(x / w) + (2 * (x + (y + z)))");

        Expression none = Add(Add(Q(X, W), M(K(2), Y)), M(K(3), Q(Z, X)));
        AssertHistorical(none);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(S(none), S(S(none))));
        Assert.AreEqual(0, Statistics(none).BinaryFinishings);
    }

    /// <summary>
    /// Every association of the same terms gives, in one call, the reference's second call, a fixed point, for free and for bound
    /// parameters: <c>z * x</c> or <c>x * z</c>, the free-parameter tie of the canonical order (L-3) deciding between them as it
    /// does on <c>master</c>.
    /// </summary>
    [TestMethod]
    public void Associations_AndBoundParameters_GiveTheReferenceFixedPoint()
    {
        Expression q = Q(Y, W), a = M(K(-2), X), b = M(X, Add(K(2), Z));
        Expression[] sources = [Add(Add(q, a), b), Add(q, Add(a, b)), Add(Add(a, q), b), Add(a, Add(q, b)), Add(Add(a, b), q), Add(b, Add(q, a))];
        foreach (Expression source in sources)
        {
            Expression once = S(source);
            Assert.AreEqual(Text(H(H(source))), Text(once), Text(source));
            CollectionAssert.Contains(new[] { "(y / w) + (z * x)", "(y / w) + (x * z)" }, Text(once), Text(source));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), Text(once));
            AssertSameValues(source, once);

            LambdaExpression lambda = Expression.Lambda(source, Variables);
            var bound = (LambdaExpression)S(lambda);
            Assert.AreEqual(Text(H(H(lambda))), Text(bound), "bound");
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(bound, S(bound)), "bound fixed point");
        }
    }

    /// <summary>
    /// A shared factor (a DAG: one sum instance in two products) is factored like distinct copies, and the finishing runs once per
    /// boundary.
    /// </summary>
    [TestMethod]
    public void SharedFactor_AndCounters()
    {
        Expression shared = Add(Y, K(1));
        Expression source = Add(Add(Q(X, W), M(shared, X)), M(Z, shared));
        Assert.AreEqual("(x / w) + ((1 + y) * (x + z))", Text(S(source)));

        FlattenAdmissionStatistics statistics = Statistics(source);
        Assert.AreEqual(1, statistics.BinaryFinishings);
        Assert.AreEqual(1, statistics.BinaryCandidates);
        Assert.AreEqual(1, statistics.BinaryPrefixLevels);
        Assert.IsTrue(statistics.BinarySearches >= 1);
    }

    /// <summary>
    /// The integral, floating-point and decimal types. An integral division by a parameter may throw: it is pinned, never grouped,
    /// and the division by zero is kept; a division by a constant is inert, and <c>t + t = 2 * t</c> is exact in modular arithmetic.
    /// <see cref="short"/>, <see cref="decimal"/> and lifted chains are not canonicalized and keep their historical result.
    /// </summary>
    [TestMethod]
    public void NumericTypes_FinishExactly_AndKeepTheirExceptions()
    {
        foreach (Type type in new[] { typeof(int), typeof(long), typeof(uint), typeof(ulong), typeof(short), typeof(float), typeof(decimal), typeof(int?), typeof(double) })
        {
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            ParameterExpression x = Expression.Parameter(type, "x"), y = Expression.Parameter(type, "y"), w = Expression.Parameter(type, "w");
            Expression Constant(long value) => Expression.Constant(Convert.ChangeType(value, underlying), type);
            bool canonical = underlying != typeof(short) && underlying != typeof(decimal) && type == underlying;
            bool integral = underlying != typeof(float) && underlying != typeof(double) && underlying != typeof(decimal);

            Expression group = Add(Add(Q(x, Constant(3)), y), Q(x, Constant(3)));
            Expression pinned = Add(Add(Q(x, w), y), Q(x, w));
            Expression simplifiedGroup = new ExpressionSimplifier().Simplify(group);
            Expression simplifiedPinned = new ExpressionSimplifier().Simplify(pinned);
            if (canonical)
            {
                Assert.AreEqual(Text(H(H(group))), Text(simplifiedGroup), type.Name);
                Assert.IsTrue(Text(simplifiedGroup).Contains("(2"), type.Name + ": " + Text(simplifiedGroup));
            }
            else
            {
                Assert.AreEqual(Text(H(group)), Text(simplifiedGroup), type.Name);
            }

            // An integral quotient by a parameter is pinned: identical pinned terms are never grouped nor merged.
            if (integral) Assert.AreEqual(Text(H(pinned)), Text(simplifiedPinned), type.Name);

            foreach ((Expression source, Expression simplified) in new[] { (group, simplifiedGroup), (pinned, simplifiedPinned) })
            {
                Delegate before = Expression.Lambda(source, x, y, w).Compile(), after = Expression.Lambda(simplified, x, y, w).Compile();
                long[] values = underlying == typeof(uint) || underlying == typeof(ulong) ? [0, 1, 7, 13, 4000000000] : [0, 1, -1, 7, -13, short.MaxValue, short.MinValue];
                foreach (long a in values)
                {
                    foreach (long b in values)
                    {
                        object[] arguments = [Value(a, underlying), Value(b, underlying), Value(b, underlying)];
                        (object? Value, Type? Exception) expected = Evaluate(before, arguments), actual = Evaluate(after, arguments);
                        string message = $"{type.Name}: {Text(source)} -> {Text(simplified)} on {a}, {b}";
                        Assert.AreEqual(expected.Exception, actual.Exception, message);

                        // Integral and decimal values are exact; a floating-point sum is reassociated (the S3 latitude the next call takes).
                        if (underlying == typeof(float) || underlying == typeof(double))
                        {
                            double e = Convert.ToDouble(expected.Value), r = Convert.ToDouble(actual.Value);
                            Assert.IsTrue(e.Equals(r) || Math.Abs(e - r) <= 1e-6 * Math.Max(Math.Abs(e), Math.Abs(r)), message);
                        }
                        else
                        {
                            Assert.AreEqual(expected.Value, actual.Value, message);
                        }
                    }
                }
            }
        }

        static object Value(long value, Type type) => type == typeof(float) ? (float)value : type == typeof(double) ? (double)value : type == typeof(decimal) ? (decimal)value : Convert.ChangeType(unchecked(type == typeof(uint) ? (uint)value : type == typeof(short) ? (short)value : type == typeof(int) ? (int)value : value), type);
    }

    /// <summary>
    /// <c>int.MinValue / -1</c> throws: such a quotient is pinned, so its group is kept and the exception stays the source's.
    /// </summary>
    [TestMethod]
    public void IntegralDivisionByMinusOne_KeepsItsOverflow()
    {
        ParameterExpression x = Expression.Parameter(typeof(int), "x"), y = Expression.Parameter(typeof(int), "y");
        Expression source = Expression.Add(Expression.Add(Expression.Divide(x, Expression.Constant(-1)), y), Expression.Divide(x, Expression.Constant(-1)));
        Expression simplified = new ExpressionSimplifier().Simplify(source);
        Assert.AreEqual(Text(H(source)), Text(simplified));
        Delegate before = Expression.Lambda(source, x, y).Compile(), after = Expression.Lambda(simplified, x, y).Compile();
        Assert.AreEqual(Evaluate(before, [int.MinValue, 1]), Evaluate(after, [int.MinValue, 1]));
        Assert.AreEqual(typeof(OverflowException), Evaluate(after, [int.MinValue, 1]).Exception);
    }

    /// <summary>
    /// Floating-point specials: a doubling is exact, so a merged quotient group gives the source's values bit for bit, <c>NaN</c>,
    /// infinities and signed zeros included. Non-finite and negative zero coefficients, which the global factoring refuses, are
    /// not finished either: the binary rule's factoring of them (<c>Infinity * y + Infinity * (z / w)</c> is <c>NaN</c> for
    /// <c>y = 1</c>, <c>z / w = 0</c>, its factored form <c>Infinity</c>) stays the next call's IEEE latitude, as on <c>master</c>.
    /// </summary>
    [TestMethod]
    public void FloatingPointSpecials_MergeExactly()
    {
        Expression group = Add(Q(X, W), Add(Q(X, W), Y));
        Expression simplified = S(group);
        Assert.AreEqual("y + (2 * (x / w))", Text(simplified));
        Delegate before = Expression.Lambda(group, Variables).Compile(), after = Expression.Lambda(simplified, Variables).Compile();
        double[] specials = [0, -0.0, 1, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.Epsilon];
        foreach (double a in specials)
        {
            foreach (double b in specials)
            {
                // y = 0 isolates the merge t + t = 2 * t from the reassociation with y (the S3 latitude).
                double expected = (double)before.DynamicInvoke(a, 0.0, 0.0, b)!, actual = (double)after.DynamicInvoke(a, 0.0, 0.0, b)!;
                Assert.IsTrue(BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual) || double.IsNaN(expected) && double.IsNaN(actual), $"{a}, {b}: {expected} against {actual}");
            }
        }

        foreach (double special in new[] { double.NaN, double.PositiveInfinity, -0.0 })
        {
            Expression chain = Add(Add(Q(X, W), M(K(special), Y)), M(K(special), Q(Z, W)));
            Assert.AreEqual(Text(H(chain)), Text(S(chain)), special.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Observable calls keep their number, order, values and exceptions under every policy: <c>F()*x + F()*y</c>,
    /// <c>F()*x - F()*y</c>, <c>x/F() + y/F()</c>, <c>F()*(x+y)</c> and a throwing call inside a common factor, alone or behind a
    /// quotient. A call that is not inert is pinned by the canonicalization, so no pair or group holding it is ever merged.
    /// </summary>
    [TestMethod]
    public void ObservableCalls_KeepTheirEvaluations_UnderEveryPolicy()
    {
        MethodInfo f = typeof(Probe).GetMethod(nameof(Probe.F))!, g = typeof(Probe).GetMethod(nameof(Probe.G))!;
        Expression F() => Expression.Call(f);
        Expression G(Expression argument) => Expression.Call(g, argument);
        Expression quotient = Q(Z, W);
        Expression[] sources =
        [
            Add(M(F(), X), M(F(), Y)),
            Sub(M(F(), X), M(F(), Y)),
            Add(Q(X, F()), Q(Y, F())),
            M(F(), Add(X, Y)),
            Add(Add(quotient, M(F(), X)), M(F(), Y)),
            Add(Add(quotient, Q(X, F())), Q(X, F())),
            Add(Add(quotient, M(G(W), X)), M(X, G(W))),
            Add(Add(quotient, M(X, Add(G(Y), K(1)))), M(Z, Add(G(Y), K(1)))),
        ];
        ExpressionSimplifierOptions[] policies =
        [
            new ExpressionSimplifierOptions(),
            new ExpressionSimplifierOptions { TrustPolicy = ExpressionTrustPolicy.Verified },
            new ExpressionSimplifierOptions { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
            new ExpressionSimplifierOptions { AggressiveSimplification = true },
            new ExpressionSimplifierOptions { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
        ];
        double[][] samples = [[0.5, 1.5, 2.5, -1.25], [1, 2, 3, 0]];
        foreach (Expression source in sources)
        {
            Delegate compiled = Expression.Lambda(source, Variables).Compile();
            foreach (ExpressionSimplifierOptions options in policies)
            {
                Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
                Delegate target = Expression.Lambda(simplified, Variables).Compile();
                foreach (double[] sample in samples)
                {
                    object[] arguments = sample.Cast<object>().ToArray();
                    Probe.Log.Clear();
                    (object? Value, Type? Exception) expected = Evaluate(compiled, arguments);
                    string log = string.Join(",", Probe.Log);
                    Probe.Log.Clear();
                    (object? Value, Type? Exception) actual = Evaluate(target, arguments);
                    string message = $"{options.TrustPolicy}: {Text(source)} -> {Text(simplified)}";
                    Assert.AreEqual(expected.Exception, actual.Exception, message);
                    if (expected.Value is double e && actual.Value is double r) Assert.IsTrue(e.Equals(r) || Math.Abs(e - r) <= 1e-12 * Math.Max(Math.Abs(e), Math.Abs(r)), message);
                    else Assert.AreEqual(expected.Value, actual.Value, message);
                    Assert.AreEqual(log, string.Join(",", Probe.Log), $"{options.TrustPolicy}: {Text(source)} -> {Text(simplified)}");
                }
            }
        }
    }

    /// <summary>Evaluates a delegate and returns its value or the type of the exception it throws.</summary>
    /// <param name="function">The delegate.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The value, or the exception type.</returns>
    private static (object? Value, Type? Exception) Evaluate(Delegate function, object[] arguments)
    {
        try
        {
            return (function.DynamicInvoke(arguments), null);
        }
        catch (TargetInvocationException exception)
        {
            return (null, exception.InnerException!.GetType());
        }
    }

    /// <summary>
    /// A derived simplifier keeps the historical result, the switch gives it back, and the earlier finishings (M1 whole chains,
    /// M4 suffixes) keep their results.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifiers_Switches_AndEarlierFinishings()
    {
        Expression source = Add(Add(Q(Y, W), M(K(-2), X)), M(X, Add(K(2), Z)));
        Assert.AreEqual("(y / w) + (z * x)", Text(S(source)));
        Assert.AreNotEqual(Text(S(source)), Text(new DerivedSimplifier().Simplify(source)));
        Assert.AreEqual("(y / w) + ((-2 * x) + (x * (2 + z)))", Text(H(source)));

        ExpressionSimplifier.RefusedSuffixFinishingDisabled = true;
        try
        {
            Assert.AreEqual(Text(H(source)), Text(S(source)), "The suffix switch stops every later finishing.");
        }
        finally
        {
            ExpressionSimplifier.RefusedSuffixFinishingDisabled = false;
        }

        Assert.AreEqual("-8 + (0 - (z * (1 + x)))", Text(S(Sub(Sub(Sub(K(2), K(10)), Z), M(X, Z)))));
        Assert.AreEqual("(0 - (x / (y / 0))) + (2 * ((0 - y) + z))", Text(S(Sub(M(K(-2), Q(Y, K(1))), Add(Q(X, Q(Y, K(0))), M(K(-1), M(K(2), Z)))))));
    }

    /// <summary>
    /// The comparer identifies the historical result with the finished one, with the same hash, and a simplification it makes for a
    /// factor probe is not finished (its result keeps the historical decisions of the binary rules): diff#3627 keeps its historical
    /// result, as on <c>master</c>.
    /// </summary>
    [TestMethod]
    public void Comparer_IdentifiesTheHistoricalAndTheFinishedResults()
    {
        Expression source = Add(Add(Q(X, W), Y), Q(X, W));
        ExpressionComparer comparer = ExpressionComparer.Default;
        Assert.IsTrue(comparer.Equals(H(source), S(source)));
        Assert.AreEqual(comparer.GetHashCode(H(source)), comparer.GetHashCode(S(source)));

        // diff#3627 (float): a coefficient sum equal to 1 is simplified by a probe; finishing it there let the binary rule factor
        // the whole chain by it, (z + (-1 + (y + (y / -0)))) * (...), a form no call of master builds. Its sum stays historical.
        ParameterExpression x = Expression.Parameter(typeof(float), "x"), y = Expression.Parameter(typeof(float), "y"), z = Expression.Parameter(typeof(float), "z");
        Expression one = Expression.Constant(1f), two = Expression.Constant(2f), minusTwo = Expression.Constant(-2f), zero = Expression.Constant(0f);
        Expression diff3627 = Expression.Add(
            Expression.Add(
                Expression.Multiply(Expression.Subtract(z, Expression.Multiply(x, z)), z),
                Expression.Multiply(Expression.Subtract(one, Expression.Subtract(z, Expression.Multiply(x, z))), z)),
            Expression.Add(
                Expression.Divide(y, Expression.Multiply(Expression.Divide(two, Expression.Divide(one, two)), Expression.Multiply(zero, minusTwo))),
                Expression.Add(Expression.Multiply(minusTwo, Expression.Add(y, Expression.Constant(-1f))), Expression.Multiply(Expression.Subtract(one, minusTwo), Expression.Add(y, Expression.Constant(-1f))))));
        Assert.AreEqual(Text(H(diff3627)), Text(S(diff3627)));
        Assert.AreEqual("-1f + (y + ((y / -0f) + (z * ((z * (1f + (0f - x))) + (1f + (z * (-1f + x)))))))", Text(S(diff3627)));
    }
}
