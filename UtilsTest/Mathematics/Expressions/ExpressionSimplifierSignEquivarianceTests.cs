using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Sign equivariance audit (<c>docs/expressions/SimplifierFlatten.md</c> §25): the invariants of <c>master</c> <c>f6ecb2a5</c>
/// that the partial sign normalization V1b + C1 breaks, pinned as permanent characterizations.
/// </summary>
/// <remarks>
/// <para>
/// The canonical sums of <c>master</c> keep the physical placement of the sign of a term (in its coefficient or in its additive
/// sign, §20.1), but the result only depends on the terms the canonicalization collects: a leaf keeps its physical form, and
/// its net additive sign is the product of the signs of the groups above it. Two trees with the same leaves and the same net
/// signs therefore give one structure, whatever their association, and a level canonicalized on its own then collected again
/// by its parent gives what the flat collection gives.
/// </para>
/// <para>
/// V1b rewrites <c>(c &lt; 0, subtracted)</c> into <c>(|c|, added)</c> inside a level. When the parent subtracts that level, the
/// term becomes <c>(|c|, subtracted)</c>, where the flat collection keeps <c>(c, added)</c>: both have the same value, but they
/// are the two competing forms of V1, so the association decides the structure. These tests fail under V1b (measured in the
/// variant checkout of §25) and must keep passing on any sign normalization proposed later.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierSignEquivarianceTests
{
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");
    private static readonly ParameterExpression E = Expression.Parameter(typeof(double), "e");
    private static readonly ParameterExpression[] Parameters = [A, B, C, D, E];

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

    /// <summary>
    /// Asserts that every source gives, in one call, the same structure as the first one (strict raw comparison), that this
    /// structure is a fixed point, and that every result computes its source's values; free parameters and parameters bound by
    /// a lambda alike.
    /// </summary>
    /// <param name="expected">The expected text of the free result, or <see langword="null"/>.</param>
    /// <param name="sources">The sources over <see cref="Parameters"/>.</param>
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

    /// <summary>
    /// Asserts that two bodies over <see cref="Parameters"/> compute the same values: identical, or within the S3 latitude
    /// (<see cref="CompareValues"/>), never outside it.
    /// </summary>
    /// <param name="source">The source body.</param>
    /// <param name="result">The simplified body.</param>
    private static void AssertSameValues(Expression source, Expression result)
    {
        Delegate before = Expression.Lambda(source, Parameters).Compile();
        Delegate after = Expression.Lambda(result, Parameters).Compile();
        double[][] samples = [[0.5, 1.5, 2.5, -1.25, 4], [3, -2, 0.75, 2, -0.5]];
        foreach (double[] sample in samples)
        {
            object[] arguments = sample.Cast<object>().ToArray();
            double p = (double)before.DynamicInvoke(arguments)!, q = (double)after.DynamicInvoke(arguments)!;
            Assert.AreNotEqual(ValueAgreement.Different, CompareValues(p, q, 1e-12), $"{Text(source)} -> {Text(result)}: {p} against {q}");
        }
    }

    /// <summary>The agreement of two floating-point values.</summary>
    public enum ValueAgreement
    {
        /// <summary>The same bits, or two <c>NaN</c> (whatever their sign and payload, which IEEE-754 leaves unspecified).</summary>
        Identical,

        /// <summary>
        /// A difference the S3 contract allows (<c>docs/expressions/SimplificationPolicies.md</c>): zeros of opposite signs, or two
        /// different finite values within the relative reassociation tolerance. Not a numeric equality.
        /// </summary>
        S3Latitude,

        /// <summary>Any other difference: one <c>NaN</c>, an infinity against another value, finite values outside the tolerance.</summary>
        Different,
    }

    /// <summary>
    /// Compares a value of a source with the value of its result. The non-finite values are classified first: a relative
    /// tolerance alone would accept a finite value against an infinity, or two infinities of opposite signs, since
    /// <c>∞ - ∞</c> is <c>NaN</c> and <c>∞ ≤ ∞</c> holds.
    /// </summary>
    /// <param name="expected">The value of the source.</param>
    /// <param name="actual">The value of the result.</param>
    /// <param name="tolerance">The relative tolerance, applied to finite values only.</param>
    /// <returns>The agreement.</returns>
    internal static ValueAgreement CompareValues(double expected, double actual, double tolerance)
    {
        if (double.IsNaN(expected) || double.IsNaN(actual)) return double.IsNaN(expected) && double.IsNaN(actual) ? ValueAgreement.Identical : ValueAgreement.Different;
        if (BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual)) return ValueAgreement.Identical;
        if (double.IsInfinity(expected) || double.IsInfinity(actual)) return ValueAgreement.Different;
        if (expected == actual) return ValueAgreement.S3Latitude; // +0 and -0
        return Math.Abs(expected - actual) <= tolerance * Math.Max(Math.Abs(expected), Math.Abs(actual)) ? ValueAgreement.S3Latitude : ValueAgreement.Different;
    }

    /// <summary>The value comparison of these tests and of the <c>signs</c> campaign classifies non-finite values before any tolerance.</summary>
    /// <param name="expected">The value of the source.</param>
    /// <param name="actual">The value of the result.</param>
    /// <param name="agreement">The expected classification.</param>
    [TestMethod]
    [DataRow(1.0, double.PositiveInfinity, ValueAgreement.Different, DisplayName = "finite against infinity")]
    [DataRow(double.MaxValue, double.PositiveInfinity, ValueAgreement.Different, DisplayName = "largest finite against infinity")]
    [DataRow(double.PositiveInfinity, double.NegativeInfinity, ValueAgreement.Different, DisplayName = "+infinity against -infinity")]
    [DataRow(double.PositiveInfinity, double.PositiveInfinity, ValueAgreement.Identical, DisplayName = "+infinity against itself")]
    [DataRow(double.NegativeInfinity, double.NegativeInfinity, ValueAgreement.Identical, DisplayName = "-infinity against itself")]
    [DataRow(double.NaN, double.NaN, ValueAgreement.Identical, DisplayName = "NaN against NaN")]
    [DataRow(double.NaN, 1.0, ValueAgreement.Different, DisplayName = "NaN against finite")]
    [DataRow(1.0, double.NaN, ValueAgreement.Different, DisplayName = "finite against NaN")]
    [DataRow(1.0, 1.0 + 1e-14, ValueAgreement.S3Latitude, DisplayName = "close finite values")]
    [DataRow(1.0, 1.5, ValueAgreement.Different, DisplayName = "different finite values")]
    [DataRow(2.5, 2.5, ValueAgreement.Identical, DisplayName = "equal finite values")]
    [DataRow(0.0, -0.0, ValueAgreement.S3Latitude, DisplayName = "signed zeros")]
    public void CompareValues_ClassifiesNonFiniteValuesFirst(double expected, double actual, ValueAgreement agreement)
    {
        Assert.AreEqual(agreement, CompareValues(expected, actual, 1e-12));
        Assert.AreEqual(agreement, CompareValues(actual, expected, 1e-12), "symmetric");
    }

    /// <summary>NaN with another sign and payload is still a NaN result: IEEE-754 does not specify the propagated payload.</summary>
    [TestMethod]
    public void CompareValues_IgnoresNaNPayloads()
    {
        double other = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8_0000_0000_0123UL));
        Assert.IsTrue(double.IsNaN(other));
        Assert.AreEqual(ValueAgreement.Identical, CompareValues(double.NaN, other, 1e-12));
    }

    /// <summary>
    /// The minimal reproduction of §25: the same leaves with the same net signs, one association subtracting a group that holds
    /// the negative product. <c>master</c> gives one structure; V1b + C1 gives <c>… + (0 - (6.5 * c))</c> for the first.
    /// </summary>
    [TestMethod]
    public void MinimalReproduction_GivesOneStructure()
    {
        Expression product = Expression.Multiply(C, K(-6.5));
        AssertOneStructure(
            "a + (d + ((0 - (b / e)) + (-6.5 * c)))",
            Expression.Add(Expression.Subtract(A, Expression.Subtract(Expression.Divide(B, E), product)), D),
            Expression.Add(Expression.Add(Expression.Subtract(A, Expression.Divide(B, E)), product), D));
    }

    /// <summary>
    /// The three seeds of the grouped quotient corpus (<see cref="ExpressionSimplifierGroupedQuotientChainTests"/>) whose grouped
    /// treatment and level by level treatment differ under V1b: on <c>master</c> they agree, after one and two passes.
    /// </summary>
    [TestMethod]
    [DataRow(1346)]
    [DataRow(SecondGroupedSeed)]
    [DataRow(ThirdGroupedSeed)]
    public void GroupedCorpusSeeds_GiveTheLevelByLevelStructure(int seed)
    {
        Expression source = new ExpressionSimplifierGroupedQuotientChainTests.Generator(seed).Chain(2 + seed % 13);
        Expression reference = ExpressionSimplifierGroupedQuotientChainTests.Reference(source);
        (Expression result, FlattenAdmissionStatistics statistics) = ExpressionSimplifierGroupedQuotientChainTests.Counted(source);
        Assert.IsTrue(statistics.GroupedChains + statistics.GroupedFallbacks > 0, $"seed {seed}: not a grouped chain");
        Assert.AreEqual(ExpressionSimplifierGroupedQuotientChainTests.Dump(reference), ExpressionSimplifierGroupedQuotientChainTests.Dump(result), $"seed {seed}: {source}");
        Assert.AreEqual(
            ExpressionSimplifierGroupedQuotientChainTests.Dump(ExpressionSimplifierGroupedQuotientChainTests.Reference(reference)),
            ExpressionSimplifierGroupedQuotientChainTests.Dump(new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(result)),
            $"seed {seed} (second pass)");
    }

    /// <summary>
    /// The ten placements of the <c>signs</c> campaign whose associations give one structure on <c>master</c> and two under the
    /// dual convention (<c>docs/expressions/SimplifierFlatten.md</c> §25.12): the acceptance cases of a future dual normalization.
    /// Each pair holds the same leaves with the same net signs; the expected text is the bound result of <c>master</c>.
    /// </summary>
    /// <param name="index">The index of the placement in <see cref="DualResidues"/>.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    public void DualConventionResidues_GiveOneStructure(int index)
    {
        (string placement, string expected, Expression[] sources) = DualResidues()[index];
        AssertOneStructure(null, sources);
        foreach (Expression source in sources)
        {
            Expression once = S(Expression.Lambda(source, Parameters));
            Assert.AreEqual(expected, Text(((LambdaExpression)once).Body), $"{placement}: {Text(source)}");
        }
    }

    /// <summary>The placements of <see cref="DualConventionResidues_GiveOneStructure"/>: identifier, bound result of <c>master</c>, sources.</summary>
    /// <returns>The placements.</returns>
    private static (string Placement, string Expected, Expression[] Sources)[] DualResidues()
    {
        static Expression N(Expression x) => Expression.Negate(x);
        static Expression Add(Expression x, Expression y) => Expression.Add(x, y);
        static Expression Sub(Expression x, Expression y) => Expression.Subtract(x, y);
        static Expression Mul(Expression x, Expression y) => Expression.Multiply(x, y);
        Expression q = Expression.Divide(B, E);
        return
        [
            ("{a, -b/e, -6.5c, d} p3", "a + (d + ((0 - (b / e)) + (-6.5 * c)))",
                [Sub(Sub(Sub(N(q), N(Mul(K(-6.5), C))), N(D)), N(A)), Sub(N(q), Add(N(Mul(K(-6.5), C)), Add(N(D), N(A))))]),
            ("{a, -b/e, -6.5c, d} p12", "a + (d + ((0 - (b / e)) + (-6.5 * c)))",
                [Sub(Sub(Add(N(q), Mul(C, K(-6.5))), N(D)), N(A)), Sub(N(q), Sub(N(D), Sub(Mul(C, K(-6.5)), N(A))))]),
            ("{a, -b/e, +6.5c, d} p3", "a + (d + ((0 - (b / e)) + (6.5 * c)))",
                [Sub(Sub(Sub(N(q), N(Mul(K(6.5), C))), N(D)), N(A)), Sub(N(q), Add(N(Mul(K(6.5), C)), Add(N(D), N(A))))]),
            ("{a, -b/e, +6.5c, d} p12", "a + (d + ((0 - (b / e)) + (6.5 * c)))",
                [Sub(Sub(Add(N(q), Mul(C, K(6.5))), N(D)), N(A)), Sub(N(q), Sub(N(D), Sub(Mul(C, K(6.5)), N(A))))]),
            ("{a, b/e, -2c, -3d} p3", "a + ((b / e) + ((-2 * c) + (0 - (3 * d))))",
                [Sub(Sub(Sub(N(Mul(D, K(3))), N(A)), N(q)), N(Mul(K(-2), C))), Sub(N(Mul(D, K(3))), Add(N(q), Add(N(A), N(Mul(K(-2), C)))))]),
            ("{a, b/e, -2c, -3d} p21", "a + ((b / e) + ((-3 * d) + (0 - (2 * c))))",
                [Sub(Add(Add(q, N(Mul(K(2), C))), Mul(D, K(-3))), N(A)), Sub(N(Mul(K(2), C)), Sub(Sub(N(A), Mul(D, K(-3))), q))]),
            ("{a, -2c, 2d, b/e} p0", "a + ((b / e) + (-2 * (c + (0 - d))))",
                [Add(A, Sub(Mul(K(-2), C), Sub(Mul(K(-2), D), q))), Add(Sub(Add(A, Mul(K(-2), C)), Mul(K(-2), D)), q)]),
            ("{a, -2c, 2d, b/e} p4", "a + ((b / e) + (-2 * (c + (0 - d))))",
                [Add(A, Add(Mul(K(-2), C), Add(N(Mul(D, K(-2))), q))), Add(Add(Add(A, Mul(K(-2), C)), N(Mul(D, K(-2)))), q)]),
            ("{a, -2c, 2d, b/e} p21", "a + ((b / e) + (2 * ((0 - c) + d)))",
                [Sub(Sub(Sub(N(Mul(K(2), C)), N(Mul(K(2), D))), N(q)), N(A)), Sub(N(Mul(K(2), C)), Add(N(q), Add(N(A), N(Mul(K(2), D)))))]),
            ("{a, -2c, 2d, b/e} p23", "a + ((b / e) + (-2 * (c + (0 - d))))",
                [Sub(A, Sub(N(Mul(C, K(-2))), Add(N(Mul(D, K(-2))), q))), Add(Add(Sub(A, N(Mul(C, K(-2)))), N(Mul(D, K(-2)))), q)]),
        ];
    }

    /// <summary>The second seed of <see cref="GroupedCorpusSeeds_GiveTheLevelByLevelStructure"/>.</summary>
    private const int SecondGroupedSeed = 1973;

    /// <summary>The third seed of <see cref="GroupedCorpusSeeds_GiveTheLevelByLevelStructure"/>.</summary>
    private const int ThirdGroupedSeed = 2385;

    /// <summary>
    /// The four combinations of the sign of the coefficient and of the additive sign, each under a refused chain (a quotient)
    /// and an admitted one: the leaf written once, added or subtracted directly, or through a subtracted group whose inner
    /// subtraction restores its net sign, in left, right and balanced associations.
    /// </summary>
    /// <param name="coefficient">The coefficient of the leaf.</param>
    /// <param name="subtracted">Whether the leaf is subtracted.</param>
    /// <param name="quotient">Whether the chain holds a quotient (refused by the linear family).</param>
    [TestMethod]
    [DataRow(-6.5, false, true)]
    [DataRow(-6.5, true, true)]
    [DataRow(6.5, false, true)]
    [DataRow(6.5, true, true)]
    [DataRow(-6.5, false, false)]
    [DataRow(-6.5, true, false)]
    [DataRow(6.5, false, false)]
    [DataRow(6.5, true, false)]
    public void SignCombinations_AreIndependentOfTheAssociation(double coefficient, bool subtracted, bool quotient)
    {
        Expression leaf = Expression.Multiply(C, K(coefficient));
        Expression other = quotient ? Expression.Divide(B, E) : B;
        Func<Expression, Expression, Expression> outer = subtracted ? Expression.Subtract : Expression.Add;
        Func<Expression, Expression, Expression> inner = subtracted ? Expression.Add : Expression.Subtract;
        AssertOneStructure(
            null,
            // Left comb: ((a - other) ± leaf) + d.
            Expression.Add(outer(Expression.Subtract(A, other), leaf), D),
            // Right comb: a + ((0 - other) + (± leaf + d)), the sign in the group.
            Expression.Add(A, Expression.Subtract(D, inner(other, leaf))),
            // A subtracted group holding the leaf with the opposite inner sign: (a - (other ∓ leaf)) + d.
            Expression.Add(Expression.Subtract(A, inner(other, leaf)), D),
            // Balanced: (a - other) + (± leaf + d).
            Expression.Add(Expression.Subtract(A, other), subtracted ? Expression.Subtract(D, leaf) : Expression.Add(leaf, D)));
    }

    /// <summary>
    /// Simple and double inversions of the leaf's sign through groups: the leaf under one, two and three subtracted groups, with
    /// the net sign kept by the inner operators, gives one structure.
    /// </summary>
    [TestMethod]
    public void NestedInversions_GiveOneStructure()
    {
        Expression leaf = Expression.Multiply(K(-2), C);
        Expression q = Expression.Divide(B, E);
        AssertOneStructure(
            null,
            Expression.Add(Expression.Add(Expression.Subtract(A, q), leaf), D),
            Expression.Add(Expression.Subtract(A, Expression.Subtract(q, leaf)), D),
            Expression.Add(Expression.Subtract(A, Expression.Subtract(q, Expression.Subtract(D, Expression.Subtract(D, leaf)))), D),
            Expression.Add(Expression.Subtract(A, Expression.Subtract(q, Expression.Add(Expression.Subtract(D, D), leaf))), D));
    }

    /// <summary>
    /// Rebuilding then collecting again: replacing a subtracted group by its own simplified form (a level canonicalized alone,
    /// then collected by its parent) gives the structure of the whole source.
    /// </summary>
    [TestMethod]
    public void RebuiltGroup_CollectedAgain_GivesTheStructureOfTheSource()
    {
        foreach (double coefficient in new[] { -6.5, 6.5, -1e300 })
        {
            Expression leaf = Expression.Multiply(C, K(coefficient));
            Expression group = Expression.Subtract(Expression.Divide(B, E), leaf);
            Expression flat = Expression.Add(Expression.Add(Expression.Subtract(A, Expression.Divide(B, E)), leaf), D);
            Expression source = Expression.Add(Expression.Subtract(A, group), D);
            Expression rebuilt = Expression.Add(Expression.Subtract(A, S(group)), D);
            AssertOneStructure(null, flat, source, rebuilt);
        }
    }

    /// <summary>A shared negative-coefficient product, collected through a subtracted group and directly, gives one structure.</summary>
    [TestMethod]
    public void SharedProduct_GivesOneStructure()
    {
        Expression shared = Expression.Multiply(K(-2), Expression.Multiply(C, D));
        Expression q = Expression.Divide(B, E);
        AssertOneStructure(
            null,
            Expression.Add(Expression.Add(Expression.Subtract(A, q), shared), Expression.Multiply(shared, K(3))),
            Expression.Add(Expression.Subtract(A, Expression.Subtract(q, shared)), Expression.Multiply(shared, K(3))));
    }

    /// <summary>
    /// Extreme coefficients and the minimal values of the integers: the same leaves in two associations give one structure,
    /// with the source's values (unchecked integer arithmetic).
    /// </summary>
    [TestMethod]
    public void ExtremeAndIntegralCoefficients_GiveOneStructure()
    {
        foreach (double coefficient in new[] { -1e300, -double.MaxValue, -double.Epsilon })
        {
            Expression leaf = Expression.Multiply(C, K(coefficient));
            AssertOneStructure(
                null,
                Expression.Add(Expression.Subtract(A, Expression.Subtract(Expression.Divide(B, E), leaf)), D),
                Expression.Add(Expression.Add(Expression.Subtract(A, Expression.Divide(B, E)), leaf), D));
        }

        foreach ((Type type, object[] values) in new (Type, object[])[]
        {
            (typeof(int), [int.MinValue, int.MinValue + 1, -2, -3]),
            (typeof(long), [long.MinValue, long.MinValue + 1, -2L]),
            (typeof(short), [short.MinValue, (short)(short.MinValue + 1), (short)-2]),
        })
        {
            ParameterExpression[] p = new[] { "a", "b", "c", "d", "e" }.Select(n => Expression.Parameter(type, n)).ToArray();
            foreach (object value in values)
            {
                Expression leaf = Expression.Multiply(p[2], Expression.Constant(value, type));
                Expression nested = Expression.Add(Expression.Subtract(p[0], Expression.Subtract(Expression.Divide(p[1], p[4]), leaf)), p[3]);
                Expression flat = Expression.Add(Expression.Add(Expression.Subtract(p[0], Expression.Divide(p[1], p[4])), leaf), p[3]);
                Expression first = S(Expression.Lambda(nested, p)), second = S(Expression.Lambda(flat, p));
                Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, second), $"{type.Name} {value}: {Text(first)} against {Text(second)}");
                Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, S(first)), Text(first));
                Delegate f = Expression.Lambda(nested, p).Compile(), g = ((LambdaExpression)first).Compile();
                object[] arguments = new object[] { 7, -11, 13, 5, 3 }.Select(v => Convert.ChangeType(v, type)).ToArray();
                Assert.AreEqual(f.DynamicInvoke(arguments), g.DynamicInvoke(arguments), $"{type.Name} {value}");
            }
        }
    }
}
