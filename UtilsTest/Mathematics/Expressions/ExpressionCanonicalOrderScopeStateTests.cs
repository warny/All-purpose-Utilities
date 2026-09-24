using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S5 ("Construction-performance cleanup" — see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>), specifically its P4 change: the internal
/// <c>ExpressionCanonicalOrder.ScopeState</c> <see langword="ref struct"/> that replaced <c>BuildKey</c>'s/
/// <c>BuildKeys</c>' eagerly-materialized, per-call <c>List&lt;ParameterExpression[]&gt;</c> scope workspace
/// with an immutable enclosing-scope reference plus a lazily-allocated nested-lambda scope stack.
/// </summary>
/// <remarks>
/// These tests deliberately exercise the internal <c>ExpressionCanonicalOrder.Compare</c>/<c>BuildKey</c>
/// helpers directly via reflection, exactly like <see cref="ExpressionSimplifierStructuralCanonicalizationTests"/>'s
/// own <c>Compare</c>/<c>CompareType</c>/<c>CompareMethod</c> coverage, so the tests characterize
/// <c>ExpressionCanonicalOrder</c>'s own scope-lookup semantics in isolation from the additive/multiplicative
/// canonicalization pipeline that is the only production caller of these entry points.
/// </remarks>
[TestClass]
public class ExpressionCanonicalOrderScopeStateTests
{
    /// <summary>Reflected internal <c>Utils.Mathematics.Expressions.ExpressionCanonicalOrder</c> type.</summary>
    private static readonly Type ExpressionCanonicalOrderType = typeof(ExpressionSimplifier).Assembly
        .GetType("Utils.Mathematics.Expressions.ExpressionCanonicalOrder")!;

    /// <summary>Reflected <c>ExpressionCanonicalOrder.Compare(Expression, Expression, IReadOnlyList&lt;ParameterExpression[]&gt;)</c> helper.</summary>
    private static readonly MethodInfo CompareMethod = ExpressionCanonicalOrderType.GetMethod(
        "Compare", BindingFlags.NonPublic | BindingFlags.Static,
        [typeof(Expression), typeof(Expression), typeof(IReadOnlyList<ParameterExpression[]>)])!;

    /// <summary>Invokes the internal <c>ExpressionCanonicalOrder.Compare</c> helper via reflection, without changing its accessibility.</summary>
    /// <param name="x">The first expression.</param>
    /// <param name="y">The second expression.</param>
    /// <param name="enclosingScopes">The lexical scopes enclosing both expressions, outermost first.</param>
    /// <returns>The comparison result.</returns>
    private static int InvokeCompare(Expression? x, Expression? y, IReadOnlyList<ParameterExpression[]> enclosingScopes) =>
        (int)CompareMethod.Invoke(null, [x, y, enclosingScopes])!;

    // ------------------------------------------------------------------------------------------
    // 1. Enclosing scopes without a nested lambda: depth/position ordering must survive the
    //    ScopeState refactor exactly as the pre-P4 single concatenated list produced it.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Three enclosing scopes, each declaring two same-type parameters, produce six bound parameters whose
    /// (depth, position) pairs are all pairwise distinct: <c>(0,0) (0,1) (1,0) (1,1) (2,0) (2,1)</c> (depth 0
    /// = the innermost/last-supplied enclosing scope). Every parameter shares <see cref="double"/> as its
    /// declared type, so names/types cannot accidentally decide the order - only <c>ScopeState</c>'s
    /// enclosing-scope search (innermost-to-outermost, no nested frames pushed here) can. Sorting all six in
    /// reverse-declaration order by <c>ExpressionCanonicalOrder.Compare</c> must recover exactly the
    /// depth-then-position ascending order.
    /// </summary>
    [TestMethod]
    public void Compare_MultipleEnclosingScopesWithoutNestedLambda_OrdersByDepthThenPosition()
    {
        ParameterExpression p00 = Expression.Parameter(typeof(double), "p00");
        ParameterExpression p01 = Expression.Parameter(typeof(double), "p01");
        ParameterExpression p10 = Expression.Parameter(typeof(double), "p10");
        ParameterExpression p11 = Expression.Parameter(typeof(double), "p11");
        ParameterExpression p20 = Expression.Parameter(typeof(double), "p20");
        ParameterExpression p21 = Expression.Parameter(typeof(double), "p21");

        // Outermost first, as ExpressionCanonicalOrder.BuildKey's enclosingScopes contract requires.
        var scopes = new List<ParameterExpression[]>
        {
            new[] { p00, p01 }, // depth 2 (outermost)
            new[] { p10, p11 }, // depth 1
            new[] { p20, p21 }, // depth 0 (innermost)
        };

        var shuffled = new List<Expression> { p01, p00, p11, p10, p21, p20 };
        shuffled.Sort((x, y) => InvokeCompare(x, y, scopes));

        var expectedOrder = new[] { p20, p21, p10, p11, p00, p01 };
        for (int i = 0; i < expectedOrder.Length; i++)
        {
            Assert.IsTrue(ReferenceEquals(expectedOrder[i], shuffled[i]),
                $"Position {i}: expected the parameter at depth {expectedOrder.Length / 2 - 1 - i / 2}, position {i % 2}.");
        }
    }

    // ------------------------------------------------------------------------------------------
    // 2. Nested lambda plus enclosing capture: alpha-equivalence must hold regardless of which
    //    ParameterExpression instances/names are used for the nested lambda's own parameter.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A lambda nested inside an enclosing scope, whose body references both its own parameter (nested,
    /// depth 0) and the enclosing scope's parameter (depth 1), must produce IDENTICAL keys for two
    /// alpha-equivalent instances that use entirely different <see cref="ParameterExpression"/> instances and
    /// names for the nested parameter - proving <c>ScopeState.NestedScopes</c>' lazy allocation and lookup
    /// reproduce the same depth-independent-of-identity result the pre-P4 combined list did.
    /// </summary>
    [TestMethod]
    public void Compare_NestedLambdaCapturingEnclosingParameter_AlphaEquivalentInstancesTie()
    {
        ParameterExpression outer = Expression.Parameter(typeof(double), "outer");
        var enclosingScopes = new List<ParameterExpression[]> { new[] { outer } };

        ParameterExpression x1 = Expression.Parameter(typeof(double), "x1");
        ParameterExpression x2 = Expression.Parameter(typeof(double), "x2");
        Expression lambda1 = Expression.Lambda(Expression.Add(outer, x1), x1);
        Expression lambda2 = Expression.Lambda(Expression.Add(outer, x2), x2);

        int forward = InvokeCompare(lambda1, lambda2, enclosingScopes);
        int backward = InvokeCompare(lambda2, lambda1, enclosingScopes);

        Assert.AreEqual(0, forward, "Two alpha-equivalent nested lambdas (different parameter instances/names) capturing the same enclosing parameter must tie.");
        Assert.AreEqual(0, backward, "Compare must be symmetric for a tie.");

        // Negative control: a lambda that does NOT capture the enclosing parameter (x + x instead of
        // outer + x) must NOT tie with one that does, proving the equality above is not vacuous.
        Expression notCapturing = Expression.Lambda(Expression.Add(x1, x1), x1);
        Assert.AreNotEqual(0, InvokeCompare(lambda1, notCapturing, enclosingScopes),
            "A lambda referencing the enclosing parameter must not tie with one that only references its own parameter.");
    }

    // ------------------------------------------------------------------------------------------
    // 3. Multiple nested lambda depths: enclosing scope -> nested A -> nested B, with parameters
    //    captured from all three depths, proves 0/1/2 remain correctly distinguished.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds <c>Lambda(a =&gt; Lambda(b =&gt; REF, [b]), [a])</c> for three choices of <c>REF</c> - the
    /// enclosing parameter <c>outer</c> (expected depth 2: two nested frames pushed by this same call, then
    /// found in the one enclosing scope), the outer nested lambda's own parameter <c>a</c> (expected depth 1),
    /// and the inner nested lambda's own parameter <c>b</c> (expected depth 0) - all otherwise structurally
    /// identical (same lambda/parameter types, same shape) so the complete key's ONLY distinguishing
    /// dimension is <c>ParameterKey</c>'s <c>(depth, position, type)</c> triple with a shared position (0) and
    /// shared type (<see cref="double"/>). This directly exercises <c>ScopeState</c> combining an
    /// <see cref="ExpressionCanonicalOrder"/>-lazily-allocated two-level <c>NestedScopes</c> stack with a
    /// non-empty <c>EnclosingScopes</c> snapshot, proving the two depth ranges add up correctly rather than
    /// overlapping or double-counting.
    /// </summary>
    [TestMethod]
    public void Compare_TwoNestedLambdaLevelsPlusEnclosingScope_DistinguishesAllThreeDepths()
    {
        ParameterExpression outer = Expression.Parameter(typeof(double), "outer");
        var enclosingScopes = new List<ParameterExpression[]> { new[] { outer } };

        static Expression BuildDoublyNested(Func<ParameterExpression, ParameterExpression, Expression> body)
        {
            ParameterExpression a = Expression.Parameter(typeof(double), "a");
            ParameterExpression b = Expression.Parameter(typeof(double), "b");
            Expression innerLambda = Expression.Lambda(body(a, b), b);
            return Expression.Lambda(innerLambda, a);
        }

        Expression referencesOuter = BuildDoublyNested((a, b) => outer);
        Expression referencesA = BuildDoublyNested((a, b) => a);
        Expression referencesB = BuildDoublyNested((a, b) => b);

        int bVsA = InvokeCompare(referencesB, referencesA, enclosingScopes);
        int aVsOuter = InvokeCompare(referencesA, referencesOuter, enclosingScopes);
        int bVsOuter = InvokeCompare(referencesB, referencesOuter, enclosingScopes);

        Assert.IsTrue(bVsA < 0, "Depth 0 (innermost nested parameter b) must sort before depth 1 (a).");
        Assert.IsTrue(aVsOuter < 0, "Depth 1 (a) must sort before depth 2 (the enclosing parameter, reached through two nested frames).");
        Assert.IsTrue(bVsOuter < 0, "Depth 0 (b) must sort before depth 2 (outer), transitively.");

        Assert.AreEqual(Math.Sign(bVsA), -Math.Sign(InvokeCompare(referencesA, referencesB, enclosingScopes)), "Compare must be antisymmetric.");
        Assert.AreEqual(Math.Sign(aVsOuter), -Math.Sign(InvokeCompare(referencesOuter, referencesA, enclosingScopes)), "Compare must be antisymmetric.");

        // Alpha-equivalence at this same two-level nesting depth, sharing the SAME enclosing scope instance
        // (matching how one BuildKeys/CanonicalizeAdditiveExpression caller shares one ambient scope across
        // sibling terms): fresh, differently-named nested parameter instances for a/b must still tie with
        // referencesOuter, since only the enclosing-captured reference differs in name/instance choice for a/b.
        Expression referencesOuterAgain = BuildDoublyNested((a, b) => outer);
        Assert.AreEqual(0, InvokeCompare(referencesOuter, referencesOuterAgain, enclosingScopes),
            "Two structurally alpha-equivalent doubly-nested lambdas (fresh a/b instances each time), both capturing the same enclosing parameter instance, must tie.");
    }
}
