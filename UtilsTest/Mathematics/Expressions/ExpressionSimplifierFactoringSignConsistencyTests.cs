using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The consistency of the finalization and of the orientation of factorings (mission K/N/F,
/// <c>docs/expressions/SimplifierFlatten.md</c> §26): K, the finishing of a small factored coefficient of added terms; N, the
/// orientation of a negated sum; F, the orientation of an extracted numeric factor and of a factored product of a mixed sum.
/// </summary>
/// <remarks>
/// Every case checks the first call, the structural idempotence (raw comparison, never a hash), the values (identical or within
/// the S3 latitude, <see cref="ExpressionSimplifierSignEquivarianceTests.CompareValues"/>) and, where the case is a family of
/// associations, that every association gives one structure, with free parameters and with parameters bound by a lambda.
/// </remarks>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierFactoringSignConsistencyTests
{
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");
    private static readonly ParameterExpression E = Expression.Parameter(typeof(double), "e");
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");
    private static readonly ParameterExpression[] Parameters = [A, B, C, D, E, X, Y, Z];

    /// <summary>Simplifies with a new built-in simplifier.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The result.</returns>
    private static Expression S(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Formats with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>A <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression K(double value) => Expression.Constant(value);

    /// <summary>An ordinary addition.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The node.</returns>
    private static Expression Add(Expression x, Expression y) => Expression.Add(x, y);

    /// <summary>An ordinary subtraction.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The node.</returns>
    private static Expression Sub(Expression x, Expression y) => Expression.Subtract(x, y);

    /// <summary>An ordinary multiplication.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The node.</returns>
    private static Expression Mul(Expression x, Expression y) => Expression.Multiply(x, y);

    /// <summary>An ordinary negation.</summary>
    /// <param name="x">The operand.</param>
    /// <returns>The node.</returns>
    private static Expression Neg(Expression x) => Expression.Negate(x);

    /// <summary>
    /// Asserts the first call (<paramref name="expected"/>, free parameters), its idempotence and its values, for free parameters
    /// and for parameters bound by a lambda.
    /// </summary>
    /// <param name="source">The source over <see cref="Parameters"/>.</param>
    /// <param name="expected">The expected text of the free result.</param>
    private static void AssertOneCall(Expression source, string expected)
    {
        Expression once = S(source);
        Assert.AreEqual(expected, Text(once), Text(source));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), $"Not a fixed point: {Text(source)} -> {Text(once)}");
        AssertSameValues(source, once);
        Expression bound = S(Expression.Lambda(source, Parameters));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(bound, S(bound)), $"Not a fixed point (bound): {Text(source)} -> {Text(bound)}");
    }

    /// <summary>
    /// Asserts that every source gives one structure in one call, a fixed point that computes the source's values, free and bound.
    /// </summary>
    /// <param name="expected">The expected text of the free result, or <see langword="null"/>.</param>
    /// <param name="sources">The sources (same leaves, same net signs).</param>
    private static void AssertOneStructure(string? expected, params Expression[] sources)
    {
        foreach (bool bound in new[] { false, true })
        {
            Expression? first = null;
            foreach (Expression body in sources)
            {
                Expression source = bound ? Expression.Lambda(body, Parameters) : body;
                Expression once = S(source);
                Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), $"Not a fixed point: {Text(source)} -> {Text(once)}");
                AssertSameValues(body, bound ? ((LambdaExpression)once).Body : once);
                if (first is null)
                {
                    first = once;
                    if (expected is not null && !bound) Assert.AreEqual(expected, Text(once), Text(source));
                }
                else
                {
                    Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, once), $"{Text(sources[0])} -> {Text(first)}, but {Text(source)} -> {Text(once)}");
                }
            }
        }
    }

    /// <summary>Asserts that two bodies over <see cref="Parameters"/> compute the same values, or values within the S3 latitude.</summary>
    /// <param name="source">The source body.</param>
    /// <param name="result">The simplified body.</param>
    private static void AssertSameValues(Expression source, Expression result)
    {
        Delegate before = Expression.Lambda(source, Parameters).Compile();
        Delegate after = Expression.Lambda(result, Parameters).Compile();
        double[][] samples = [[0.5, 1.5, 2.5, -1.25, 4, 3, -7, 0.25], [3, -2, 0.75, 2, -0.5, -1, 5, 9], [0, -0.0, 1e300, -1e-300, 2, 0, 1, -1]];
        foreach (double[] sample in samples)
        {
            object[] arguments = sample.Cast<object>().ToArray();
            double p = (double)before.DynamicInvoke(arguments)!, q = (double)after.DynamicInvoke(arguments)!;
            Assert.AreNotEqual(ExpressionSimplifierSignEquivarianceTests.ValueAgreement.Different, ExpressionSimplifierSignEquivarianceTests.CompareValues(p, q, 1e-9), $"{Text(source)} -> {Text(result)}: {p} against {q}");
        }
    }

    /// <summary>Runs <paramref name="action"/> with every switch of the mission set as given, restoring them afterwards.</summary>
    /// <param name="k">Whether K is off.</param>
    /// <param name="n">Whether N is off.</param>
    /// <param name="f">Whether F is off.</param>
    /// <param name="action">The action.</param>
    private static void WithSwitches(bool k, bool n, bool f, Action action)
    {
        try
        {
            ExpressionSimplifier.FactoredCoefficientFinishingDisabled = k;
            ExpressionSimplifier.NegatedSumOrientationDisabled = n;
            ExpressionSimplifier.FactorOrientationDisabled = f;
            action();
        }
        finally
        {
            ExpressionSimplifier.FactoringSignConsistencyDisabled = false;
        }
    }

    /// <summary>The <c>dagfactor</c> family: <c>a1*s + a2*s + … + an*s</c> with one shared sum <c>s = u + v</c>, left-associated.</summary>
    /// <param name="n">The number of terms.</param>
    /// <returns>The chain and its parameters.</returns>
    private static (Expression Chain, ParameterExpression[] Parameters) DagFactor(int n)
    {
        ParameterExpression u = Expression.Parameter(typeof(double), "u"), v = Expression.Parameter(typeof(double), "v");
        Expression shared = Add(u, v);
        ParameterExpression[] coefficients = Enumerable.Range(1, n).Select(i => Expression.Parameter(typeof(double), "a" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        Expression chain = Mul(coefficients[0], shared);
        for (int i = 1; i < n; i++) chain = Add(chain, Mul(coefficients[i], shared));
        return (chain, [u, v, .. coefficients]);
    }

    // ---------------------------------------------------------------- K

    /// <summary>
    /// K: a coefficient of added terms that grows by one term per binary factoring is canonicalized up to
    /// <c>FinishedCoefficientTerms</c> terms, so the first call is the fixed point (it needed a second call on <c>master</c>).
    /// </summary>
    [TestMethod]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    public void GrowingCoefficient_IsFinishedInOneCall(int n)
    {
        (Expression chain, ParameterExpression[] parameters) = DagFactor(n);
        Expression source = Expression.Lambda(chain, parameters);
        Expression once = S(source);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), Text(once));
        string body = Text(((LambdaExpression)once).Body);
        Assert.IsTrue(body.StartsWith("(u + v) * (a001 + (a002 + ", StringComparison.Ordinal), body);
    }

    /// <summary>
    /// K: past the bound the historical node is kept (the work per factoring stays constant), so a longer coefficient still needs
    /// the second call, as on <c>master</c>; the second call is the fixed point.
    /// </summary>
    [TestMethod]
    public void GrowingCoefficient_PastTheBound_KeepsTheHistoricalNode()
    {
        (Expression chain, ParameterExpression[] parameters) = DagFactor(24);
        Expression source = Expression.Lambda(chain, parameters);
        Expression once = S(source);
        Expression twice = S(once);
        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(once, twice), "the bound no longer applies");
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(twice, S(twice)), Text(twice));
    }

    /// <summary>
    /// K: the work the finishing adds is bounded per factoring: the minimal structural budget of the <c>dagfactor</c> family grows
    /// by at most a constant per term against the engine without K.
    /// </summary>
    [TestMethod]
    public void GrowingCoefficient_AddsBoundedWork()
    {
        long Minimal(Expression source)
        {
            long low = 1, high = 1L << 26;
            while (low + 1 < high)
            {
                long middle = (low + high) / 2;
                try
                {
                    new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = middle }).Simplify(source);
                    high = middle;
                }
                catch (ExpressionResourceLimitException)
                {
                    low = middle;
                }
            }

            return high;
        }

        foreach (int n in new[] { 32, 64 })
        {
            Expression source = Expression.Lambda(DagFactor(n).Chain, DagFactor(n).Parameters);
            long with = Minimal(source);
            long without = 0;
            WithSwitches(true, false, false, () => without = Minimal(source));
            Assert.IsTrue(with - without <= 64L * ExpressionSimplifier.FinishedCoefficientTerms * 16, $"n = {n}: {with} against {without}");
        }
    }

    /// <summary>
    /// K: the factored coefficient of the <c>bfsumcoef</c> family merges its repeated monomial in the first call
    /// (<c>2*(b + z) + 2*(c + z)</c> gave <c>2 * ((b + z) + (c + z))</c>, then <c>2 * (b + (c + (2 * z)))</c>).
    /// </summary>
    [TestMethod]
    public void RepeatedMonomialsOfTheCoefficient_AreMerged()
    {
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        Expression source = Add(Add(Add(Expression.Divide(A, q), Mul(K(2), Add(B, Z))), Mul(K(2), Add(C, Z))), Mul(K(2), Add(D, Z)));
        Expression once = S(source);
        Assert.AreEqual("(a / q) + (2 * (b + (c + (d + (3 * z)))))", Text(once));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)));
        Assert.AreEqual("2 * (b + (c + (2 * z)))", Text(S(Add(Mul(K(2), Add(B, Z)), Mul(K(2), Add(C, Z))))));
    }

    /// <summary>
    /// K: the gen#643 mechanism on <c>master</c> (the coefficient of a quotient and a canonical sum of added terms, left unsorted):
    /// the first call is the fixed point.
    /// </summary>
    [TestMethod]
    public void QuotientAndCanonicalSum_AreOrderedInOneCall()
    {
        ParameterExpression w = Expression.Parameter(typeof(double), "w");
        Expression source = Add(Mul(Expression.Divide(Y, Y), w), Mul(Add(Y, Add(Mul(B, Z), Mul(K(11), X))), w));
        Expression once = S(source);
        Assert.AreEqual("w * (y + ((y / y) + ((11 * x) + (b * z))))", Text(once));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)));
    }

    /// <summary>
    /// K: a coefficient holding a term that is not atomic (a product of a sum, a call) keeps the historical node: its
    /// canonicalization is not what the next call does (diff#3627).
    /// </summary>
    [TestMethod]
    public void NonAtomicCoefficients_KeepTheHistoricalNode()
    {
        ParameterExpression w = Expression.Parameter(typeof(double), "w");
        Expression product = Mul(Z, Sub(K(1), X));
        Expression source = Add(Mul(product, w), Mul(Add(K(1), Mul(Z, Sub(X, K(1)))), w));
        string branch = Text(S(source));
        string reference = "";
        WithSwitches(true, false, false, () => reference = Text(S(source)));
        Assert.AreEqual(reference, branch);
    }

    // ---------------------------------------------------------------- N

    /// <summary>
    /// N: the negated-sum residue of <c>master</c> (a chain starting with a negated leaf minus a group holding the other terms):
    /// every association gives the flat sum.
    /// </summary>
    [TestMethod]
    public void NegatedLeafMinusGroup_GivesTheFlatSum()
    {
        Expression q = Expression.Divide(B, E);
        AssertOneStructure(
            "d + ((0 - (b / e)) + (0 - (-2 * c)))",
            Sub(Neg(q), Sub(Mul(K(-2), C), D)),
            Add(Sub(Neg(q), Mul(K(-2), C)), D),
            Sub(D, Add(q, Mul(K(-2), C))),
            Add(D, Sub(Neg(q), Mul(K(-2), C))));
    }

    /// <summary>N: the negation of a sum holding a subtracted term is the flat sum of the opposite terms; other negations stay.</summary>
    [TestMethod]
    public void Negations_AreOrientedByTheTermsTheyNegate()
    {
        AssertOneCall(Neg(Sub(X, Y)), "(0 - x) + y");
        AssertOneCall(Neg(Add(X, Sub(K(0), Y))), "(0 - x) + y");
        AssertOneCall(Mul(K(-1), Sub(X, Y)), "(0 - x) + y");
        AssertOneCall(Mul(Sub(X, Y), K(-1)), "(0 - x) + y");
        AssertOneCall(Neg(Add(X, Y)), "-(x + y)");
        AssertOneCall(Neg(Add(K(1), X)), "-(1 + x)");
        AssertOneCall(Sub(Neg(Expression.Divide(B, E)), Add(D, A)), "-(d + (a + (b / e)))");
    }

    /// <summary>N: a negated sum holding an observable call or a <c>NaN</c> term, or of <see cref="decimal"/>, keeps the result of master.</summary>
    [TestMethod]
    public void NegatedSums_ThatAreNotInert_KeepMaster()
    {
        var calls = new List<string>();
        Func<double> f = () => { calls.Add("f"); return 2; };
        Expression call = Expression.Invoke(Expression.Constant(f));
        Expression[] sources =
        [
            Neg(Sub(call, X)),
            Neg(Sub(K(double.NaN), X)),
            Expression.Negate(Expression.Subtract(Expression.Parameter(typeof(decimal), "m"), Expression.Constant(2m))),
        ];
        foreach (Expression source in sources)
        {
            string branch = Text(S(source));
            string reference = "";
            WithSwitches(false, true, false, () => reference = Text(S(source)));
            Assert.AreEqual(reference, branch, Text(source));
        }
    }

    // ---------------------------------------------------------------- F

    /// <summary>F: the five signed pairs of the mission and their external negations, one orientation each.</summary>
    [TestMethod]
    public void SignedPairs_HaveOneOrientation()
    {
        AssertOneCall(Add(Mul(K(2), X), Mul(K(2), Y)), "2 * (x + y)");
        AssertOneCall(Sub(Mul(K(-2), X), Mul(K(2), Y)), "-(2 * (x + y))");
        AssertOneCall(Add(Mul(K(-2), X), Mul(K(-2), Y)), "-(2 * (x + y))");
        AssertOneCall(Add(Mul(K(-2), X), Mul(K(2), Y)), "2 * ((0 - x) + y)");
        AssertOneCall(Sub(Mul(K(-2), X), Mul(K(-2), Y)), "2 * ((0 - x) + y)");
        AssertOneCall(Sub(Mul(K(2), X), Mul(K(2), Y)), "2 * ((0 - y) + x)");
        AssertOneCall(Neg(Sub(Mul(K(2), X), Mul(K(2), Y))), "2 * ((0 - x) + y)");
        AssertOneCall(Neg(Sub(Mul(K(-2), X), Mul(K(2), Y))), "2 * (x + y)");
        AssertOneCall(Add(Mul(K(-2), Add(X, Y)), Mul(K(-2), Z)), "-(2 * (x + (y + z)))");
    }

    /// <summary>F: three terms and more, every association: one structure.</summary>
    [TestMethod]
    public void SignedChains_HaveOneStructure()
    {
        AssertOneStructure(
            "2 * ((0 - x) + (y + z))",
            Add(Add(Mul(K(-2), X), Mul(K(2), Y)), Mul(K(2), Z)),
            Add(Mul(K(-2), X), Add(Mul(K(2), Y), Mul(K(2), Z))),
            Sub(Mul(K(2), Z), Sub(Mul(K(2), X), Mul(K(2), Y))),
            Sub(Add(Mul(K(2), Y), Mul(K(2), Z)), Mul(K(2), X)),
            Sub(Mul(K(2), Y), Sub(Mul(K(-2), Z), Mul(K(-2), X))));
    }

    /// <summary>
    /// F: a factored product of a mixed sum carries its sign in the sum, as a term and alone, so the binary and the global
    /// factorings agree (the factoring-sign residue of <c>master</c>, §25.6).
    /// </summary>
    [TestMethod]
    public void FactoredProductsOfMixedSums_CarryTheirSignInTheSum()
    {
        Expression q = Expression.Divide(B, E);
        AssertOneStructure(
            "a + ((b / e) + (2 * ((0 - c) + d)))",
            Sub(A, Sub(Add(Mul(C, K(2)), Mul(K(-2), D)), q)),
            Add(Add(Sub(A, Mul(K(2), C)), Mul(K(2), D)), q),
            Sub(Add(A, q), Add(Mul(C, K(2)), Mul(K(-2), D))),
            Add(Add(A, q), Add(Mul(K(-2), C), Mul(K(2), D))));
        AssertOneCall(Neg(Mul(K(2), Sub(C, D))), "2 * ((0 - c) + d)");
        AssertOneCall(Sub(Expression.Divide(A, B), Mul(X, Sub(C, D))), "(a / b) + (x * ((0 - c) + d))");
        AssertOneCall(Sub(Expression.Divide(A, B), Sub(Mul(K(-2), X), Mul(K(-2), Y))), "(a / b) + (2 * ((0 - y) + x))");
    }

    /// <summary>
    /// F: a subtracted factored product whose identical term is added in the same sum is left to the cancellation (a rebuilt
    /// product would no longer cancel its partner: <c>q - t + t - t …</c> kept every occurrence), and the oriented occurrences
    /// merge with each other; the work stays linear in the number of occurrences (the <c>knfsharedsum</c> family).
    /// </summary>
    [TestMethod]
    public void SharedFactoredProducts_CancelAndMerge()
    {
        Expression t = Mul(K(2), Sub(X, Y));
        Expression q = Expression.Divide(A, B);
        Expression source = q;
        for (int i = 1; i < 64; i++) source = i % 2 == 1 ? Sub(source, t) : Add(source, t);
        AssertOneCall(source, "(a / b) + (2 * ((0 - x) + y))");
        AssertOneCall(Sub(Sub(q, t), t), "(a / b) + (4 * ((0 - x) + y))");
        AssertOneCall(Add(Sub(q, t), Mul(K(2), Sub(Y, X))), "(a / b) + (4 * ((0 - x) + y))");
        AssertOneCall(Add(Sub(q, t), Mul(K(-2), Sub(Y, X))), "a / b");
    }

    /// <summary>
    /// F: the coefficients outside the domain of the magnitude keep the result of master: <see cref="short"/>, unsigned, the
    /// minimal integers, and the non-factorable constants.
    /// </summary>
    [TestMethod]
    public void CoefficientsOutsideTheMagnitudeDomain_KeepMaster()
    {
        var sources = new List<Expression>();
        foreach ((Type type, object minus, object plus) in new (Type, object, object)[]
        {
            (typeof(short), (short)-2, (short)2),
            (typeof(int), int.MinValue, int.MinValue),
            (typeof(long), long.MinValue, long.MinValue),
            (typeof(uint), 2u, 2u),
        })
        {
            ParameterExpression x = Expression.Parameter(type, "x"), y = Expression.Parameter(type, "y");
            sources.Add(Expression.Subtract(Expression.Multiply(Expression.Constant(minus, type), x), Expression.Multiply(Expression.Constant(plus, type), y)));
            sources.Add(Expression.Add(Expression.Multiply(Expression.Constant(minus, type), x), Expression.Multiply(Expression.Constant(minus, type), y)));
        }

        foreach (double special in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, -0.0, 0.0, -1.0, 1.0 })
        {
            sources.Add(Sub(Mul(K(special), X), Mul(K(special), Y)));
            sources.Add(Add(Mul(K(special), X), Mul(K(special), Y)));
        }

        foreach (Expression source in sources)
        {
            Expression branch = S(source);
            Expression reference = null!;
            WithSwitches(false, false, true, () => reference = S(source));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(reference, branch), $"{Text(source)}: {Text(reference)} against {Text(branch)}");
        }
    }

    /// <summary>
    /// F: <c>int.MinValue + 1</c> is negatable, so a pair of it is oriented, exactly (unchecked integers form a ring): the values are
    /// identical on every sample, wrap included.
    /// </summary>
    [TestMethod]
    public void NegatableIntegralCoefficients_AreOrientedExactly()
    {
        ParameterExpression x = Expression.Parameter(typeof(int), "x"), y = Expression.Parameter(typeof(int), "y");
        foreach (int coefficient in new[] { -3, int.MinValue + 1 })
        {
            Expression source = Expression.Subtract(Expression.Multiply(Expression.Constant(coefficient), x), Expression.Multiply(Expression.Constant(coefficient), y));
            Expression once = S(source);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), Text(once));
            Func<int, int, int> before = Expression.Lambda<Func<int, int, int>>(source, x, y).Compile();
            Func<int, int, int> after = Expression.Lambda<Func<int, int, int>>(once, x, y).Compile();
            foreach (int a in new[] { 0, 1, -1, 7, int.MaxValue, int.MinValue, 123456789 })
            {
                foreach (int b in new[] { 0, 2, -5, int.MaxValue, int.MinValue })
                {
                    Assert.AreEqual(before(a, b), after(a, b), $"{Text(source)} -> {Text(once)} at {a}, {b}");
                }
            }
        }
    }

    /// <summary>
    /// F: subnormal and extreme finite floating-point coefficients are oriented exactly. The factoring itself keeps the latitude it
    /// had on master (a subnormal product rounds), but the orientation only negates the factor and the residue sum, which IEEE-754
    /// does bit for bit (<c>(-c) * (x - y)</c> and <c>c * (y - x)</c>): the oriented result has the bits of master's result on every
    /// sample, zeros apart (their sign is the S3 latitude).
    /// </summary>
    [TestMethod]
    [DataRow(-double.Epsilon)]
    [DataRow(-double.MaxValue)]
    [DataRow(-1e300)]
    [DataRow(-2.5)]
    public void ExtremeFloatingPointCoefficients_AreOrientedExactly(double coefficient)
    {
        Expression source = Sub(Mul(K(coefficient), X), Mul(K(coefficient), Y));
        Expression once = S(source);
        Assert.AreEqual(Text(K(-coefficient)) + " * ((0 - x) + y)", Text(once));
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), Text(once));
        Expression reference = null!;
        WithSwitches(false, false, true, () => reference = S(source));
        Func<double, double, double> master = Expression.Lambda<Func<double, double, double>>(reference, X, Y).Compile();
        Func<double, double, double> after = Expression.Lambda<Func<double, double, double>>(once, X, Y).Compile();
        foreach (double x in new[] { 1.0, -3.0, 0.5, 1e-10, 0.0, double.NaN, double.PositiveInfinity })
        {
            foreach (double y in new[] { 2.0, 7.0, -0.25, 1.0 })
            {
                double expected = master(x, y), actual = after(x, y);
                if (expected == 0 && actual == 0) continue;
                if (double.IsNaN(expected) && double.IsNaN(actual)) continue;
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual), $"{x}, {y}: {expected} against {actual}");
            }
        }
    }

    // ---------------------------------------------------------------- Switches and isolation

    /// <summary>
    /// Each switch restores the result of master for its mechanism, and only for it (so a mutation of one decision is detected
    /// by the tests above).
    /// </summary>
    [TestMethod]
    public void Switches_RestoreMasterPerMechanism()
    {
        (Expression chain, ParameterExpression[] parameters) = DagFactor(4);
        Expression k = Expression.Lambda(chain, parameters);
        Expression n = Sub(Neg(Expression.Divide(B, E)), Sub(Mul(K(-2), C), D));
        Expression f = Sub(Mul(K(-2), X), Mul(K(-2), Y));
        WithSwitches(true, false, false, () =>
        {
            Assert.AreEqual("(u, v, a001, a002, a003, a004) => (u + v) * (((a001 + a002) + a003) + a004)", Text(S(k)));
            Assert.AreEqual("d + ((0 - (b / e)) + (0 - (-2 * c)))", Text(S(n)));
            Assert.AreEqual("2 * ((0 - x) + y)", Text(S(f)));
        });
        WithSwitches(false, true, false, () =>
        {
            Assert.AreEqual("-((0 - d) + ((b / e) + (-2 * c)))", Text(S(n)));
            Assert.AreEqual("2 * ((0 - x) + y)", Text(S(f)));
        });
        WithSwitches(false, false, true, () => Assert.AreEqual("-2 * ((0 - y) + x)", Text(S(f))));
        Assert.AreEqual("2 * ((0 - x) + y)", Text(S(f)));
    }

    /// <summary>
    /// The switches are per thread: concurrent operations on other threads, each with its own switches, keep their own results,
    /// and an operation nested in a comparison keeps the switches of its thread.
    /// </summary>
    [TestMethod]
    public void Switches_AreIsolatedPerThread()
    {
        Expression f = Sub(Mul(K(-2), X), Mul(K(-2), Y));
        string[] results = new string[16];
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, index =>
        {
            bool off = index % 2 == 0;
            ExpressionSimplifier.FactorOrientationDisabled = off;
            try
            {
                for (int round = 0; round < 20; round++)
                {
                    results[index] = Text(S(f));
                    Assert.IsTrue(ExpressionComparer.Default.Equals(f, Sub(Mul(K(-2), X), Mul(K(-2), Y))));
                }
            }
            finally
            {
                ExpressionSimplifier.FactorOrientationDisabled = false;
            }
        });

        for (int index = 0; index < results.Length; index++)
        {
            Assert.AreEqual(index % 2 == 0 ? "-2 * ((0 - y) + x)" : "2 * ((0 - x) + y)", results[index], index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
