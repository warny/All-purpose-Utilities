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
/// The analysis of the terms of an additive chain (mission C3b, <c>docs/expressions/SimplifierFlatten.md</c> §12): each term
/// is decomposed into a numeric coefficient and factors, classified, and described with the metadata of mission C3a, so that
/// the terms sharing factors can be found without extracting anything again from the raw expressions.
/// </summary>
[TestClass]
public class ExpressionSimplifierTermAnalysisTests
{
    /// <summary>A derived simplifier without any member.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>An extension node that fails on any use beyond its type tests: the analysis must never read it.</summary>
    private sealed class HostileNode : Expression
    {
        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => typeof(double);

        /// <inheritdoc/>
        public override bool CanReduce => false;

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor) => throw new InvalidOperationException("The node was visited.");
    }

    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
    private static readonly MethodInfo Cos = typeof(Math).GetMethod(nameof(Math.Cos), [typeof(double)])!;

    /// <summary>Builds a <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C(double value) => Expression.Constant(value, typeof(double));

    /// <summary>The square of <paramref name="operand"/>, as an ordinary power.</summary>
    /// <param name="operand">The base.</param>
    /// <returns>The power.</returns>
    private static Expression Square(Expression operand) => Expression.Power(operand, C(2));

    /// <summary>Analyzes positive terms outside any operation.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The analysis.</returns>
    private static ExpressionSimplifier.AdditiveTermAnalysis Analyze(params Expression[] terms)
        => ExpressionSimplifier.AdditiveTermAnalysis.Analyze(new ExpressionSimplifier(), [.. terms.Select(static t => (t, false))], typeof(double), [], record: false);

    /// <summary>The limits of the operations of <see cref="InOperation"/>.</summary>
    private static readonly ExpressionResourceLimits SharedLimits = new() { MaxTransformVisits = 1_000_000_000, MaxStructuralVisits = 10_000_000_000 };

    /// <summary>Runs <paramref name="function"/> inside one expression operation of <paramref name="simplifier"/>.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="simplifier">The owner of the operation.</param>
    /// <param name="function">The function.</param>
    /// <returns>Its result.</returns>
    private static T InOperation<T>(ExpressionSimplifier simplifier, Func<T> function)
        => ExpressionResourceBudget.Run(SharedLimits, simplifier, null, null, function, static f => f());

    /// <summary>Runs <paramref name="action"/> with metadata counters installed and returns them.</summary>
    /// <param name="action">The measured action.</param>
    /// <returns>The counters.</returns>
    private static SimplificationMetadataStatistics Measure(Action action)
    {
        var statistics = new SimplificationMetadataStatistics();
        ExpressionSimplifier.MetadataStatistics = statistics;
        try
        {
            action();
        }
        finally
        {
            ExpressionSimplifier.MetadataStatistics = null;
        }

        return statistics;
    }

    /// <summary>
    /// The elementary forms of the mission: coefficient, factors and family of <c>x</c>, <c>3*x</c>, <c>x*3</c>, <c>a*x</c>,
    /// <c>(a+3)*x</c>, <c>x²</c>, <c>2*x²</c>, <c>3*cos(x)</c>, <c>sin(x)</c> and a constant.
    /// </summary>
    [TestMethod]
    public void ElementaryForms_AreDecomposedIntoCoefficientAndFactors()
    {
        ConstantExpression three = C(3), two = C(2), seven = C(7);
        Expression sum = Expression.Add(A, C(3));
        Expression xSquared = Square(X);
        Expression cosX = Expression.Call(Cos, X), sinX = Expression.Call(Sin, X);
        ExpressionSimplifier.AdditiveTermAnalysis analysis = Analyze(
            X,
            Expression.Multiply(three, X),
            Expression.Multiply(X, three),
            Expression.Multiply(A, X),
            Expression.Multiply(sum, X),
            xSquared,
            Expression.Multiply(two, xSquared),
            Expression.Multiply(three, cosX),
            sinX,
            seven);

        (AdditiveTermFamily Family, ConstantExpression? Coefficient, Expression[] Factors)[] expected =
        [
            (AdditiveTermFamily.Parameter, null, [X]),
            (AdditiveTermFamily.Parameter, three, [X]),
            (AdditiveTermFamily.Parameter, three, [X]),
            (AdditiveTermFamily.Product, null, [A, X]),
            (AdditiveTermFamily.Product, null, [sum, X]),
            (AdditiveTermFamily.Power, null, [xSquared]),
            (AdditiveTermFamily.Power, two, [xSquared]),
            (AdditiveTermFamily.Function, three, [cosX]),
            (AdditiveTermFamily.Function, null, [sinX]),
            (AdditiveTermFamily.Constant, seven, []),
        ];

        Assert.AreEqual(expected.Length, analysis.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            ref readonly AnalyzedAdditiveTerm term = ref analysis[i];
            Assert.AreEqual(expected[i].Family, term.Family, "family of term " + i);
            Assert.AreSame(expected[i].Coefficient, term.Coefficient, "coefficient of term " + i + " (the node itself, never converted)");
            CollectionAssert.AreEqual(expected[i].Factors, analysis.GetFactors(i).ToArray(), "factors of term " + i);
            Assert.AreEqual(i, term.SourceIndex);
            Assert.IsTrue(term.IsDecomposable);
        }

        Assert.IsFalse(analysis[7].IsFunctionLike, "3 * cos(x) is a product for the grouping order");
        Assert.IsTrue(analysis[8].IsFunctionLike, "sin(x) is function-like for the grouping order");
    }

    /// <summary>
    /// Signs and positions are kept; a negation, a division, a constant of another type and an extension node are opaque,
    /// indivisible terms, and the extension node is never read.
    /// </summary>
    [TestMethod]
    public void SignsPositionsAndOpaqueTerms_AreKept()
    {
        Expression negation = Expression.Negate(X);
        Expression division = Expression.Divide(X, C(2));
        Expression hostile = new HostileNode();
        Expression otherType = Expression.Constant(2, typeof(int));
        var analysis = ExpressionSimplifier.AdditiveTermAnalysis.Analyze(
            new ExpressionSimplifier(),
            [(negation, true), (division, false), (hostile, true), (otherType, false)],
            typeof(double),
            [],
            record: false);

        for (int i = 0; i < analysis.Count; i++)
        {
            Assert.AreEqual(AdditiveTermFamily.Opaque, analysis[i].Family, "term " + i);
            Assert.IsFalse(analysis[i].IsDecomposable);
            Assert.IsNull(analysis[i].Coefficient);
            Assert.AreEqual(1, analysis[i].FactorCount);
            Assert.AreSame(analysis[i].Expression, analysis.GetFactors(i)[0]);
        }

        Assert.IsTrue(analysis[0].IsNegative);
        Assert.IsFalse(analysis[1].IsNegative);
        Assert.IsTrue(analysis[2].IsNegative);
        Assert.AreEqual(2, analysis[2].SourceIndex);
    }

    /// <summary>
    /// A product with two constant factors folds nothing: both stay factors and the coefficient is left undetermined; a
    /// product of several factors keeps them in the order of the term.
    /// </summary>
    [TestMethod]
    public void SeveralConstantFactors_StayFactors()
    {
        ExpressionSimplifier.AdditiveTermAnalysis analysis = Analyze(
            Expression.Multiply(C(2), Expression.Multiply(C(3), X)),
            Expression.Multiply(Expression.Multiply(A, X), Y));

        Assert.IsNull(analysis[0].Coefficient);
        Assert.AreEqual(3, analysis[0].FactorCount);
        Assert.AreEqual(AdditiveTermFamily.Product, analysis[0].Family);
        CollectionAssert.AreEqual(new Expression[] { A, X, Y }, analysis.GetFactors(1).ToArray());
    }

    /// <summary>
    /// The C3c contract: <c>x</c>, <c>2*x</c> and <c>3*x</c> have the same factors, <c>a*x</c> shares the factor <c>x</c>
    /// with them; <c>x²</c> and <c>2*x²</c> share <c>x²</c>; <c>sin(x)</c> and <c>3*sin(x)</c> share <c>sin(x)</c>; the factors
    /// of a product match in any order.
    /// </summary>
    [TestMethod]
    public void TermsSharingFactors_AreBroughtTogether()
    {
        ExpressionSimplifier.AdditiveTermAnalysis analysis = Analyze(
            X,
            Expression.Multiply(C(2), X),
            Expression.Multiply(A, X),
            Expression.Multiply(C(3), X),
            Square(X),
            Expression.Multiply(C(2), Square(X)),
            Expression.Call(Sin, X),
            Expression.Multiply(C(3), Expression.Call(Sin, X)),
            Expression.Multiply(X, A),
            Expression.Multiply(C(5), Expression.Call(Cos, X)));

        CollectionAssert.AreEqual(new[] { 0, 0, 2, 0, 4, 4, 6, 6, 2, 9 }, analysis.GroupByFactors());
        Assert.IsTrue(analysis.HaveSameFactors(2, 8), "a * x and x * a");
        Assert.IsFalse(analysis.HaveSameFactors(0, 2), "x and a * x");
        Assert.IsTrue(analysis.TryFindCommonFactor(2, 3, out int first, out int second));
        Assert.AreEqual(1, first, "x is the second factor of a * x");
        Assert.AreEqual(0, second);
        Assert.IsFalse(analysis.TryFindCommonFactor(6, 9, out _, out _), "sin(x) and cos(x) share no factor");
        Assert.AreEqual(analysis[6].Family, analysis[9].Family, "sin(x) and 5 * cos(x) are both function factors");
        Assert.AreNotEqual(analysis[6].FactorHash, analysis[9].FactorHash);

        // The coefficients are kept for the merge: 2 and 3 for the group of x, none for x itself.
        Assert.IsNull(analysis[0].Coefficient);
        Assert.AreEqual(2.0, analysis[1].Coefficient!.Value);
        Assert.AreEqual(3.0, analysis[3].Coefficient!.Value);
    }

    /// <summary>Structurally equal factors built separately group together: identity is structural, never by reference only.</summary>
    [TestMethod]
    public void DistinctInstancesOfEqualFactors_Group()
    {
        ExpressionSimplifier.AdditiveTermAnalysis analysis = Analyze(
            Expression.Multiply(C(3), Expression.Call(Cos, X)),
            Expression.Multiply(C(5), Expression.Call(Cos, X)),
            Expression.Call(Cos, Y));

        CollectionAssert.AreEqual(new[] { 0, 0, 2 }, analysis.GroupByFactors());
    }

    /// <summary>
    /// Metadata present: hashes and keys stored by a canonicalization of the running operation are reused without a walk.
    /// Metadata absent: they are computed. Another lexical context: only the key is rebuilt, the hash is still reused.
    /// </summary>
    [TestMethod]
    public void Metadata_IsReusedComputedOrRebuiltPerLexicalContext()
    {
        var simplifier = new ExpressionSimplifier();
        Expression term = Expression.Multiply(C(3), Expression.Call(Cos, X));
        ParameterExpression[][] scopes = [[X]];
        ParameterExpression[][] otherScopes = [[X]];

        // Absent: outside an operation nothing is stored, everything is computed.
        SimplificationMetadataStatistics absent = Measure(() => ExpressionSimplifier.AdditiveTermAnalysis.Analyze(simplifier, [(term, false)], typeof(double), scopes, record: true).GetKey(0));
        Assert.AreEqual(0, absent.HashReuses);
        Assert.AreEqual(0, absent.KeyReuses);
        Assert.IsTrue(absent.HashComputations >= 2, "the term and its factor");
        Assert.AreEqual(1, absent.KeyBuilds);

        (SimplificationMetadataStatistics present, SimplificationMetadataStatistics other) = InOperation(simplifier, () =>
        {
            ExpressionSimplifier.AdditiveTermAnalysis.Analyze(simplifier, [(term, false)], typeof(double), scopes, record: true).GetKey(0);
            SimplificationMetadataStatistics again = Measure(() => ExpressionSimplifier.AdditiveTermAnalysis.Analyze(simplifier, [(term, false)], typeof(double), scopes, record: true).GetKey(0));
            SimplificationMetadataStatistics elsewhere = Measure(() => ExpressionSimplifier.AdditiveTermAnalysis.Analyze(simplifier, [(term, false)], typeof(double), otherScopes, record: true).GetKey(0));
            return (again, elsewhere);
        });

        Assert.AreEqual(0, present.HashComputations, "present: every hash is reused");
        Assert.AreEqual(0, present.KeyBuilds, "present: the key is reused");
        Assert.AreEqual(1, present.KeyReuses);
        Assert.AreEqual(0, other.HashComputations, "another context: the hash does not depend on the scopes");
        Assert.AreEqual(1, other.KeyBuilds, "another context: the key is rebuilt, never reused");
        Assert.AreEqual(0, other.KeyReuses);
    }

    /// <summary>Keys are built on demand only: an analysis whose keys are never requested builds none.</summary>
    [TestMethod]
    public void Keys_AreBuiltOnDemandOnly()
    {
        SimplificationMetadataStatistics statistics = Measure(() => Analyze(X, Expression.Multiply(C(2), X), Expression.Multiply(A, Y)));
        Assert.AreEqual(0, statistics.KeyBuilds);

        ExpressionSimplifier.AdditiveTermAnalysis analysis = Analyze(X, Y);
        SimplifiedExpressionInfo info = analysis.Describe(1);
        Assert.AreSame(Y, info.Expression);
        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(Y), info.StructuralHash);
        Assert.AreSame(info.Key, analysis.GetKey(1), "a key is built once per analysis");
    }

    /// <summary>
    /// States are reported, never strengthened: leaves are canonical for the built-in type only, a node is unknown unless the
    /// analyzing instance recorded a state for it, and a derived simplifier reads unknown for every node.
    /// </summary>
    [TestMethod]
    public void States_AreReportedAsRecorded()
    {
        Expression product = Expression.Multiply(C(2), X);
        ExpressionSimplifier.AdditiveTermAnalysis builtIn = Analyze(X, C(4), product);
        Assert.AreEqual(CanonicalizationState.Canonical, builtIn[0].State);
        Assert.AreEqual(CanonicalizationState.Canonical, builtIn[1].State);
        Assert.AreEqual(CanonicalizationState.Unknown, builtIn[2].State, "nothing proves a product is a fixed point");

        var derived = ExpressionSimplifier.AdditiveTermAnalysis.Analyze(new DerivedSimplifier(), [(X, false), (product, false)], typeof(double), [], record: false);
        Assert.AreEqual(CanonicalizationState.Unknown, derived[0].State);
        Assert.AreEqual(CanonicalizationState.Unknown, derived[1].State);
    }

    /// <summary>The factor hash does not depend on the order of the factors and matches for equal structures.</summary>
    [TestMethod]
    public void FactorHash_IsOrderIndependent()
    {
        ExpressionSimplifier.AdditiveTermAnalysis analysis = Analyze(
            Expression.Multiply(Expression.Multiply(A, X), Y),
            Expression.Multiply(Expression.Multiply(Y, A), X),
            Expression.Multiply(Expression.Multiply(Y, A), A));

        Assert.AreEqual(analysis[0].FactorHash, analysis[1].FactorHash);
        Assert.IsTrue(analysis.HaveSameFactors(0, 1));
        Assert.IsFalse(analysis.HaveSameFactors(0, 2), "a multiset: a twice is not a and x");
    }
}
