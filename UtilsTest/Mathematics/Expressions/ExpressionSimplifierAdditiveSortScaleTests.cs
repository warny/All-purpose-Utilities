using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S5 ("Construction-performance cleanup" — see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>), specifically its P1 change:
/// <c>ExpressionSimplifier.CanonicalizeAdditiveExpression</c>'s primary sort comparator now consumes
/// structural keys precomputed once per term (<c>AnnotatedAdditiveTerm.Key</c>/<c>ArgumentKeys</c>) instead
/// of rebuilding an <c>ExpressionCanonicalOrder.KeyNode</c> tree from scratch on every pairwise comparison.
/// </summary>
/// <remarks>
/// These tests deliberately use larger term counts (16+) than the S4 regression suite
/// (<see cref="ExpressionSimplifierStructuralCanonicalizationTests"/>) to exercise the O(n log n) sort path
/// this stage optimizes, and assert the exact SAME canonical result the pre-S5 baseline produces: S5 must
/// not change the S4 canonical form, only how cheaply it is computed.
/// </remarks>
/// <remarks>
/// Tests 1-3 reflect directly into the private <c>CanonicalizeAdditiveExpression</c> (the exact method S5's
/// P1 change modifies) rather than going through the public <c>Simplify(Expression)</c> pipeline: a raw,
/// deeply-nested N-term source expression makes <c>Transform</c>'s bottom-up traversal canonicalize EVERY
/// nesting level, and pre-existing, S4-review-documented factoring rules (e.g. <c>AdditionOfEqualsElements</c>)
/// call the PUBLIC <see cref="ExpressionComparer"/>, which itself re-invokes <c>Simplify()</c> on operands -
/// a real, but entirely unrelated, pre-existing construction-cost multiplier this stage's roadmap explicitly
/// keeps out of scope. Reflecting directly into the method under test isolates exactly the code this stage
/// changed, matching this project's existing precedent for exercising an internal method directly (see
/// <see cref="ExpressionSimplifierStructuralCanonicalizationTests"/>'s own reflection-based
/// <c>ExpressionCanonicalOrder.Compare</c>/<c>CompareType</c>/<c>CompareMethod</c> coverage). Tests 4-5 use
/// the public <c>Simplify(Expression)</c> path directly, at a term count empirically confirmed to run in
/// well under a second.
/// </remarks>
[TestClass]
public class ExpressionSimplifierAdditiveSortScaleTests
{
    /// <summary>The <see cref="double.Sin(double)"/> method, reflected once for reuse across tests.</summary>
    private static readonly MethodInfo SinMethod = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!;

    /// <summary>The private <c>ExpressionSimplifier.CanonicalizeAdditiveExpression(ExpressionType, Expression, Expression)</c> method under test.</summary>
    private static readonly MethodInfo CanonicalizeAdditiveExpressionMethod = typeof(ExpressionSimplifier).GetMethod(
        "CanonicalizeAdditiveExpression", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>The internal <c>ExpressionSimplifier.OnEnterLambdaScope</c> hook, used to simulate being inside a lambda's bound-parameter scope without going through the full <c>Simplify</c> pipeline.</summary>
    private static readonly MethodInfo OnEnterLambdaScopeMethod = typeof(ExpressionSimplifier).GetMethod(
        "OnEnterLambdaScope", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>The internal <c>ExpressionSimplifier.OnExitLambdaScope</c> hook, paired with <see cref="OnEnterLambdaScopeMethod"/>.</summary>
    private static readonly MethodInfo OnExitLambdaScopeMethod = typeof(ExpressionSimplifier).GetMethod(
        "OnExitLambdaScope", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>Invokes the private <c>CanonicalizeAdditiveExpression(ExpressionType.Add, left, right)</c> directly on <paramref name="simplifier"/>.</summary>
    /// <param name="simplifier">The simplifier instance to invoke the method on.</param>
    /// <param name="left">The additive expression's left branch.</param>
    /// <param name="right">The additive expression's right branch.</param>
    /// <returns>The canonicalized additive expression.</returns>
    private static Expression InvokeCanonicalizeAdditive(ExpressionSimplifier simplifier, Expression left, Expression right)
        => (Expression)CanonicalizeAdditiveExpressionMethod.Invoke(simplifier, [ExpressionType.Add, left, right])!;

    /// <summary>Pushes <paramref name="parameters"/> as a bound lexical scope frame, via <see cref="OnEnterLambdaScopeMethod"/>.</summary>
    /// <param name="simplifier">The simplifier instance whose ambient scope stack to push onto.</param>
    /// <param name="parameters">The parameters to treat as bound at this scope frame.</param>
    private static void EnterScope(ExpressionSimplifier simplifier, ParameterExpression[] parameters)
        => OnEnterLambdaScopeMethod.Invoke(simplifier, [null!, parameters]);

    /// <summary>Pops the scope frame pushed by <see cref="EnterScope"/>, via <see cref="OnExitLambdaScopeMethod"/>.</summary>
    /// <param name="simplifier">The simplifier instance whose ambient scope stack to pop from.</param>
    /// <param name="parameters">The parameters previously pushed by <see cref="EnterScope"/>.</param>
    private static void ExitScope(ExpressionSimplifier simplifier, ParameterExpression[] parameters)
        => OnExitLambdaScopeMethod.Invoke(simplifier, [null!, parameters]);

    /// <summary>
    /// Splits an ordered term list into the <c>(left, right)</c> shape <c>CanonicalizeAdditiveExpression</c>
    /// expects: the first term, and an ordinary nested <see cref="ExpressionType.Add"/> chain of the rest
    /// (which <c>CollectAdditiveTerms</c> flattens back out internally).
    /// </summary>
    /// <param name="terms">The terms, in source order.</param>
    /// <returns>The <c>(left, right)</c> split.</returns>
    private static (Expression Left, Expression Right) SplitForDirectAdditiveCall(IReadOnlyList<Expression> terms)
    {
        if (terms.Count == 1) return (terms[0], terms[0]);
        Expression right = terms[^1];
        for (int i = terms.Count - 2; i >= 1; i--)
        {
            right = Expression.Add(terms[i], right);
        }
        return (terms[0], right);
    }

    /// <summary>Flattens a canonicalized, strictly non-negative, right-associated additive chain (<c>t0 + (t1 + (t2 + ...))</c>) into an ordered list of exactly <paramref name="expectedCount"/> TOP-LEVEL terms/groups.</summary>
    /// <param name="root">The canonicalized additive expression to flatten.</param>
    /// <param name="expectedCount">
    /// The expected number of top-level terms/groups (<c>BuildRightAssociative</c>'s own <c>rebuiltTerms.Count</c>).
    /// Required because the walk cannot otherwise tell "one more link in the right-associative chain" apart
    /// from "the last group's own combined sub-expression", when that last group itself happens to combine
    /// more than one term (see <see cref="LargePowerWrappedTermSet_GroupsByArgumentThenTieBreaksOnExponent"/>,
    /// whose last group is itself an <see cref="ExpressionType.Add"/> node like every chain link): both shapes
    /// are indistinguishable <see cref="BinaryExpression"/> nodes of <see cref="ExpressionType.Add"/>, so only
    /// knowing how many top-level items to stop at resolves the ambiguity.
    /// </param>
    /// <returns>The top-level terms/groups, in left-to-right (canonical) order.</returns>
    /// <remarks>Only valid for a chain built entirely from non-negative terms: <see cref="ExpressionSimplifier"/> rebuilds a negative term's group via <see cref="ExpressionType.Subtract"/>, which this simple walk does not unwrap.</remarks>
    private static List<Expression> FlattenPositiveAdditiveChain(Expression root, int expectedCount)
    {
        var result = new List<Expression>();
        Expression current = root;
        while (result.Count < expectedCount - 1 && current is BinaryExpression be && be.NodeType == ExpressionType.Add)
        {
            result.Add(be.Left);
            current = be.Right;
        }
        result.Add(current);
        return result;
    }

    // ------------------------------------------------------------------------------------------
    // 1. Bound function-like terms (the ArgumentKeys precomputation path)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Sixteen <c>Sin(p_i)</c> terms, supplied in reverse declaration order, must canonicalize to ascending
    /// declaration-position order. This exercises exactly the code path S5's P1 change touches for a
    /// function-like term: <c>CompareAdditiveGroupingOrder</c>'s primary sort now compares each term's
    /// precomputed <c>ArgumentKeys</c> (built once via <c>ExpressionCanonicalOrder.BuildKeys</c>) instead of
    /// rebuilding <c>ExpressionCanonicalOrder.Compare</c> on the raw arguments for every pairwise comparison.
    /// </summary>
    [TestMethod]
    public void LargeBoundFunctionLikeTermSet_OrdersByArgumentDeclarationPosition()
    {
        const int n = 16;
        var simplifier = new ExpressionSimplifier();
        var parameters = new ParameterExpression[n];
        for (int i = 0; i < n; i++) parameters[i] = Expression.Parameter(typeof(double), $"p{i}");

        var terms = new Expression[n];
        for (int i = 0; i < n; i++) terms[i] = Expression.Call(SinMethod, parameters[n - 1 - i]);

        Expression result;
        EnterScope(simplifier, parameters);
        try
        {
            (Expression left, Expression right) = SplitForDirectAdditiveCall(terms);
            result = InvokeCanonicalizeAdditive(simplifier, left, right);
        }
        finally
        {
            ExitScope(simplifier, parameters);
        }

        List<Expression> ordered = FlattenPositiveAdditiveChain(result, n);
        Assert.AreEqual(n, ordered.Count);
        for (int i = 0; i < n; i++)
        {
            var call = (MethodCallExpression)ordered[i];
            Assert.AreEqual(SinMethod, call.Method);
            Assert.IsTrue(ReferenceEquals(parameters[i], call.Arguments[0]),
                $"Position {i} must hold Sin(p{i}) after canonicalization, but held a call on a different parameter.");
        }
    }

    // ------------------------------------------------------------------------------------------
    // 2. Bound opaque terms (the Key-reuse path)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Sixteen bare bound parameters used directly as opaque additive terms, supplied in reverse declaration
    /// order, must canonicalize to ascending declaration-position order. This exercises the OPAQUE branch of
    /// S5's P1 change: <c>CompareAdditiveGroupingOrder</c> now reuses <c>AnnotatedAdditiveTerm.Key</c>
    /// directly (the term's own precomputed complete key) instead of rebuilding
    /// <c>ExpressionCanonicalOrder.Compare(term, term, scopes)</c> from scratch for every pairwise comparison.
    /// </summary>
    [TestMethod]
    public void LargeOpaqueBoundTermSet_OrdersByDeclarationPosition()
    {
        const int n = 16;
        var simplifier = new ExpressionSimplifier();
        var parameters = new ParameterExpression[n];
        for (int i = 0; i < n; i++) parameters[i] = Expression.Parameter(typeof(double), $"p{i}");

        var terms = new Expression[n];
        for (int i = 0; i < n; i++) terms[i] = parameters[n - 1 - i];

        Expression result;
        EnterScope(simplifier, parameters);
        try
        {
            (Expression left, Expression right) = SplitForDirectAdditiveCall(terms);
            result = InvokeCanonicalizeAdditive(simplifier, left, right);
        }
        finally
        {
            ExitScope(simplifier, parameters);
        }

        List<Expression> ordered = FlattenPositiveAdditiveChain(result, n);
        Assert.AreEqual(n, ordered.Count);
        for (int i = 0; i < n; i++)
        {
            Assert.IsTrue(ReferenceEquals(parameters[i], ordered[i]),
                $"Position {i} must hold p{i} after canonicalization, but held a different parameter.");
        }
    }

    // ------------------------------------------------------------------------------------------
    // 3. Power-wrapped terms: grouping ignores the exponent, the complete-key tie-break does not
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Eight pairs of <c>Sin(p_i)^2</c>/<c>Sin(p_i)^3</c>, supplied with both the argument order AND the
    /// within-pair exponent order reversed from the expected canonical result, must still canonicalize so
    /// that: (a) terms cluster by argument declaration position into one combined per-argument group (the
    /// coarse, exponent-ignoring additive grouping S4 deliberately applies to same-argument function-like
    /// terms — see <see cref="ExpressionSimplifier"/>'s "Additive grouping" remarks — which S5's
    /// ArgumentKeys precomputation now serves without changing), and (b) within each group, the lower
    /// exponent sorts first (the complete-key <c>.ThenBy(term.Key)</c> tie-break, which still distinguishes
    /// the exponent).
    /// </summary>
    [TestMethod]
    public void LargePowerWrappedTermSet_GroupsByArgumentThenTieBreaksOnExponent()
    {
        const int pairs = 8;
        var simplifier = new ExpressionSimplifier();
        var parameters = new ParameterExpression[pairs];
        for (int i = 0; i < pairs; i++) parameters[i] = Expression.Parameter(typeof(double), $"p{i}");

        var terms = new List<Expression>();
        for (int i = pairs - 1; i >= 0; i--)
        {
            Expression call = Expression.Call(SinMethod, parameters[i]);
            terms.Add(Expression.Power(call, Expression.Constant(3.0)));
            terms.Add(Expression.Power(call, Expression.Constant(2.0)));
        }

        Expression result;
        EnterScope(simplifier, parameters);
        try
        {
            (Expression left, Expression right) = SplitForDirectAdditiveCall(terms);
            result = InvokeCanonicalizeAdditive(simplifier, left, right);
        }
        finally
        {
            ExitScope(simplifier, parameters);
        }

        List<Expression> groups = FlattenPositiveAdditiveChain(result, pairs);
        Assert.AreEqual(pairs, groups.Count);

        for (int i = 0; i < pairs; i++)
        {
            var groupAdd = (BinaryExpression)groups[i];
            Assert.AreEqual(ExpressionType.Add, groupAdd.NodeType);

            var square = (BinaryExpression)groupAdd.Left;
            var cube = (BinaryExpression)groupAdd.Right;
            Assert.AreEqual(ExpressionType.Power, square.NodeType);
            Assert.AreEqual(ExpressionType.Power, cube.NodeType);

            var squareCall = (MethodCallExpression)square.Left;
            var cubeCall = (MethodCallExpression)cube.Left;
            Assert.IsTrue(ReferenceEquals(parameters[i], squareCall.Arguments[0]), $"Group {i} must be Sin(p{i})'s group.");
            Assert.IsTrue(ReferenceEquals(parameters[i], cubeCall.Arguments[0]), $"Group {i} must be Sin(p{i})'s group.");

            Assert.AreEqual(2.0, (double)((ConstantExpression)square.Right).Value!, $"Group {i}'s first term must be the square (lower exponent).");
            Assert.AreEqual(3.0, (double)((ConstantExpression)cube.Right).Value!, $"Group {i}'s second term must be the cube (higher exponent).");
        }
    }

    // ------------------------------------------------------------------------------------------
    // 4. Free parameters: stable sort must survive the refactor when every dimension ties
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Sixteen distinct FREE parameters (not declared by any enclosing lambda) of the same type, added in
    /// reverse order. Every canonical-order dimension ties for two distinct free parameters of the same type
    /// (see <c>ExpressionCanonicalOrder</c>'s "Free parameters" policy), so the ONLY thing that can decide
    /// this shape's final order is the caller's STABLE sort — exactly the invariant the roadmap warns is easy
    /// to silently break when refactoring <c>OrderBy</c>'s key-selector/comparator plumbing. Uses the public
    /// <c>Simplify(Expression)</c> path directly (empirically well under a second at this term count).
    /// </summary>
    [TestMethod]
    public void LargeFreeParameterTermSet_PreservesStableSourceOrder()
    {
        const int n = 16;
        var simplifier = new ExpressionSimplifier();
        var parameters = new ParameterExpression[n];
        for (int i = 0; i < n; i++) parameters[i] = Expression.Parameter(typeof(double), $"p{i}");

        // Left-associated chain, built with the highest-indexed parameter innermost: CollectAdditiveTerms's
        // left-then-right recursion visits (and therefore the stable sort must preserve) p[n-1], p[n-2], ...,
        // p[0], in that order.
        Expression body = parameters[n - 1];
        for (int i = n - 2; i >= 0; i--)
        {
            body = Expression.Add(body, parameters[i]);
        }

        Expression result = simplifier.Simplify(body);
        List<Expression> terms = FlattenPositiveAdditiveChain(result, n);

        Assert.AreEqual(n, terms.Count);
        for (int i = 0; i < n; i++)
        {
            Assert.IsTrue(ReferenceEquals(parameters[n - 1 - i], terms[i]),
                $"Position {i} must preserve CollectAdditiveTerms' visitation (source) order for tied free parameters.");
        }
    }

    // ------------------------------------------------------------------------------------------
    // 5. Adversarial content at scale: no user code, no ToString, ever
    // ------------------------------------------------------------------------------------------

    /// <summary>Marker exception used by <see cref="HostileConstant"/> to prove its members are never invoked.</summary>
    private sealed class HostileConstantException : Exception { }

    /// <summary>A reference-type constant value whose <see cref="object.Equals(object?)"/> and <see cref="object.GetHashCode"/> overrides both record a call and throw.</summary>
    private sealed class HostileConstant
    {
        /// <summary>The number of times <see cref="Equals(object?)"/> has been called.</summary>
        public int EqualsCallCount { get; private set; }

        /// <summary>The number of times <see cref="GetHashCode"/> has been called.</summary>
        public int GetHashCodeCallCount { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            EqualsCallCount++;
            throw new HostileConstantException();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            GetHashCodeCallCount++;
            throw new HostileConstantException();
        }
    }

    /// <summary>Test-only static method accepting an opaque <see cref="object"/> argument, used to embed a <see cref="HostileConstant"/> as a method-call argument participating in additive grouping.</summary>
    /// <param name="value">An arbitrary opaque value, ignored.</param>
    /// <returns>A constant, arbitrary result.</returns>
    private static double IdentityFromObject(object value) => 1.0;

    /// <summary>Marker exception used by <see cref="ThrowingExpression"/> to prove its <see cref="Expression.ToString"/> is never invoked.</summary>
    private sealed class ToStringProbeException : Exception { }

    /// <summary>A non-reducible <see cref="ExpressionType.Extension"/> node whose <see cref="ToString"/> throws and records a call count.</summary>
    private sealed class ThrowingExpression : Expression
    {
        /// <summary>The number of times <see cref="ToString"/> has been called.</summary>
        public int ToStringCallCount { get; private set; }

        /// <inheritdoc/>
        public override Type Type => typeof(double);

        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override bool CanReduce => false;

        /// <summary>Records a call in <see cref="ToStringCallCount"/>, then throws <see cref="ToStringProbeException"/>.</summary>
        /// <returns>Never returns.</returns>
        /// <exception cref="ToStringProbeException">Always thrown.</exception>
        public override string ToString()
        {
            ToStringCallCount++;
            throw new ToStringProbeException();
        }
    }

    /// <summary>
    /// A moderate-size (12-term) additive expression, padded with ordinary bound function-like terms, plus
    /// one <see cref="HostileConstant"/>-carrying method-call argument (repeated as the SAME instance, to
    /// force a grouping-equality hash collision and therefore an actual equality check) and one
    /// <see cref="ThrowingExpression"/> unsupported term, must canonicalize successfully through the public
    /// <c>Simplify(Expression)</c> pipeline without ever calling <see cref="HostileConstant.Equals(object?)"/>,
    /// <see cref="HostileConstant.GetHashCode"/>, or <see cref="ThrowingExpression.ToString"/>. Directly
    /// exercises S5's precomputed <c>ArgumentKeys</c>/<c>Key</c> construction path
    /// (<c>ExpressionCanonicalOrder.BuildKey(s)</c>) with this adversarial content, since that construction
    /// happens once per term regardless of scale.
    /// </summary>
    [TestMethod]
    public void LargeAdversarialTermSet_NeverExecutesUserEqualsGetHashCodeOrToString()
    {
        var simplifier = new ExpressionSimplifier();
        var hostile = new HostileConstant();
        var throwing = new ThrowingExpression();
        MethodInfo identity = typeof(ExpressionSimplifierAdditiveSortScaleTests).GetMethod(
            nameof(IdentityFromObject), BindingFlags.NonPublic | BindingFlags.Static)!;

        var parameters = new ParameterExpression[8];
        for (int i = 0; i < parameters.Length; i++) parameters[i] = Expression.Parameter(typeof(double), $"p{i}");

        Expression body = throwing;
        foreach (ParameterExpression p in parameters)
        {
            body = Expression.Add(body, Expression.Call(SinMethod, p));
        }
        body = Expression.Add(body, Expression.Call(identity, Expression.Constant(hostile, typeof(object))));
        body = Expression.Add(body, Expression.Call(identity, Expression.Constant(hostile, typeof(object))));

        var lambda = Expression.Lambda(body, parameters);

        Expression result = simplifier.Simplify(lambda);

        Assert.AreEqual(0, hostile.EqualsCallCount, "No additive-grouping/ordering step may call a constant value's own Equals override.");
        Assert.AreEqual(0, hostile.GetHashCodeCallCount, "No additive-grouping/ordering step may call a constant value's own GetHashCode override.");
        Assert.AreEqual(0, throwing.ToStringCallCount, "No additive-grouping/ordering step may call Expression.ToString().");
        Assert.IsNotNull(result);
    }
}
