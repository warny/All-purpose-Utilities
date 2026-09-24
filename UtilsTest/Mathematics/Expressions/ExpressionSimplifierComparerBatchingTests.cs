using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S5's "P3" change (see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>, stage S5): <c>ExpressionSimplifier</c>'s
/// high-fan-out factoring rules (<c>AdditionOfEqualsElements</c>, <c>SubstractionOfEqualsElements</c>) now
/// reuse a per-invocation <c>FactorEqualityProbe</c> so each of the up to four candidate operands is
/// simplified via the PUBLIC <see cref="ExpressionComparer"/> contract at most once per rule call, instead
/// of once per pairwise comparison it participates in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Second-pass characterization (the hazard this change must not break).</b> A matching rule can return
/// a newly-built expression that <c>TransformCore</c> returns immediately, without re-simplifying it to a
/// fixed point (see <see cref="ExpressionSimplifier"/>'s own remarks). When that not-yet-fixed-point
/// expression later participates in another rule's equality check via the PUBLIC
/// <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/>, the comparer's OWN internal
/// <c>Simplify()</c> call on each operand is what actually recognizes the equality - a "second pass" the
/// naive alternative, <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>, does
/// NOT perform (it never simplifies its operands, and uses the safe-constant policy instead of the public
/// comparer's non-safe one - see that method's remarks and this roadmap stage's "Critical warning").
/// <see cref="SecondPassAdd_UnfoldedCoefficient_RecognizesEqualityAfterSimplifying"/> and its sibling tests
/// below reproduce this exact mechanism directly against <c>AdditionOfEqualsElements</c>/
/// <c>SubstractionOfEqualsElements</c> (the two rules actually modified by this stage), by hand-building an
/// operand that already contains a not-yet-folded <c>Add</c>/<c>Subtract</c> coefficient - exactly the shape
/// these two rules themselves produce when one is fed the OTHER's unfixed-point return value in a deeper
/// source expression (see <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c> for the worked
/// <c>((2*x + 3*x) + 5*z)</c> end-to-end example this direct-reflection shape is drawn from). Manually
/// verified (once, during development, not automated here) that
/// <see cref="SecondPassAdd_UnfoldedCoefficient_RecognizesEqualityAfterSimplifying"/> FAILS when
/// <c>AdditionOfEqualsElements</c>'s four comparisons are naively substituted with
/// <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/> (the rule then returns
/// <see langword="null"/> instead of factoring, because <c>StructuralEqualsRaw</c> never folds the
/// <c>Add(2, 3)</c> coefficient before comparing it to <c>Constant(5)</c>) - proving this test guards the
/// exact distinction this stage's "Critical warning" describes.
/// </para>
/// <para>
/// <b>Why direct reflection, not the public <c>Simplify(Expression)</c> pipeline.</b> Reproducing the
/// second-pass shape through the full bottom-up <c>Transform</c> pipeline requires several levels of
/// specific nesting and is sensitive to unrelated sibling rules (<c>SubstractionWithNegate</c>,
/// <c>MultiplicationWithZeroOrOne</c>, ...) intercepting the shape before it reaches the rule under test.
/// Reflecting directly into the protected <c>AdditionOfEqualsElements</c>/<c>SubstractionOfEqualsElements</c>
/// methods (matching this project's own precedent - see
/// <see cref="ExpressionSimplifierAdditiveSortScaleTests"/>'s reflection into
/// <c>CanonicalizeAdditiveExpression</c>) isolates exactly the code this stage changed and removes that
/// fragility; <see cref="BoundLambdaEndToEnd_SecondPassFactoring_ProducesSameShapeAsFreeParameters"/> below
/// separately proves the same shape is reachable through the public pipeline for a bound-lambda scenario.
/// </para>
/// </remarks>
[TestClass]
public class ExpressionSimplifierComparerBatchingTests
{
    /// <summary>The protected <c>ExpressionSimplifier.AdditionOfEqualsElements(BinaryExpression, Expression, Expression)</c> method under test.</summary>
    private static readonly MethodInfo AdditionOfEqualsElementsMethod = typeof(ExpressionSimplifier).GetMethod(
        "AdditionOfEqualsElements", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>The protected <c>ExpressionSimplifier.SubstractionOfEqualsElements(BinaryExpression, Expression, Expression)</c> method under test.</summary>
    private static readonly MethodInfo SubstractionOfEqualsElementsMethod = typeof(ExpressionSimplifier).GetMethod(
        "SubstractionOfEqualsElements", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>Invokes <see cref="AdditionOfEqualsElementsMethod"/> directly on <paramref name="simplifier"/>.</summary>
    /// <param name="simplifier">The simplifier instance to invoke the method on.</param>
    /// <param name="left">The addition's left operand.</param>
    /// <param name="right">The addition's right operand.</param>
    /// <returns>The rule's result, or <see langword="null"/> if it did not fire.</returns>
    private static Expression? InvokeAdditionOfEqualsElements(ExpressionSimplifier simplifier, Expression left, Expression right)
        => (Expression?)AdditionOfEqualsElementsMethod.Invoke(simplifier, [Expression.Add(left, right), left, right]);

    /// <summary>Invokes <see cref="SubstractionOfEqualsElementsMethod"/> directly on <paramref name="simplifier"/>.</summary>
    /// <param name="simplifier">The simplifier instance to invoke the method on.</param>
    /// <param name="left">The subtraction's left operand.</param>
    /// <param name="right">The subtraction's right operand.</param>
    /// <returns>The rule's result, or <see langword="null"/> if it did not fire.</returns>
    private static Expression? InvokeSubstractionOfEqualsElements(ExpressionSimplifier simplifier, Expression left, Expression right)
        => (Expression?)SubstractionOfEqualsElementsMethod.Invoke(simplifier, [Expression.Subtract(left, right), left, right]);

    // ------------------------------------------------------------------------------------------
    // Second-pass dependency: the rule's own equality probe must still recognize an unfolded
    // coefficient's value, exactly like the pre-existing per-comparison ExpressionComparer.Default.Equals
    // calls this stage replaced.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>AdditionOfEqualsElements(Add((2+3)*x, 5*z))</c>, where <c>(2+3)</c> is a hand-built, NOT-yet-folded
    /// <see cref="ExpressionType.Add"/> node (never a <see cref="ConstantExpression"/>): the rule must still
    /// recognize <c>(2+3)</c> and <c>5</c> as the same coefficient (after simplifying <c>(2+3)</c> to
    /// <c>5</c>) and factor to <c>(x+z) * (2+3)</c>, swapping so the recognized-equal coefficients become the
    /// new <c>Add</c>'s operands. See this class's remarks for why <c>(2+3)</c> is built this way and why the
    /// naive <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/> substitution
    /// would instead return <see langword="null"/> here (manually verified, not automated).
    /// </summary>
    [TestMethod]
    public void SecondPassAdd_UnfoldedCoefficient_RecognizesEqualityAfterSimplifying()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");

        Expression unfoldedCoefficient = Expression.Add(Expression.Constant(2.0), Expression.Constant(3.0));
        Expression left = Expression.Multiply(unfoldedCoefficient, x);
        Expression right = Expression.Multiply(Expression.Constant(5.0), z);

        Expression? result = InvokeAdditionOfEqualsElements(simplifier, left, right);

        Assert.IsNotNull(result, "The rule must recognize (2+3) and 5 as equal after simplifying the unfolded coefficient.");
        var multiply = (BinaryExpression)result!;
        Assert.AreEqual(ExpressionType.Multiply, multiply.NodeType);
        var sumOfVariables = (BinaryExpression)multiply.Left;
        Assert.AreEqual(ExpressionType.Add, sumOfVariables.NodeType);
        Assert.IsTrue(ReferenceEquals(x, sumOfVariables.Left));
        Assert.IsTrue(ReferenceEquals(z, sumOfVariables.Right));
        Assert.IsTrue(ReferenceEquals(unfoldedCoefficient, multiply.Right), "The still-unfolded (2+3) coefficient must survive as the factored-out term, unmodified by the equality probe itself.");
    }

    /// <summary>
    /// Subtraction analogue of <see cref="SecondPassAdd_UnfoldedCoefficient_RecognizesEqualityAfterSimplifying"/>:
    /// <c>SubstractionOfEqualsElements(Subtract((2-3)*x, (-1)*z))</c> must recognize <c>(2-3)</c> and
    /// <c>-1</c> as equal after simplification and factor to <c>(x-z) * (2-3)</c>.
    /// </summary>
    [TestMethod]
    public void SecondPassSubtract_UnfoldedCoefficient_RecognizesEqualityAfterSimplifying()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");

        Expression unfoldedCoefficient = Expression.Subtract(Expression.Constant(2.0), Expression.Constant(3.0));
        Expression left = Expression.Multiply(unfoldedCoefficient, x);
        Expression right = Expression.Multiply(Expression.Constant(-1.0), z);

        Expression? result = InvokeSubstractionOfEqualsElements(simplifier, left, right);

        Assert.IsNotNull(result, "The rule must recognize (2-3) and -1 as equal after simplifying the unfolded coefficient.");
        var multiply = (BinaryExpression)result!;
        Assert.AreEqual(ExpressionType.Multiply, multiply.NodeType);
        var differenceOfVariables = (BinaryExpression)multiply.Left;
        Assert.AreEqual(ExpressionType.Subtract, differenceOfVariables.NodeType);
        Assert.IsTrue(ReferenceEquals(x, differenceOfVariables.Left));
        Assert.IsTrue(ReferenceEquals(z, differenceOfVariables.Right));
        Assert.IsTrue(ReferenceEquals(unfoldedCoefficient, multiply.Right));
    }

    /// <summary>
    /// The final cancellation check in <c>SubstractionOfEqualsElements</c> (<c>if (Equals(leftleft,
    /// rightleft)) return 0;</c>, reached AFTER the swap performed by the earlier branches) must also
    /// recognize an unfolded coefficient: <c>SubstractionOfEqualsElements(Subtract((2-3)*x, (-1)*x))</c>
    /// must fully cancel to the numeric constant <c>0</c>, not fall through to a <c>Multiply(Subtract(...),
    /// ...)</c> factoring result.
    /// </summary>
    [TestMethod]
    public void SecondPassSubtract_UnfoldedCoefficient_CancelsToZero()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        Expression unfoldedCoefficient = Expression.Subtract(Expression.Constant(2.0), Expression.Constant(3.0));
        Expression left = Expression.Multiply(unfoldedCoefficient, x);
        Expression right = Expression.Multiply(Expression.Constant(-1.0), x);

        Expression? result = InvokeSubstractionOfEqualsElements(simplifier, left, right);

        Assert.IsNotNull(result);
        var constant = (ConstantExpression)result!;
        Assert.AreEqual(0.0, (double)constant.Value!);
    }

    /// <summary>
    /// End-to-end (public <c>Simplify(Expression)</c>) confirmation that the second-pass factoring shape
    /// from <see cref="SecondPassAdd_UnfoldedCoefficient_RecognizesEqualityAfterSimplifying"/> is reachable
    /// through ordinary nested source expressions, not only via direct reflection: <c>(2*x + 3*x) + 5*z</c>
    /// simplifies to <c>(x + z) * (2 + 3)</c>, because the inner <c>2*x + 3*x</c> factors first (to the
    /// still-unfolded <c>(2+3)*x</c>, per <c>TransformCore</c> "returns immediately" - see this class's
    /// remarks) and that unfixed-point result becomes the outer <c>AdditionOfEqualsElements</c> call's own
    /// <c>left</c> operand.
    /// </summary>
    [TestMethod]
    public void EndToEnd_NestedAdditionOfEqualsElements_ProducesSecondPassFactoredShape()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");

        Expression source = Expression.Add(
            Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Multiply(Expression.Constant(3.0), x)),
            Expression.Multiply(Expression.Constant(5.0), z));

        Expression result = simplifier.Simplify(source);

        var multiply = (BinaryExpression)result;
        Assert.AreEqual(ExpressionType.Multiply, multiply.NodeType);
        var sumOfVariables = (BinaryExpression)multiply.Left;
        Assert.AreEqual(ExpressionType.Add, sumOfVariables.NodeType);
        Assert.IsTrue(ReferenceEquals(x, sumOfVariables.Left));
        Assert.IsTrue(ReferenceEquals(z, sumOfVariables.Right));
        var coefficient = (BinaryExpression)multiply.Right;
        Assert.AreEqual(ExpressionType.Add, coefficient.NodeType);
        Assert.AreEqual(2.0, (double)((ConstantExpression)coefficient.Left).Value!);
        Assert.AreEqual(3.0, (double)((ConstantExpression)coefficient.Right).Value!);
    }

    // ------------------------------------------------------------------------------------------
    // Ordinary factor-match and near-miss branches (positive/negative controls)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Positive control: the ordinary, no-second-pass-needed shape <c>2*x + 3*x</c> (with the SAME
    /// <see cref="ParameterExpression"/> instance used on both sides) must still factor to
    /// <c>(2+3)*x</c>, exactly like before this stage's change.
    /// </summary>
    [TestMethod]
    public void OrdinaryFactorMatch_SameReferenceVariable_StillFactors()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression left = Expression.Multiply(Expression.Constant(2.0), x);
        Expression right = Expression.Multiply(Expression.Constant(3.0), x);

        Expression? result = InvokeAdditionOfEqualsElements(simplifier, left, right);

        Assert.IsNotNull(result);
        var multiply = (BinaryExpression)result!;
        Assert.AreEqual(ExpressionType.Multiply, multiply.NodeType);
        Assert.IsTrue(ReferenceEquals(x, multiply.Right));
        var coefficient = (BinaryExpression)multiply.Left;
        Assert.AreEqual(2.0, (double)((ConstantExpression)coefficient.Left).Value!);
        Assert.AreEqual(3.0, (double)((ConstantExpression)coefficient.Right).Value!);
    }

    /// <summary>
    /// Negative control: when none of the four candidate comparisons match (different coefficients AND
    /// different variables on each side), <c>AdditionOfEqualsElements</c> must return <see langword="null"/>,
    /// exactly like before this stage's change.
    /// </summary>
    [TestMethod]
    public void NearMiss_NoCandidateMatches_ReturnsNull()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Expression left = Expression.Multiply(Expression.Constant(2.0), a);
        Expression right = Expression.Multiply(Expression.Constant(3.0), b);

        Expression? result = InvokeAdditionOfEqualsElements(simplifier, left, right);

        Assert.IsNull(result);
    }

    // ------------------------------------------------------------------------------------------
    // Unsupported-node-kind conservatism/reflexivity
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Same-instance reflexivity for an unsupported node kind (<see cref="ConditionalExpression"/>, not one
    /// of the seven structural kinds <see cref="ExpressionComparer"/> understands): using the EXACT SAME
    /// <see cref="ConditionalExpression"/> instance as both <c>leftright</c> and <c>rightright</c> must still
    /// factor successfully, because the top-level <see cref="ExpressionComparer.TryFastPathEquals"/> check
    /// (reused unchanged by the probe) recognizes reference equality before ever attempting a structural
    /// comparison this comparer does not understand.
    /// </summary>
    [TestMethod]
    public void UnsupportedNodeKind_SameInstance_StillFactorsViaReferenceFastPath()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression test = Expression.Parameter(typeof(bool), "test");
        Expression sharedUnsupported = Expression.Condition(test, Expression.Constant(1.0), Expression.Constant(2.0));

        Expression left = Expression.Multiply(Expression.Constant(2.0), sharedUnsupported);
        Expression right = Expression.Multiply(Expression.Constant(3.0), sharedUnsupported);

        Expression? result = InvokeAdditionOfEqualsElements(simplifier, left, right);

        Assert.IsNotNull(result, "Two references to the SAME unsupported-node instance must still be recognized as equal via the reference-equality fast path.");
        var multiply = (BinaryExpression)result!;
        Assert.IsTrue(ReferenceEquals(sharedUnsupported, multiply.Right));
    }

    /// <summary>
    /// Conservatism for an unsupported node kind: two DISTINCT but structurally identical
    /// <see cref="ConditionalExpression"/> instances must NOT be conflated - <c>AdditionOfEqualsElements</c>
    /// must return <see langword="null"/>, exactly like before this stage's change (this comparer's
    /// structural walk does not understand <see cref="ConditionalExpression"/> at all - see
    /// <see cref="ExpressionComparer"/>'s own class remarks - so it conservatively reports two distinct
    /// instances as unequal rather than risk a false positive).
    /// </summary>
    [TestMethod]
    public void UnsupportedNodeKind_DistinctInstances_AreNotConflated()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression test = Expression.Parameter(typeof(bool), "test");
        Expression left = Expression.Multiply(Expression.Constant(2.0), Expression.Condition(test, Expression.Constant(1.0), Expression.Constant(2.0)));
        Expression right = Expression.Multiply(Expression.Constant(3.0), Expression.Condition(test, Expression.Constant(1.0), Expression.Constant(2.0)));

        Expression? result = InvokeAdditionOfEqualsElements(simplifier, left, right);

        Assert.IsNull(result, "Two distinct, structurally-identical unsupported-node instances must not be conflated.");
    }

    // ------------------------------------------------------------------------------------------
    // Opaque/hostile constants: the public, non-safe comparer policy - never StructuralEqualsRaw's safe
    // policy - and exception behavior must both be preserved exactly.
    // ------------------------------------------------------------------------------------------

    /// <summary>Marker exception used by <see cref="HostileConstant"/> to prove its <see cref="Equals(object?)"/> is actually invoked (the non-safe policy), and that the resulting exception propagates unmodified.</summary>
    private sealed class HostileConstantException : Exception { }

    /// <summary>A reference-type constant value whose <see cref="object.Equals(object?)"/> records a call and then throws, so a test can prove it was invoked exactly once and that the exception was not swallowed.</summary>
    private sealed class HostileConstant
    {
        /// <summary>The number of times <see cref="Equals(object?)"/> has been called.</summary>
        public int EqualsCallCount { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            EqualsCallCount++;
            throw new HostileConstantException();
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>Test-only static method accepting an opaque <see cref="object"/> argument, used to embed a <see cref="HostileConstant"/> as a method-call argument inside a numeric-typed additive term.</summary>
    /// <param name="value">An arbitrary opaque value, ignored.</param>
    /// <returns>A constant, arbitrary result.</returns>
    private static double IdentityFromObject(object value) => 1.0;

    /// <summary>
    /// <c>AdditionOfEqualsElements</c> must still use the PUBLIC, non-safe constant policy (never
    /// <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>'s safe-constant
    /// policy): comparing two method-call terms that each embed a DIFFERENT <see cref="HostileConstant"/>
    /// instance must actually invoke <see cref="HostileConstant.Equals(object?)"/> (proving the non-safe
    /// policy is still in effect) exactly once (proving the new per-invocation cache does not cause it to
    /// run MORE than the single, unmodified comparison would have), and the resulting exception must
    /// propagate out of the rule unmodified (preserving pre-existing exception behavior), not be swallowed
    /// or converted into a <see langword="null"/> "rule did not fire" result.
    /// </summary>
    [TestMethod]
    public void HostileConstant_NonSafePolicyInvokesUserEqualsOnceAndPropagatesException()
    {
        var simplifier = new ExpressionSimplifier();
        var hostileLeft = new HostileConstant();
        var hostileRight = new HostileConstant();
        MethodInfo identity = typeof(ExpressionSimplifierComparerBatchingTests).GetMethod(
            nameof(IdentityFromObject), BindingFlags.NonPublic | BindingFlags.Static)!;

        // leftleft/rightleft (2 vs 3) and any leftleft/rightright, leftright/rightleft cross-comparisons
        // never reach the hostile constants (NodeType mismatch short-circuits first); only the final
        // leftright-vs-rightright comparison (both MethodCallExpression, same Method) recurses into the
        // hostile constants' own Equals.
        Expression left = Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(hostileLeft, typeof(object))));
        Expression right = Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(hostileRight, typeof(object))));

        TargetInvocationException thrown = Assert.ThrowsExactly<TargetInvocationException>(
            () => InvokeAdditionOfEqualsElements(simplifier, left, right));

        Assert.IsInstanceOfType<HostileConstantException>(thrown.InnerException);
        Assert.AreEqual(1, hostileLeft.EqualsCallCount, "The hostile constant's Equals must be invoked exactly once - not zero (which would mean the safe policy was silently substituted) and not more than once (which would mean the new cache re-ran the comparison redundantly).");
    }

    // ------------------------------------------------------------------------------------------
    // Bound lambda parameters / S4 scope isolation
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The second-pass factoring shape from
    /// <see cref="EndToEnd_NestedAdditionOfEqualsElements_ProducesSecondPassFactoredShape"/> must produce
    /// the SAME shape when <c>x</c>/<c>z</c> are BOUND lambda parameters instead of free parameters: the S4
    /// ambient lexical-scope stack is open around the whole call, and each nested
    /// <see cref="ExpressionComparer.SimplifyForComparison(Expression)"/> call the probe performs must
    /// still establish its own independent, self-contained top-level scope boundary (see
    /// <see cref="ExpressionSimplifier"/>'s "Independent top-level calls" remarks) rather than leaking the
    /// ambient bound-parameter scope into - or losing it from - the cached simplified forms.
    /// </summary>
    [TestMethod]
    public void BoundLambdaEndToEnd_SecondPassFactoring_ProducesSameShapeAsFreeParameters()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");

        Expression body = Expression.Add(
            Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Multiply(Expression.Constant(3.0), x)),
            Expression.Multiply(Expression.Constant(5.0), z));
        Expression<Func<double, double, double>> source = Expression.Lambda<Func<double, double, double>>(body, x, z);

        var result = (Expression<Func<double, double, double>>)simplifier.Simplify(source);

        var multiply = (BinaryExpression)result.Body;
        Assert.AreEqual(ExpressionType.Multiply, multiply.NodeType);
        var sumOfVariables = (BinaryExpression)multiply.Left;
        Assert.AreEqual(ExpressionType.Add, sumOfVariables.NodeType);
        Assert.IsTrue(ReferenceEquals(x, sumOfVariables.Left));
        Assert.IsTrue(ReferenceEquals(z, sumOfVariables.Right));
        var coefficient = (BinaryExpression)multiply.Right;
        Assert.AreEqual(ExpressionType.Add, coefficient.NodeType);

        Func<double, double, double> compiledSource = source.Compile();
        Func<double, double, double> compiledResult = result.Compile();
        foreach ((double xv, double zv) in new[] { (1.0, 2.0), (-3.5, 0.25), (0.0, 10.0) })
        {
            Assert.AreEqual(compiledSource(xv, zv), compiledResult(xv, zv), 1e-9);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Derived ExpressionSimplifier subclass compatibility
    // ------------------------------------------------------------------------------------------

    /// <summary>A minimal <see cref="ExpressionSimplifier"/> subclass with no overrides, used to prove the modified protected rule methods behave identically for a derived type. Neither <c>AdditionOfEqualsElements</c> nor <c>SubstractionOfEqualsElements</c> is declared <see langword="virtual"/>, so a subclass cannot override their behavior; this test exists to pin that the inherited behavior is unchanged rather than to exercise any override.</summary>
    private sealed class MinimalDerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>
    /// The second-pass factoring shape from
    /// <see cref="SecondPassAdd_UnfoldedCoefficient_RecognizesEqualityAfterSimplifying"/> must be identical
    /// when invoked through a derived <see cref="ExpressionSimplifier"/> subclass with no overrides.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifier_SecondPassFactoring_MatchesExactType()
    {
        var simplifier = new MinimalDerivedSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");

        Expression unfoldedCoefficient = Expression.Add(Expression.Constant(2.0), Expression.Constant(3.0));
        Expression left = Expression.Multiply(unfoldedCoefficient, x);
        Expression right = Expression.Multiply(Expression.Constant(5.0), z);

        Expression? result = InvokeAdditionOfEqualsElements(simplifier, left, right);

        Assert.IsNotNull(result);
        var multiply = (BinaryExpression)result!;
        Assert.AreEqual(ExpressionType.Multiply, multiply.NodeType);
        var sumOfVariables = (BinaryExpression)multiply.Left;
        Assert.IsTrue(ReferenceEquals(x, sumOfVariables.Left));
        Assert.IsTrue(ReferenceEquals(z, sumOfVariables.Right));
        Assert.IsTrue(ReferenceEquals(unfoldedCoefficient, multiply.Right));
    }

    // ------------------------------------------------------------------------------------------
    // Review round 6: caching must never reduce how many times a NESTED Simplify() call's own user
    // code runs - not just the final structural comparison's user code (see this class's remarks on
    // HostileConstant_NonSafePolicyInvokesUserEqualsOnceAndPropagatesException, which only exercised the
    // final comparison, not a nested one reached while simplifying a cached candidate).
    // ------------------------------------------------------------------------------------------

    /// <summary>A reference-type constant value whose <see cref="object.Equals(object?)"/> records a call count and returns <see langword="false"/>, without throwing - used to observe an INVOCATION COUNT rather than an exception.</summary>
    private sealed class CountingConstant
    {
        /// <summary>The number of times <see cref="Equals(object?)"/> has been called.</summary>
        public int EqualsCallCount { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            EqualsCallCount++;
            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>
    /// Reproduces review round 6's finding directly: <c>AdditionOfEqualsElements(Add((2*Call(c1) + 3*Call(c2)) * x, 7*y))</c>,
    /// where <c>c1</c>/<c>c2</c> are <see cref="CountingConstant"/> instances embedded as method-call
    /// arguments INSIDE the first candidate's own subtree (<c>leftleft</c>, i.e. the not-yet-simplified inner
    /// <c>Add</c>). <c>leftleft</c> itself participates in two separate comparisons in this shape
    /// (<c>Equals(leftleft, rightleft)</c> then <c>Equals(leftleft, rightright)</c>, since neither of the
    /// earlier branches match). Simplifying <c>leftleft</c> reaches its OWN nested
    /// <c>AdditionOfEqualsElements</c> call, whose own equality probe compares the two method-call arguments
    /// and therefore invokes <c>c1.Equals(c2)</c> - once per INDEPENDENT simplification of <c>leftleft</c>.
    /// Before caching <c>leftleft</c>'s simplified form was made conditional on
    /// <c>FactorEqualityProbe.MightInvokeUserCodeWhenSimplified</c> (this review round), the second
    /// comparison reused the FIRST comparison's cached simplification instead of re-simplifying
    /// <c>leftleft</c>, so <c>c1.Equals(c2)</c> ran only ONCE - one fewer than the uncached, pre-P3 baseline,
    /// confirmed by hand-checking the shipped pre-fix build (reverted before commit, per this file's own
    /// established manual-verification pattern) and the TRUE pre-P3 baseline both invoke it exactly TWICE.
    /// This test pins the count at 2, matching both baselines, not 1.
    /// </summary>
    [TestMethod]
    public void NestedSimplifyReachingUserCode_InvokedOncePerComparison_NotOncePerCandidate()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        MethodInfo identity = typeof(ExpressionSimplifierComparerBatchingTests).GetMethod(
            nameof(IdentityFromObject), BindingFlags.NonPublic | BindingFlags.Static)!;
        var c1 = new CountingConstant();
        var c2 = new CountingConstant();

        // Not-yet-simplified: leftleft below is exactly this raw shape, mirroring how a nested rule's own
        // unfixed-point return value would look (see this class's "second-pass" remarks) - here hand-built
        // directly, since the point under test is what happens when THIS shape is simplified more than once,
        // not how it was produced.
        Expression inner = Expression.Add(
            Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(c1, typeof(object)))),
            Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(c2, typeof(object)))));

        Expression left = Expression.Multiply(inner, x);
        Expression right = Expression.Multiply(Expression.Constant(7.0), y);

        Expression? result = InvokeAdditionOfEqualsElements(simplifier, left, right);

        Assert.IsNull(result, "None of the four candidate comparisons should structurally match for this shape.");
        Assert.AreEqual(2, c1.EqualsCallCount,
            "c1.Equals must run once per comparison leftleft participates in (matching the pre-P3, uncached " +
            "baseline exactly), not once per DISTINCT candidate reference - a cache must never reduce how " +
            "many times a nested Simplify() call's own user code runs.");
        Assert.AreEqual(0, c2.EqualsCallCount, "c2.Equals is never the receiver of the nested comparison (c1.Equals(c2) is called, not c2.Equals(c1)), so it must never be invoked in this shape.");
    }

    // ------------------------------------------------------------------------------------------
    // Review round 7: classifying (not-cacheable-vs-cacheable) must happen for BOTH operands before
    // simplifying EITHER one, so a comparison that ultimately falls back to the public comparer
    // reproduces its exact x-then-y simplification ORDER - not merely its final exception TYPE. Round
    // 6 classified and simplified interleaved (x, then y), so a cacheable y whose OWN simplification
    // throws was simplified - and its exception observed - before a non-cacheable x was ever touched,
    // even though the public contract (and this type's own fallback call) always attempt x first.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Reproduces round 7's finding directly. <c>x</c> is non-cacheable (an <c>Add</c> of two
    /// <c>Multiply</c>/<c>Call</c> terms, each embedding its own <see cref="HostileConstant"/> - the exact
    /// "inner" shape from <see cref="NestedSimplifyReachingUserCode_InvokedOncePerComparison_NotOncePerCandidate"/>,
    /// which simplifies by reaching its own nested <c>AdditionOfEqualsElements</c> call whose structural
    /// argument comparison invokes <c>hostileLeft.Equals(hostileRight)</c> and throws
    /// <see cref="HostileConstantException"/>). <c>y</c> is cacheable (a plain <c>Divide(Constant(1.0),
    /// Constant(0.0))</c> of only native numeric constants) but its OWN simplification unconditionally throws
    /// <see cref="DivideByZeroException"/> (<c>ExpressionSimplifier.DivideWithZeroOrOne</c>) - a DIFFERENT
    /// exception type from <c>x</c>'s, chosen specifically so the propagated exception's TYPE identifies which
    /// operand was actually attempted first.
    /// </summary>
    /// <remarks>
    /// <c>left</c>/<c>right</c> share the exact same <c>Constant(5.0)</c> instance as their <c>Multiply</c>
    /// left factor, so <c>equalityProbe.Equals(leftleft, rightleft)</c> - the first check
    /// <c>AdditionOfEqualsElements</c> performs - is satisfied by <see cref="ExpressionComparer.TryFastPathEquals"/>'s
    /// <see cref="object.ReferenceEquals(object?, object?)"/> shortcut alone, WITHOUT touching the probe's cache,
    /// making <c>equalityProbe.Equals(leftright=x, rightright=y)</c> (inside that same <c>if</c> condition's
    /// short-circuited <c>&amp;&amp;</c> chain) the very first substantive comparison the probe performs - so the
    /// exception this test observes can only have come from that one call.
    /// </remarks>
    [TestMethod]
    public void NonCacheableThenCacheableOperand_FallsBackWithoutSimplifyingEitherFirst_PreservesXBeforeYOrder()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo identity = typeof(ExpressionSimplifierComparerBatchingTests).GetMethod(
            nameof(IdentityFromObject), BindingFlags.NonPublic | BindingFlags.Static)!;
        var hostileLeft = new HostileConstant();
        var hostileRight = new HostileConstant();

        // Non-cacheable: reachable HostileConstant instances make MightInvokeUserCodeWhenSimplified true, and
        // simplifying this shape reaches its own nested AdditionOfEqualsElements call whose structural
        // argument comparison invokes hostileLeft.Equals(hostileRight) - see this class's remarks on
        // NestedSimplifyReachingUserCode_InvokedOncePerComparison_NotOncePerCandidate for why.
        Expression x = Expression.Add(
            Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(hostileLeft, typeof(object)))),
            Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(hostileRight, typeof(object)))));

        // Cacheable (only native numeric constants reachable) but its OWN simplification unconditionally
        // throws DivideByZeroException - a different exception type from x's, so the type that propagates
        // proves which operand was attempted first.
        Expression y = Expression.Divide(Expression.Constant(1.0), Expression.Constant(0.0));

        Expression sharedFactor = Expression.Constant(5.0);
        Expression left = Expression.Multiply(sharedFactor, x);
        Expression right = Expression.Multiply(sharedFactor, y);

        TargetInvocationException thrown = Assert.ThrowsExactly<TargetInvocationException>(
            () => InvokeAdditionOfEqualsElements(simplifier, left, right));

        Exception innermost = thrown.InnerException!;
        while (innermost is TargetInvocationException { InnerException: { } nestedInner })
        {
            innermost = nestedInner;
        }

        Assert.IsInstanceOfType<HostileConstantException>(
            innermost,
            "The propagated exception must be x's HostileConstantException, matching the true pre-P3 baseline " +
            "(and ExpressionComparer.Equals's own Simplify(x)-then-Simplify(y) order) where non-cacheable x is " +
            "always attempted before cacheable y. Observing y's DivideByZeroException instead would mean y was " +
            "simplified - a side-effecting operation - during classification, before the fallback decision was " +
            "even reached, which is exactly round 6's ordering bug.");
    }
}
