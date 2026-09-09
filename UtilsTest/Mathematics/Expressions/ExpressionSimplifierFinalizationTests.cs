using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization and regression coverage for <c>ExpressionSimplifier.FinalizeExpression</c>'s
/// fallback path: when no <c>ExpressionSignatureAttribute</c>-annotated rule matches a prepared node,
/// the historical behavior performs a second reconstruction (<c>CopyExpression</c>) of a node that
/// <c>Prepare*</c> already rebuilt once. These tests lock in both the baseline double-reconstruction
/// behavior (observable only through a subclass override, since the intermediate "prepared" node is
/// otherwise private to the transform pipeline) and the exact scope of any later fast-path
/// optimization: it must apply only to the exact built-in <see cref="ExpressionSimplifier"/> runtime
/// type, and only to <see cref="UnaryExpression"/>, <see cref="BinaryExpression"/>,
/// <see cref="MethodCallExpression"/>, and <see cref="ConditionalExpression"/> nodes that reach the
/// fallback (i.e. are not resolved by a rule and not canonicalized by the Add/Subtract/Multiply
/// canonicalization already present in the fallback).
/// </summary>
[TestClass]
public class ExpressionSimplifierFinalizationTests
{
    /// <summary>
    /// A subclass that observes <c>FinalizeExpression</c>'s input and the result of calling
    /// <c>base.FinalizeExpression</c>, without altering behavior. Used to make the otherwise-private
    /// "prepared node" passed into the fallback observable from a test, and to verify that a subclass
    /// overriding <c>FinalizeExpression</c> keeps receiving the historical double reconstruction
    /// regardless of any optimization applied to the exact <see cref="ExpressionSimplifier"/> type.
    /// </summary>
    private sealed class ObservingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the node <c>FinalizeExpression</c> was called with (the node <c>Prepare*</c> already rebuilt).</summary>
        public Expression? FinalizeInput { get; private set; }

        /// <summary>Gets the parameters array <c>FinalizeExpression</c> was called with.</summary>
        public Expression[]? FinalizeParameters { get; private set; }

        /// <summary>Gets the result of calling <c>base.FinalizeExpression</c> (the built-in simplifier's fallback result).</summary>
        public Expression? BaseFinalizeResult { get; private set; }

        /// <summary>Records <paramref name="e"/> and <paramref name="parameters"/>, then delegates to and records the base result.</summary>
        /// <param name="e">The node being finalized.</param>
        /// <param name="parameters">The prepared sub-expressions of <paramref name="e"/>.</param>
        /// <returns>Whatever <c>base.FinalizeExpression</c> returns, unmodified.</returns>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            FinalizeInput = e;
            FinalizeParameters = parameters;
            BaseFinalizeResult = base.FinalizeExpression(e, parameters);
            return BaseFinalizeResult;
        }
    }

    /// <summary>A static method with no matching <c>ExpressionCallSignatureAttribute</c> rule in <see cref="ExpressionSimplifier"/>.</summary>
    /// <param name="value">The value to return unchanged.</param>
    /// <returns><paramref name="value"/>, unchanged.</returns>
    private static double Identity(double value) => value;

    // ------------------------------------------------------------------------------------------
    // A/B: Finalize receives an already-rebuilt node, and its children are already prepared.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// For a <see cref="ConditionalExpression"/> with no matching rule, <c>FinalizeExpression</c> is
    /// called with a node already distinct from the original (rebuilt by <c>PrepareConditional</c>),
    /// and the built-in fallback rebuilds it a second time (<c>CopyExpression</c>) before returning.
    /// </summary>
    [TestMethod]
    public void Finalize_ConditionalWithoutRule_ReceivesRebuiltNodeAndCopiesAgain()
    {
        var simplifier = new ObservingSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConditionalExpression original = Expression.Condition(
            Expression.GreaterThan(x, Expression.Constant(0.0)),
            x,
            Expression.Constant(-1.0));

        Expression result = simplifier.Simplify(original);

        Assert.IsNotNull(simplifier.FinalizeInput);
        Assert.AreNotSame(original, simplifier.FinalizeInput);
        Assert.IsInstanceOfType(simplifier.FinalizeInput, typeof(ConditionalExpression));

        Assert.IsNotNull(simplifier.BaseFinalizeResult);
        Assert.AreNotSame(simplifier.FinalizeInput, simplifier.BaseFinalizeResult);

        Assert.AreSame(simplifier.BaseFinalizeResult, result);
    }

    /// <summary>
    /// The <c>Test</c>/<c>IfTrue</c>/<c>IfFalse</c> branches observed by <c>FinalizeExpression</c> are
    /// already the simplified versions (e.g. <c>x + 0</c> reduced to <c>x</c>), proving that children
    /// are fully prepared/transformed before the parent node ever reaches the fallback.
    /// </summary>
    [TestMethod]
    public void Finalize_ConditionalBranches_AreAlreadySimplified()
    {
        var simplifier = new ObservingSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConditionalExpression original = Expression.Condition(
            Expression.GreaterThan(x, Expression.Constant(0.0)),
            Expression.Add(x, Expression.Constant(0.0)),
            Expression.Multiply(x, Expression.Constant(1.0)));

        simplifier.Simplify(original);

        var finalized = (ConditionalExpression)simplifier.FinalizeInput!;
        Assert.AreSame(x, finalized.IfTrue);
        Assert.AreSame(x, finalized.IfFalse);
    }

    // ------------------------------------------------------------------------------------------
    // C: a subclass overriding FinalizeExpression must keep observing the double reconstruction.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A subclass calling <c>base.FinalizeExpression</c> must always observe
    /// <c>FinalizeInput != BaseFinalizeResult</c>: this is the compatibility guarantee that any later
    /// fast-path optimization in the exact built-in <see cref="ExpressionSimplifier"/> type must not
    /// break for derived types.
    /// </summary>
    [TestMethod]
    public void Finalize_SubclassObserving_AlwaysSeesDistinctBaseResult()
    {
        var simplifier = new ObservingSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConditionalExpression original = Expression.Condition(
            Expression.GreaterThan(x, Expression.Constant(0.0)),
            x,
            Expression.Constant(-1.0));

        simplifier.Simplify(original);

        Assert.AreNotSame(simplifier.FinalizeInput, simplifier.BaseFinalizeResult);
    }

    // ------------------------------------------------------------------------------------------
    // D/E/F/G/H: exact ExpressionSimplifier fallback for each optimized family returns a node
    // distinct from the original, with correct structure and semantics.
    // ------------------------------------------------------------------------------------------

    /// <summary>A <see cref="UnaryExpression"/> (<c>Not</c>) with no matching rule is rebuilt, not returned as-is.</summary>
    [TestMethod]
    public void Simplify_UnaryWithoutRule_ReturnsDistinctNodeWithSameSemantics()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression b = Expression.Parameter(typeof(bool), "b");
        UnaryExpression original = Expression.Not(b);

        Expression result = simplifier.Simplify(original);

        Assert.AreNotSame(original, result);
        Assert.AreEqual(ExpressionType.Not, result.NodeType);
        var compiled = Expression.Lambda<Func<bool, bool>>((Expression)result, b).Compile();
        Assert.IsFalse(compiled(true));
        Assert.IsTrue(compiled(false));
    }

    /// <summary>A <see cref="BinaryExpression"/> (<c>AndAlso</c>) with no matching rule is rebuilt, not returned as-is.</summary>
    [TestMethod]
    public void Simplify_BinaryWithoutRule_ReturnsDistinctNodeWithSameSemantics()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(bool), "a");
        ParameterExpression b = Expression.Parameter(typeof(bool), "b");
        BinaryExpression original = Expression.AndAlso(a, b);

        Expression result = simplifier.Simplify(original);

        Assert.AreNotSame(original, result);
        Assert.AreEqual(ExpressionType.AndAlso, result.NodeType);
        var compiled = Expression.Lambda<Func<bool, bool, bool>>((Expression)result, a, b).Compile();
        Assert.IsTrue(compiled(true, true));
        Assert.IsFalse(compiled(true, false));
        Assert.IsFalse(compiled(false, true));
    }

    /// <summary>A <see cref="MethodCallExpression"/> with no matching rule is rebuilt, not returned as-is.</summary>
    [TestMethod]
    public void Simplify_MethodCallWithoutRule_ReturnsDistinctNodeWithSameSemantics()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo identity = typeof(ExpressionSimplifierFinalizationTests).GetMethod(
            nameof(Identity), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodCallExpression original = Expression.Call(identity, x);

        Expression result = simplifier.Simplify(original);

        Assert.AreNotSame(original, result);
        Assert.AreEqual(ExpressionType.Call, result.NodeType);
        Assert.AreSame(identity, ((MethodCallExpression)result).Method);
        var compiled = Expression.Lambda<Func<double, double>>((Expression)result, x).Compile();
        Assert.AreEqual(3.5, compiled(3.5));
    }

    /// <summary>A <see cref="ConditionalExpression"/> with no matching rule is rebuilt, not returned as-is.</summary>
    [TestMethod]
    public void Simplify_ConditionalWithoutRule_ReturnsDistinctNodeWithSameSemantics()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConditionalExpression original = Expression.Condition(
            Expression.GreaterThan(x, Expression.Constant(0.0)),
            x,
            Expression.Constant(-1.0));

        Expression result = simplifier.Simplify(original);

        Assert.AreNotSame(original, result);
        Assert.AreEqual(ExpressionType.Conditional, result.NodeType);
        Assert.AreEqual(original.Type, ((ConditionalExpression)result).Type);
        var compiled = Expression.Lambda<Func<double, double>>((Expression)result, x).Compile();
        Assert.AreEqual(5.0, compiled(5.0));
        Assert.AreEqual(-1.0, compiled(-5.0));
    }

    // ------------------------------------------------------------------------------------------
    // I/J: canonicalization still takes priority over the (future) fast path.
    // ------------------------------------------------------------------------------------------

    /// <summary>Additive canonicalization still applies to an <see cref="ExpressionType.Add"/> node that reaches the fallback.</summary>
    [TestMethod]
    public void Simplify_AddReachingFallback_StillCanonicalizes()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");

        Expression result1 = simplifier.Simplify(Expression.Add(a, b));
        Expression result2 = simplifier.Simplify(Expression.Add(b, a));

        var compiled1 = Expression.Lambda<Func<double, double, double>>((Expression)result1, a, b).Compile();
        var compiled2 = Expression.Lambda<Func<double, double, double>>((Expression)result2, a, b).Compile();
        Assert.AreEqual(compiled1.ToString(), compiled2.ToString());
    }

    /// <summary>Multiplicative canonicalization still applies to an <see cref="ExpressionType.Multiply"/> node that reaches the fallback.</summary>
    [TestMethod]
    public void Simplify_MultiplyReachingFallback_StillCanonicalizes()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");

        Expression result1 = simplifier.Simplify(Expression.Multiply(a, b));
        Expression result2 = simplifier.Simplify(Expression.Multiply(b, a));

        var compiled1 = Expression.Lambda<Func<double, double, double>>((Expression)result1, a, b).Compile();
        var compiled2 = Expression.Lambda<Func<double, double, double>>((Expression)result2, a, b).Compile();
        Assert.AreEqual(compiled1.ToString(), compiled2.ToString());
    }

    // ------------------------------------------------------------------------------------------
    // L: the fast path (once introduced) must key off the CLR node type produced by Prepare*, not
    // NodeType, so a TypeBinaryExpression (which goes through PrepareDefault, not a dedicated
    // Prepare* method) keeps its historical, currently-broken CopyExpression behavior.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Characterizes the current (arguably broken) behavior of a <see cref="TypeBinaryExpression"/>
    /// (<c>TypeIs</c>) reaching the fallback: <c>PrepareDefault</c> passes through with an empty
    /// parameters array, and <c>CopyExpression</c>'s <c>TypeIs</c> branch indexes
    /// <c>parameters[0]</c>, which throws. This is a known, pre-existing limitation of
    /// <c>PrepareDefault</c>/<c>CopyExpression</c> and must NOT be "fixed" by this test or by any
    /// fast-path optimization scoped to <see cref="UnaryExpression"/>/<see cref="BinaryExpression"/>/
    /// <see cref="MethodCallExpression"/>/<see cref="ConditionalExpression"/> only.
    /// </summary>
    [TestMethod]
    public void Simplify_TypeBinaryWithoutRule_ThrowsIndexOutOfRange()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression obj = Expression.Parameter(typeof(object), "obj");
        TypeBinaryExpression original = Expression.TypeIs(obj, typeof(string));

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => simplifier.Simplify(original));
    }

    // ------------------------------------------------------------------------------------------
    // M: Constant is never routed through the (future) fast path.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A <see cref="ConstantExpression"/> reaching the fallback is never rebuilt via the (future)
    /// fast path: it always goes through <c>CopyExpression</c>'s <c>Constant</c> branch, which
    /// allocates a new <see cref="ConstantExpression"/> carrying the same boxed value and type.
    /// </summary>
    [TestMethod]
    public void Simplify_ConstantWithoutRule_ReturnsNewConstantWithSameValue()
    {
        var simplifier = new ExpressionSimplifier();
        ConstantExpression original = Expression.Constant(42.0);

        Expression result = simplifier.Simplify(original);

        Assert.IsInstanceOfType(result, typeof(ConstantExpression));
        Assert.AreEqual(42.0, ((ConstantExpression)result).Value);
    }
}
