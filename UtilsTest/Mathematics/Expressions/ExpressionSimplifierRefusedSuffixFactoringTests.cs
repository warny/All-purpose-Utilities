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
/// Mission M4 (<c>docs/expressions/SimplifierFlatten.md</c> §22): a refused additive chain whose canonical result ends with a
/// factorable suffix (the quotients sort first) has that suffix factored at its boundary, as the next call factored it on
/// <c>master</c> <c>48ae9be3</c>, while the refused terms, the observable calls and every chain outside the factorable family keep
/// their historical result. Each new result is the second call of the reference (the suffix finishing switched off) and a fixed
/// point.
/// </summary>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierRefusedSuffixFactoringTests
{
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");
    private static readonly ParameterExpression W = Expression.Parameter(typeof(double), "w");
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");
    private static readonly ParameterExpression[] Variables = [X, Y, Z, W, A, B, C];

    /// <summary>A derived simplifier without any member: it keeps the historical treatment.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Counts its calls, so that an evaluation count and order can be compared.</summary>
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

        /// <summary>An observable call: logs itself and returns 5.</summary>
        /// <returns>5.</returns>
        public static double G()
        {
            Log.Add("G");
            return 5;
        }
    }

    /// <summary>Simplifies with a new built-in simplifier.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression S(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Simplifies with the suffix finishing switched off: the result of <c>master</c> <c>48ae9be3</c> (reference H).</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression H(Expression expression)
    {
        ExpressionSimplifier.RefusedSuffixFinishingDisabled = true;
        try
        {
            return S(expression);
        }
        finally
        {
            ExpressionSimplifier.RefusedSuffixFinishingDisabled = false;
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

    /// <summary>
    /// Asserts that one call gives <paramref name="expected"/>, the second call of the reference (H, then H again), that it is a
    /// structural fixed point, and that it evaluates like the source.
    /// </summary>
    /// <param name="source">The source over <see cref="Variables"/>.</param>
    /// <param name="expected">The expected text of the first result.</param>
    private static void AssertSecondCallInOne(Expression source, string expected)
    {
        Expression once = S(source);
        Assert.AreEqual(expected, Text(once), Text(source));
        Expression reference = H(source);
        Assert.AreNotEqual(expected, Text(reference), "The reference needs a second call: " + Text(reference));
        Assert.AreEqual(expected, Text(H(reference)), "The result is the reference's second call.");
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), $"Not a fixed point: {Text(once)} -> {Text(S(once))}");
        AssertSameValues(source, once);
    }

    /// <summary>
    /// Asserts that one call gives <paramref name="expected"/>, the fixed point the reference reaches in at most two calls, and
    /// that it is a fixed point evaluating like the source.
    /// </summary>
    /// <param name="source">The source over <see cref="Variables"/>.</param>
    /// <param name="expected">The expected text of the first result.</param>
    private static void AssertReferenceFixedPointInOne(Expression source, string expected)
    {
        Expression once = S(source);
        Assert.AreEqual(expected, Text(once), Text(source));
        Assert.AreEqual(expected, Text(H(H(source))), "The reference's second call.");
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), $"Not a fixed point: {Text(once)} -> {Text(S(once))}");
        AssertSameValues(source, once);
    }

    /// <summary>Asserts that one call gives the reference (nothing to finish) and a fixed point.</summary>
    /// <param name="source">The source over <see cref="Variables"/>.</param>
    private static void AssertHistorical(Expression source)
    {
        Expression once = S(source);
        Assert.AreEqual(Text(H(source)), Text(once), Text(source));
        AssertSameValues(source, once);
    }

    /// <summary>
    /// Asserts that two trees over the variables evaluate alike on moderate samples (equal, or within a relative 1e-12 for the
    /// reassociation latitude of the symbolic contract), and bit for bit when <paramref name="exact"/>.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="simplified">The simplified tree.</param>
    /// <param name="exact">Whether the values must be identical bit for bit.</param>
    private static void AssertSameValues(Expression source, Expression simplified, bool exact = false)
    {
        Delegate before = Expression.Lambda(source, Variables).Compile();
        Delegate after = Expression.Lambda(simplified, Variables).Compile();
        double[][] samples = [[0.5, 1.5, 2.5, -1.25, 3, 0.75, -2], [1.25, -3, 0.75, 2, -0.5, 4, 1.5], [-2, 0.5, 1.5, 0.25, 2.5, -1, 3], [0, 0, 0, 0, 0, 0, 0]];
        foreach (double[] sample in samples)
        {
            object[] arguments = sample.Cast<object>().ToArray();
            double a = (double)before.DynamicInvoke(arguments)!, b = (double)after.DynamicInvoke(arguments)!;
            bool same = exact
                ? BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b) || (double.IsNaN(a) && double.IsNaN(b))
                : a.Equals(b) || Math.Abs(a - b) <= 1e-12 * Math.Max(Math.Abs(a), Math.Abs(b));
            Assert.IsTrue(same, $"{Text(source)} -> {Text(simplified)}: {a} against {b}");
        }
    }

    /// <summary>Builds the left-associated, right-associated and balanced sums of signed terms.</summary>
    /// <param name="terms">The signed terms, the first one added.</param>
    /// <returns>The three trees.</returns>
    private static Expression[] Associations(params (Expression Term, bool IsNegative)[] terms)
    {
        Expression left = terms[0].Term;
        for (int i = 1; i < terms.Length; i++) left = terms[i].IsNegative ? Expression.Subtract(left, terms[i].Term) : Expression.Add(left, terms[i].Term);

        Expression Signed((Expression Term, bool IsNegative) term) => term.IsNegative ? Expression.Negate(term.Term) : term.Term;
        Expression right = Signed(terms[^1]);
        for (int i = terms.Length - 2; i >= 0; i--) right = Expression.Add(Signed(terms[i]), right);

        Expression Balanced(int start, int count)
            => count == 1 ? Signed(terms[start]) : Expression.Add(Balanced(start, count / 2), Balanced(start + (count / 2), count - (count / 2)));

        return [left, right, Balanced(0, terms.Length)];
    }

    /// <summary>
    /// nested#1179 of the S5 campaign: <c>(-2 * (y / 1)) - ((x / (y / 0)) + (-1 * (2 * z)))</c>. The quotient refuses the chain,
    /// the levels give <c>(0 - (x / (y / 0))) + ((-2 * y) + (2 * z))</c>, whose suffix the next call admitted as a factorable chain;
    /// the quotient, its division by zero included, is kept as is and still evaluates to the same values.
    /// </summary>
    [TestMethod]
    public void Nested1179_FactorsItsSuffixInOneCall()
    {
        Expression source = Expression.Subtract(
            M(K(-2), Q(Y, K(1))),
            Expression.Add(Q(X, Q(Y, K(0))), M(K(-1), M(K(2), Z))));
        Assert.AreEqual("(0 - (x / (y / 0))) + ((-2 * y) + (2 * z))", Text(H(source)));
        AssertSecondCallInOne(source, "(0 - (x / (y / 0))) + (2 * ((0 - y) + z))");
    }

    /// <summary>
    /// The reduced form of nested#1179 in the stability campaign: every term of the levels' result is subtracted but the
    /// quotient; the next call factored the suffix and negated the whole sum.
    /// </summary>
    [TestMethod]
    public void Nested1179Reduced_NegatedSumOfQuotientAndSuffix()
    {
        Expression source = Expression.Subtract(M(K(-2), Y), Expression.Add(Q(Y, K(0)), M(K(2), Z)));
        Assert.AreEqual("(0 - (y / 0)) + ((-2 * y) + (0 - (2 * z)))", Text(H(source)));
        AssertSecondCallInOne(source, "-((y / 0) + (2 * (y + z)))");
    }

    /// <summary>
    /// The historical scenarios of the mission: a sub-chain that is factorable in the source keeps its historical factoring
    /// (strategy D' is not applied), and a factorable suffix that only the reconstruction forms is factored in the same call,
    /// whatever the association and whether the quotient comes first, in the middle or last in the source.
    /// </summary>
    [TestMethod]
    public void HistoricalScenarios_QuotientBeforeAfterAndInTheMiddle()
    {
        Expression ax = M(A, X), ay = M(A, Y), xb = Q(X, B), bc = Q(B, C);

        // Factorable in the source: already factored by the historical treatment.
        AssertHistorical(Expression.Add(Expression.Add(ax, ay), xb));
        AssertHistorical(Expression.Add(xb, Expression.Add(ax, ay)));
        AssertHistorical(Expression.Add(Expression.Add(ax, ay), bc));
        AssertHistorical(Expression.Subtract(Expression.Add(ax, ay), bc));
        Assert.AreEqual("(x / b) + (a * (x + y))", Text(S(Expression.Add(Expression.Add(ax, ay), xb))));

        // Factorable once reconstructed: the quotient sorts first, the products form the suffix. Every association gives one
        // tree, in one call; those whose source already holds the products in one sub-chain were factored historically.
        foreach (Expression source in Associations((ax, false), (xb, false), (ay, false)).Concat(Associations((xb, false), (ax, false), (ay, false))))
        {
            AssertReferenceFixedPointInOne(source, "(x / b) + (a * (x + y))");
        }

        AssertSecondCallInOne(Expression.Add(Expression.Add(ax, xb), ay), "(x / b) + (a * (x + y))");
        AssertSecondCallInOne(Expression.Add(ax, Expression.Add(xb, ay)), "(x / b) + (a * (x + y))");
        foreach (Expression source in Associations((ax, false), (bc, true), (ay, false)))
        {
            AssertReferenceFixedPointInOne(source, "(0 - (b / c)) + (a * (x + y))");
        }

        AssertSecondCallInOne(Expression.Subtract(Expression.Subtract(ax, bc), ay), "(0 - (b / c)) + (a * ((0 - y) + x))");
    }

    /// <summary>Several quotients separating factorable terms: the quotients sort first, the factorable suffix holds every product.</summary>
    [TestMethod]
    public void SeveralQuotients_FactorEveryCommonFactorOfTheSuffix()
    {
        Expression source = Expression.Add(Expression.Add(Expression.Add(Q(X, Y), M(K(2), A)), Q(Z, W)), M(K(2), B));
        AssertSecondCallInOne(source, "(x / y) + ((z / w) + (2 * (a + b)))");

        Expression two = Expression.Add(
            Expression.Add(Expression.Add(Expression.Add(Q(X, Y), M(K(2), A)), M(K(2), B)), Expression.Add(Q(Z, W), M(K(3), C))),
            M(K(3), X));
        AssertSecondCallInOne(two, "(x / y) + ((z / w) + ((2 * (a + b)) + (3 * (c + x))))");
    }

    /// <summary>
    /// Signed coefficients: opposite coefficients factor their magnitude (M3), subtracted products a negated sum, and a suffix
    /// whose terms cancel leaves the quotient alone (the levels already cancel it).
    /// </summary>
    [TestMethod]
    public void SignedCoefficients_FactorAsTheWholeChainFactoring()
    {
        AssertSecondCallInOne(Expression.Subtract(Expression.Add(Q(X, Y), M(K(2), A)), M(K(2), B)), "(x / y) + (2 * ((0 - b) + a))");
        AssertSecondCallInOne(Expression.Subtract(Expression.Subtract(Q(X, Y), M(K(2), A)), M(K(2), B)), "(x / y) + (0 - (2 * (a + b)))");
        AssertSecondCallInOne(Expression.Add(Expression.Add(Q(X, Y), M(K(-2), A)), M(K(2), B)), "(x / y) + (2 * ((0 - a) + b))");
        AssertHistorical(Expression.Subtract(Expression.Add(Q(X, Y), M(K(2), A)), M(K(2), A)));
    }

    /// <summary>
    /// A suffix without any common factor, a linear suffix and a suffix that is a single term are left as the levels built them:
    /// the search runs, nothing is factored and the prefix is never run again.
    /// </summary>
    [TestMethod]
    public void NoCommonFactor_KeepsTheHistoricalResult()
    {
        Expression[] sources =
        [
            Expression.Add(Expression.Add(Q(X, Y), M(A, B)), M(C, Z)),
            Expression.Add(Expression.Add(Q(X, Y), M(K(2), A)), M(K(3), B)),
            Expression.Add(Q(X, Y), M(K(2), A)),
            Expression.Add(Expression.Add(Q(X, Y), Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, A)), M(K(3), B)),
        ];
        foreach (Expression source in sources)
        {
            AssertHistorical(source);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(S(source), S(S(source))), Text(S(source)));
            Assert.AreEqual(0, Statistics(source).SuffixFactorings, Text(source));
        }
    }

    /// <summary>The boundary is searched once per refused chain and the suffix of nested#1179 is factored once, in one round.</summary>
    [TestMethod]
    public void SuffixFinishing_RunsOncePerBoundary()
    {
        Expression source = Expression.Add(Expression.Add(M(K(2), Y), Q(X, W)), M(K(2), Z));
        FlattenAdmissionStatistics statistics = Statistics(source);
        Assert.AreEqual(1, statistics.SuffixFactorings);
        Assert.AreEqual(1, statistics.SuffixPrefixLevels);
        Assert.IsTrue(statistics.SuffixSearches >= 1);
        Assert.IsTrue(statistics.SuffixTests <= statistics.SuffixSearches);
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
    /// The mechanisms of mission C3c keep their results: a chain factorable once folded (M1), a two-term coefficient (M2) and
    /// opposite coefficients (M3) are factored as before, and a fixed point stays a fixed point.
    /// </summary>
    [TestMethod]
    public void C3cMechanisms_AndFixedPoints_AreUnchanged()
    {
        Expression m1 = Expression.Subtract(Expression.Subtract(Expression.Subtract(K(2), K(10)), Z), M(X, Z));
        Assert.AreEqual("-8 + (0 - (z * (1 + x)))", Text(S(m1)));
        Assert.AreEqual("z * (1 + x)", Text(S(Expression.Add(M(Z, Expression.Add(X, K(0))), Z))));
        Assert.AreEqual("2 * ((0 - y) + x)", Text(S(Expression.Add(M(K(2), X), M(K(-2), Y)))));

        foreach (string text in new[] { "(0 - (x / (y / 0))) + (2 * ((0 - y) + z))", "(x / b) + (a * (x + y))" })
        {
            Expression fixedPoint = text.StartsWith("(0", StringComparison.Ordinal)
                ? Expression.Add(Expression.Subtract(K(0), Q(X, Q(Y, K(0)))), M(K(2), Expression.Add(Expression.Subtract(K(0), Y), Z)))
                : Expression.Add(Q(X, B), M(A, Expression.Add(X, Y)));
            Assert.AreEqual(text, Text(fixedPoint));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(fixedPoint, S(fixedPoint)), Text(S(fixedPoint)));
            Assert.AreEqual(0, Statistics(fixedPoint).SuffixFactorings);
        }
    }

    /// <summary>
    /// Observable calls keep their number and order of evaluations under every policy: a pinned term is placed after the inert
    /// groups, so it ends the spine and no suffix holding it or following it is factored; the inert products before it are only
    /// factored when they form a suffix of their own, which never crosses the call.
    /// </summary>
    [TestMethod]
    public void ObservableCalls_KeepTheirEvaluations_UnderEveryPolicy()
    {
        MethodInfo f = typeof(Probe).GetMethod(nameof(Probe.F))!, g = typeof(Probe).GetMethod(nameof(Probe.G))!;
        Expression[] sources =
        [
            Expression.Add(Expression.Add(Expression.Add(M(A, X), Expression.Call(f)), Q(X, B)), M(A, Y)),
            Expression.Add(Expression.Add(Expression.Add(Q(X, B), M(A, X)), M(Expression.Call(f), Y)), M(A, Y)),
            Expression.Add(Expression.Add(Expression.Call(g), Expression.Add(Q(X, B), M(A, X))), Expression.Add(Expression.Call(f), M(A, Y))),
        ];
        ExpressionSimplifierOptions[] policies =
        [
            new ExpressionSimplifierOptions(),
            new ExpressionSimplifierOptions { AggressiveSimplification = true },
            new ExpressionSimplifierOptions { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
            new ExpressionSimplifierOptions { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        ];
        double[] arguments = [0.5, 1.5, 2.5, -1.25, 3, 0.75, -2];
        foreach (Expression source in sources)
        {
            Delegate compiled = Expression.Lambda(source, Variables).Compile();
            Probe.Log.Clear();
            object expected = compiled.DynamicInvoke(arguments.Cast<object>().ToArray())!;
            string log = string.Join(",", Probe.Log);
            foreach (ExpressionSimplifierOptions options in policies)
            {
                Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
                Probe.Log.Clear();
                Assert.AreEqual(expected, Expression.Lambda(simplified, Variables).Compile().DynamicInvoke(arguments.Cast<object>().ToArray()), Text(simplified));
                Assert.AreEqual(log, string.Join(",", Probe.Log), Text(simplified));
            }
        }
    }

    /// <summary>
    /// Integral chains. A quotient by a parameter may throw (<see cref="DivideByZeroException"/>): it is pinned, so it ends the
    /// canonical chain and no suffix follows it; the historical result is kept, and the simplified tree throws the same exception
    /// as the source. A quotient by a constant is inert: the factoring of the suffix is then an exact modular identity, overflow
    /// included.
    /// </summary>
    [TestMethod]
    public void IntegralTypes_KeepTheDivisionByZero_AndFactorExactly()
    {
        foreach (Type type in new[] { typeof(int), typeof(long) })
        {
            ParameterExpression x = Expression.Parameter(type, "x"), y = Expression.Parameter(type, "y"), z = Expression.Parameter(type, "z");
            Expression Constant(long value) => Expression.Constant(Convert.ChangeType(value, type), type);
            Expression throwing = Expression.Add(Expression.Subtract(Expression.Multiply(Constant(2), x), Expression.Divide(x, y)), Expression.Multiply(Constant(2), z));
            Expression inert = Expression.Add(Expression.Subtract(Expression.Multiply(Constant(2), x), Expression.Divide(x, Constant(3))), Expression.Multiply(Constant(2), z));
            Assert.AreEqual(Text(H(throwing)), Text(new ExpressionSimplifier().Simplify(throwing)), type.Name);
            Assert.AreEqual(Text(H(H(inert))), Text(new ExpressionSimplifier().Simplify(inert)), type.Name);

            foreach (Expression source in new[] { throwing, inert })
            {
                Expression once = new ExpressionSimplifier().Simplify(source);
                Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, new ExpressionSimplifier().Simplify(once)), Text(once));
                Delegate before = Expression.Lambda(source, x, y, z).Compile(), after = Expression.Lambda(once, x, y, z).Compile();
                long[] values = [0, 1, -1, 7, -13, int.MaxValue, int.MinValue, long.MaxValue, long.MinValue];
                foreach (long a in values)
                {
                    foreach (long b in values)
                    {
                        object[] arguments = [Convert.ChangeType(unchecked(type == typeof(int) ? (int)a : a), type), Convert.ChangeType(unchecked(type == typeof(int) ? (int)b : b), type), Convert.ChangeType(unchecked(type == typeof(int) ? (int)(a ^ b) : a ^ b), type)];
                        (object? value, Type? exception) expected = Evaluate(before, arguments), actual = Evaluate(after, arguments);
                        Assert.AreEqual(expected, actual, $"{Text(source)} -> {Text(once)} on {string.Join(", ", arguments)}");
                    }
                }
            }

            Assert.AreEqual(typeof(DivideByZeroException), Evaluate(Expression.Lambda(new ExpressionSimplifier().Simplify(throwing), x, y, z).Compile(), [Convert.ChangeType(1, type), Convert.ChangeType(0, type), Convert.ChangeType(1, type)]).Exception);
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
    /// Floating-point chains: the factored suffix is bit for bit the source on moderate values; near the extremes of
    /// <see cref="float"/> (<c>3E+38</c>) the factoring is the reassociation latitude of the symbolic contract, the very form the
    /// next call gave on <c>master</c>; <c>NaN</c>, infinite and signed-zero coefficients are never factored.
    /// </summary>
    [TestMethod]
    public void FloatingPointTypes_FactorAsTheNextCall()
    {
        AssertSameValues(Expression.Add(Expression.Add(M(K(2), Y), Q(X, W)), M(K(2), Z)), S(Expression.Add(Expression.Add(M(K(2), Y), Q(X, W)), M(K(2), Z))), exact: true);

        ParameterExpression x = Expression.Parameter(typeof(float), "x"), y = Expression.Parameter(typeof(float), "y"), z = Expression.Parameter(typeof(float), "z");
        Expression source = Expression.Add(Expression.Subtract(Expression.Multiply(Expression.Constant(2f), x), Expression.Divide(x, y)), Expression.Multiply(Expression.Constant(2f), z));
        Expression once = new ExpressionSimplifier().Simplify(source);
        Assert.AreEqual("(0f - (x / y)) + (2f * (x + z))", Text(once));
        Assert.AreEqual(Text(H(H(source))), Text(once), "The next call's form, latitude included.");
        var compiled = (Func<float, float, float, float>)Expression.Lambda(once, x, y, z).Compile();
        var original = (Func<float, float, float, float>)Expression.Lambda(source, x, y, z).Compile();
        Assert.AreEqual(original(1.5f, 0.25f, -2.5f), compiled(1.5f, 0.25f, -2.5f));
        Assert.IsTrue(float.IsNaN(original(3E+38f, 1f, -3E+38f)), "2x and 2z overflow to opposite infinities in the source.");
        Assert.AreEqual(-3E+38f, compiled(3E+38f, 1f, -3E+38f), "x + z is exact before the doubling: the S3 latitude the next call took.");

        foreach (double special in new[] { double.NaN, double.PositiveInfinity, -0.0 })
        {
            Expression chain = Expression.Add(Expression.Add(Q(X, Y), M(K(special), A)), M(K(special), B));
            Assert.AreEqual(Text(H(chain)), Text(S(chain)), special.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Decimal chains are never canonicalized nor factored, and parameters bound by a lambda give the free-parameter tree.</summary>
    [TestMethod]
    public void DecimalChains_AndBoundParameters()
    {
        ParameterExpression x = Expression.Parameter(typeof(decimal), "x"), y = Expression.Parameter(typeof(decimal), "y"), z = Expression.Parameter(typeof(decimal), "z");
        Expression source = Expression.Add(Expression.Subtract(Expression.Multiply(Expression.Constant(2m), x), Expression.Divide(x, y)), Expression.Multiply(Expression.Constant(2m), z));
        Assert.AreEqual(Text(H(source)), Text(S(source)));

        Expression free = Expression.Add(Expression.Add(M(K(2), Y), Q(X, W)), M(K(2), Z));
        var bound = (LambdaExpression)S(Expression.Lambda(free, X, Y, Z, W));
        Assert.AreEqual(Text(S(free)), Text(bound.Body));
    }

    /// <summary>A derived simplifier and the switched-off families keep the historical result of a refused chain.</summary>
    [TestMethod]
    public void DerivedSimplifiers_AndSwitchedOffFamilies_KeepTheHistoricalResult()
    {
        Expression source = Expression.Add(Expression.Add(M(K(2), Y), Q(X, W)), M(K(2), Z));
        Assert.AreEqual("(x / w) + (2 * (y + z))", Text(S(source)));
        Assert.AreNotEqual(Text(S(source)), Text(new DerivedSimplifier().Simplify(source)));

        ExpressionSimplifier.RefusedChainFinishingDisabled = true;
        try
        {
            Assert.AreEqual(Text(H(source)), Text(S(source)));
        }
        finally
        {
            ExpressionSimplifier.RefusedChainFinishingDisabled = false;
        }

        ExpressionSimplifier.FactorableChainsDisabled = true;
        try
        {
            Assert.AreNotEqual("(x / w) + (2 * (y + z))", Text(S(source)));
        }
        finally
        {
            ExpressionSimplifier.FactorableChainsDisabled = false;
        }
    }

    /// <summary>
    /// The comparer identifies the historical result with the finished one (the canonical form of its next call), with the same
    /// hash.
    /// </summary>
    [TestMethod]
    public void Comparer_IdentifiesTheHistoricalAndTheFinishedResults()
    {
        Expression source = Expression.Add(Expression.Add(M(K(2), Y), Q(X, W)), M(K(2), Z));
        ExpressionComparer comparer = ExpressionComparer.Default;
        Assert.IsTrue(comparer.Equals(H(source), S(source)));
        Assert.AreEqual(comparer.GetHashCode(H(source)), comparer.GetHashCode(S(source)));
    }
}
