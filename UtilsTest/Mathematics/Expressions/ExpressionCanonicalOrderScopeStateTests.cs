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

    /// <summary>Reflected <c>ExpressionCanonicalOrder.BuildKey(Expression, IReadOnlyList&lt;ParameterExpression[]&gt;)</c> helper.</summary>
    private static readonly MethodInfo BuildKeyMethod = ExpressionCanonicalOrderType.GetMethod(
        "BuildKey", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Reflected <c>ExpressionCanonicalOrder.BuildKeyFromSnapshot(Expression, ParameterExpression[][])</c> helper (PR #606 review round 2).</summary>
    private static readonly MethodInfo BuildKeyFromSnapshotMethod = ExpressionCanonicalOrderType.GetMethod(
        "BuildKeyFromSnapshot", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Reflected internal <c>ExpressionCanonicalOrder.KeyNode</c> nested type.</summary>
    private static readonly Type KeyNodeType = ExpressionCanonicalOrderType.GetNestedType("KeyNode", BindingFlags.NonPublic)!;

    /// <summary>Reflected <c>ExpressionCanonicalOrder.KeyNode.CompareTo(KeyNode?)</c>.</summary>
    private static readonly MethodInfo KeyNodeCompareToMethod = KeyNodeType.GetMethod("CompareTo", BindingFlags.Public | BindingFlags.Instance)!;

    /// <summary>Invokes the internal <c>ExpressionCanonicalOrder.BuildKey</c> helper via reflection, without changing its accessibility.</summary>
    /// <param name="expression">The expression to key.</param>
    /// <param name="enclosingScopes">The lexical scopes enclosing the expression, outermost first - any <see cref="IReadOnlyList{T}"/> implementation, not necessarily the production <c>ParameterExpression[][]</c> array type.</param>
    /// <returns>The resulting key, boxed as <see cref="object"/> since the concrete <c>KeyNode</c> type is internal.</returns>
    private static object InvokeBuildKey(Expression expression, IReadOnlyList<ParameterExpression[]> enclosingScopes) =>
        BuildKeyMethod.Invoke(null, [expression, enclosingScopes])!;

    /// <summary>Invokes the internal, zero-copy <c>ExpressionCanonicalOrder.BuildKeyFromSnapshot</c> helper via reflection, without changing its accessibility.</summary>
    /// <param name="expression">The expression to key.</param>
    /// <param name="enclosingScopes">A trusted snapshot array, stored directly with no defensive copy.</param>
    /// <returns>The resulting key, boxed as <see cref="object"/> since the concrete <c>KeyNode</c> type is internal.</returns>
    private static object InvokeBuildKeyFromSnapshot(Expression expression, ParameterExpression[][] enclosingScopes) =>
        BuildKeyFromSnapshotMethod.Invoke(null, [expression, enclosingScopes])!;

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

    // ------------------------------------------------------------------------------------------
    // PR #606 review round 1: ScopeState.EnclosingScopes must behave the same whether the caller
    // supplies the exact production ParameterExpression[][] array type (the zero-copy fast path) or
    // an arbitrary IReadOnlyList<ParameterExpression[]> such as a plain List<T> (the defensive-copy
    // fallback path, restoring the pre-P4 snapshot contract for any non-production caller).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>BuildKey</c> must resolve the SAME bound-parameter (depth, position, type) key whether
    /// <c>enclosingScopes</c> is supplied as the exact production <c>ParameterExpression[][]</c> array type
    /// (stored directly, no copy) or as an arbitrary <see cref="IReadOnlyList{T}"/> such as a plain
    /// <see cref="List{T}"/> (defensively copied into a fresh array first). Both code paths must be
    /// structurally equivalent, not merely both "not crash".
    /// </summary>
    [TestMethod]
    public void BuildKey_ArrayAndListEnclosingScopes_ProduceEqualKeys_ForTheSameBoundParameter()
    {
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        ParameterExpression[][] arrayScopes = [[p]];
        var listScopes = new List<ParameterExpression[]> { new[] { p } };

        object keyViaArray = InvokeBuildKey(p, arrayScopes);
        object keyViaList = InvokeBuildKey(p, listScopes);

        int forward = (int)KeyNodeCompareToMethod.Invoke(keyViaArray, [keyViaList])!;
        int backward = (int)KeyNodeCompareToMethod.Invoke(keyViaList, [keyViaArray])!;

        Assert.AreEqual(0, forward,
            "BuildKey must produce the same bound-parameter key whether enclosingScopes is the concrete " +
            "production ParameterExpression[][] array (zero-copy fast path) or an arbitrary List<T> " +
            "(defensive-copy fallback path).");
        Assert.AreEqual(0, backward, "The comparison must be symmetric for a tie.");
    }

    /// <summary>
    /// Regression check for the narrower, POST-return half of the pre-P4 snapshot guarantee: a key already
    /// built from a caller-owned, mutable <see cref="List{T}"/> must remain structurally correct even after
    /// the caller later clears or replaces that same list instance. This does not, and cannot from a single
    /// synchronous test, directly exercise mutation DURING construction (a concurrent or reentrant write while
    /// <c>BuildKey</c> is still walking the term) - <c>Build</c> is a purely synchronous traversal that never
    /// calls back into caller code, so no such reentrant window exists to test against in-process. The actual
    /// guarantee against a during-construction mutation is structural, not something this test proves: the
    /// defensive copy in <c>ScopeState</c>'s <see cref="IReadOnlyList{T}"/>-typed constructor runs to
    /// completion before <c>Build</c> is ever invoked (see <c>ScopeState</c>'s remarks), so by construction
    /// order there is nothing left for a caller to race against once that constructor returns. This test
    /// verifies the one half of the guarantee that IS independently observable: no lingering aliasing after
    /// the call returns.
    /// </summary>
    [TestMethod]
    public void BuildKey_MutatingCallerListAfterConstruction_DoesNotAffectAlreadyBuiltKey()
    {
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        var mutableScopes = new List<ParameterExpression[]> { new[] { p } };

        object keyBuiltBeforeMutation = InvokeBuildKey(p, mutableScopes);

        // Mutate the caller's own list AFTER BuildKey has already returned.
        mutableScopes.Clear();

        object referenceKey = InvokeBuildKey(p, new List<ParameterExpression[]> { new[] { p } });

        int comparison = (int)KeyNodeCompareToMethod.Invoke(keyBuiltBeforeMutation, [referenceKey])!;
        Assert.AreEqual(0, comparison,
            "A key already built from a caller-supplied List must remain structurally correct (still a bound " +
            "parameter at depth 0, position 0) even after the caller later clears that same list instance.");
    }

    // ------------------------------------------------------------------------------------------
    // PR #606 review round 2: the generic BuildKey/BuildKeys entry points must defensively copy
    // EVERY IReadOnlyList<ParameterExpression[]> input, including one whose runtime type happens to
    // be the exact production ParameterExpression[][] array - not just non-array shapes like List<T>.
    // Round 1 restored the snapshot guarantee only for non-array inputs (an `as ParameterExpression[][]`
    // runtime-type check let an arbitrary caller-owned array through the zero-copy path too). Round 2
    // replaces that runtime check with two ScopeState constructor overloads selected by the STATIC
    // type of the argument at the call site: the generic, IReadOnlyList<T>-typed constructor (reached
    // by BuildKey/BuildKeys below) always copies; only the internal BuildKeyFromSnapshot/
    // BuildKeysFromSnapshot entry points (production's own, exclusively-owned CaptureLexicalScopeSnapshot
    // result) reach the zero-copy ParameterExpression[][]-typed constructor.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Mirrors <see cref="BuildKey_MutatingCallerListAfterConstruction_DoesNotAffectAlreadyBuiltKey"/> - same
    /// POST-return-only scope, same reasoning for why during-construction mutation is not (and cannot be)
    /// separately exercised here - but with a caller-owned <c>ParameterExpression[][]</c> ARRAY rather than a
    /// <see cref="List{T}"/>: the exact runtime type production's own trusted snapshot also has. Before this
    /// round, <c>BuildKey</c>'s generic entry point stored such an array directly (matching its runtime type
    /// via <c>as</c>), so a caller mutating its own array after the call returned could still corrupt an
    /// already-built key. The generic entry point must now defensively copy this input exactly like any other
    /// <see cref="IReadOnlyList{T}"/> shape, regardless of its runtime type.
    /// </summary>
    [TestMethod]
    public void BuildKey_MutatingCallerArrayAfterConstruction_DoesNotAffectAlreadyBuiltKey()
    {
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        ParameterExpression[][] mutableArrayScopes = [[p]];

        object keyBuiltBeforeMutation = InvokeBuildKey(p, mutableArrayScopes);

        // Mutate the caller's own array AFTER BuildKey has already returned: replace its only frame with one
        // that does not bind p at all, so a corrupted (aliased) snapshot would resolve p as free instead of
        // as a depth-0/position-0 bound parameter.
        mutableArrayScopes[0] = [Expression.Parameter(typeof(double), "unrelated")];

        object referenceKey = InvokeBuildKey(p, new ParameterExpression[][] { new[] { p } });

        int comparison = (int)KeyNodeCompareToMethod.Invoke(keyBuiltBeforeMutation, [referenceKey])!;
        Assert.AreEqual(0, comparison,
            "A key already built from a caller-supplied ParameterExpression[][] array via the generic BuildKey " +
            "entry point must remain structurally correct (still a bound parameter at depth 0, position 0) " +
            "even after the caller later replaces that same array's only frame.");
    }

    /// <summary>
    /// <c>BuildKeyFromSnapshot</c> (the zero-copy, trusted-snapshot entry point production actually calls) must
    /// resolve the SAME bound-parameter key as the generic, defensive-copying <c>BuildKey</c> entry point for
    /// the same logical input - the two constructors <c>ScopeState</c> now exposes must remain
    /// behaviorally interchangeable for correctly-behaving callers, differing only in whether they copy.
    /// </summary>
    [TestMethod]
    public void BuildKeyFromSnapshot_ProducesSameKeyAsGenericBuildKey_ForTheSameBoundParameter()
    {
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        ParameterExpression[][] snapshotScopes = [[p]];

        object keyViaSnapshot = InvokeBuildKeyFromSnapshot(p, snapshotScopes);
        object keyViaGeneric = InvokeBuildKey(p, new ParameterExpression[][] { new[] { p } });

        int forward = (int)KeyNodeCompareToMethod.Invoke(keyViaSnapshot, [keyViaGeneric])!;
        int backward = (int)KeyNodeCompareToMethod.Invoke(keyViaGeneric, [keyViaSnapshot])!;

        Assert.AreEqual(0, forward,
            "BuildKeyFromSnapshot must produce the same bound-parameter key as the generic BuildKey entry point.");
        Assert.AreEqual(0, backward, "The comparison must be symmetric for a tie.");
    }
}
