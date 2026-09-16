using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization and regression coverage for stage S1's reconstruction-fidelity hardening: the exact
/// built-in <see cref="ExpressionSimplifier"/> runtime type must reconstruct <see cref="UnaryExpression"/>
/// and <see cref="LambdaExpression"/> nodes without silently dropping metadata (a custom
/// <see cref="UnaryExpression.Method"/>, a typed <c>Throw</c>'s declared <see cref="Expression.Type"/>, a
/// custom <see cref="LambdaExpression.Type"/>/<see cref="LambdaExpression.TailCall"/>/
/// <see cref="LambdaExpression.Name"/>), while the GENERIC <see cref="ExpressionTransformer"/> and any
/// DERIVED <see cref="ExpressionSimplifier"/> subclass keep the historical, metadata-dropping
/// reconstruction unchanged. Several tests here are documented as "regression": they fail on the pre-fix
/// baseline (either by throwing or by preserving/asserting the wrong value) and only pass once the new
/// internal reconstruction hooks land. Every test builds its own expressions deterministically.
/// </summary>
[TestClass]
public class ExpressionSimplifierReconstructionFidelityTests
{
    /// <summary>A custom unary operator with an observably different implementation from <see cref="CustomNegateB"/>, used to prove <see cref="UnaryExpression.Method"/> survives simplification.</summary>
    /// <param name="value">The operand.</param>
    /// <returns>The negation of <paramref name="value"/>.</returns>
    private static double CustomNegateA(double value) => -value;

    /// <summary>A custom unary operator with an observably different implementation from <see cref="CustomNegateA"/>, used to prove <see cref="UnaryExpression.Method"/> survives simplification.</summary>
    /// <param name="value">The operand.</param>
    /// <returns>The negation of <paramref name="value"/>, doubled, so the two methods are observably different.</returns>
    private static double CustomNegateB(double value) => -(value * 2.0);

    private static readonly MethodInfo CustomNegateAMethod =
        typeof(ExpressionSimplifierReconstructionFidelityTests).GetMethod(nameof(CustomNegateA), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CustomNegateBMethod =
        typeof(ExpressionSimplifierReconstructionFidelityTests).GetMethod(nameof(CustomNegateB), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>A delegate type distinct from <see cref="DelegateB"/> despite an identical <c>double -&gt; double</c> signature, used for root/nested lambda-type fidelity tests.</summary>
    /// <param name="x">The operand.</param>
    /// <returns>A <see cref="double"/> result.</returns>
    private delegate double DelegateA(double x);

    /// <summary>A delegate type distinct from <see cref="DelegateA"/> despite an identical <c>double -&gt; double</c> signature, used for root/nested lambda-type fidelity tests.</summary>
    /// <param name="x">The operand.</param>
    /// <returns>A <see cref="double"/> result.</returns>
    private delegate double DelegateB(double x);

    /// <summary>A minimal <see cref="ExpressionSimplifier"/> subclass with no overrides, used to pin that only the EXACT built-in runtime type gets metadata-faithful reconstruction.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Creates a fresh <see cref="double"/> parameter named <c>x</c>.</summary>
    /// <returns>A new <see cref="double"/> parameter expression.</returns>
    private static ParameterExpression X() => Expression.Parameter(typeof(double), "x");

    // ------------------------------------------------------------------------------------------
    // Finding 1 - custom unary Method
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Regression: the historical reconstruction rebuilds <see cref="ExpressionType.Negate"/> via
    /// <see cref="Expression.Negate(Expression)"/>, which drops an explicit custom
    /// <see cref="UnaryExpression.Method"/>. The exact <see cref="ExpressionSimplifier"/> must preserve it.
    /// </summary>
    [TestMethod]
    public void ExactSimplifier_CustomUnaryMethod_IsPreserved()
    {
        UnaryExpression original = Expression.Negate(X(), CustomNegateAMethod);

        var simplified = (UnaryExpression)new ExpressionSimplifier().Simplify(original);

        Assert.AreEqual(CustomNegateAMethod, simplified.Method);
        Assert.AreEqual(original.Type, simplified.Type);
    }

    /// <summary>
    /// Direct simplifier semantic regression (not merely a comparer concern): a lambda whose body is a
    /// custom-method <see cref="ExpressionType.Negate"/> must still compute <see cref="CustomNegateB"/>'s
    /// actual behavior after simplification, not ordinary arithmetic negation.
    /// </summary>
    [TestMethod]
    public void ExactSimplifier_CustomUnaryMethod_SimplifiedDelegateMatchesSourceSemantics()
    {
        ParameterExpression x = X();
        UnaryExpression body = Expression.Negate(x, CustomNegateBMethod);
        var source = Expression.Lambda<Func<double, double>>(body, x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);
        Func<double, double> compiledSource = source.Compile();
        Func<double, double> compiledSimplified = simplified.Compile();

        foreach (double v in new[] { 0.0, 1.5, -3.25, 42.0 })
        {
            Assert.AreEqual(compiledSource(v), compiledSimplified(v), 1e-9);
            Assert.AreEqual(CustomNegateB(v), compiledSimplified(v), 1e-9);
        }
    }

    /// <summary>
    /// Regression: PR #590's <c>ExpressionComparer</c> correctly compares <see cref="UnaryExpression.Method"/>,
    /// but simplification erased the method before the comparer ever saw it, so two lambdas built from
    /// different custom negate methods over the same operand still compared equal. Fixing reconstruction
    /// fidelity closes this remaining false positive.
    /// </summary>
    [TestMethod]
    public void Comparer_DistinguishesTwoDifferentCustomUnaryMethods_AfterReconstructionFix()
    {
        ParameterExpression x = X();
        ParameterExpression y = X();
        var left = Expression.Lambda<Func<double, double>>(Expression.Negate(x, CustomNegateAMethod), x);
        var right = Expression.Lambda<Func<double, double>>(Expression.Negate(y, CustomNegateBMethod), y);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 2 - typed Throw
    // ------------------------------------------------------------------------------------------

    /// <summary>Regression: the historical reconstruction rebuilds <see cref="ExpressionType.Throw"/> via the untyped <see cref="Expression.Throw(Expression)"/>, always producing a <see cref="void"/> result type.</summary>
    [TestMethod]
    public void ExactSimplifier_TypedThrow_TypeIsPreserved()
    {
        UnaryExpression typedThrow = Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(int));

        var simplified = (UnaryExpression)new ExpressionSimplifier().Simplify(typedThrow);

        Assert.AreEqual(ExpressionType.Throw, simplified.NodeType);
        Assert.AreEqual(typeof(int), simplified.Type);
    }

    /// <summary>
    /// Strong discriminator: a typed throw used as one branch of a <see cref="ConditionalExpression"/> is
    /// only valid because its declared result type matches the other branch's type. If simplification had
    /// turned it back into <see langword="void"/>, this conditional could not be reconstructed/compiled as
    /// an <c>int</c>-returning expression.
    /// </summary>
    [TestMethod]
    public void ExactSimplifier_TypedThrowInsideConditional_RemainsValidAndExecutable()
    {
        ParameterExpression test = Expression.Parameter(typeof(bool), "test");
        UnaryExpression typedThrow = Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(int));
        ConditionalExpression conditional = Expression.Condition(test, typedThrow, Expression.Constant(42));
        var source = Expression.Lambda<Func<bool, int>>(conditional, test);

        var simplified = (Expression<Func<bool, int>>)new ExpressionSimplifier().Simplify(source);
        Func<bool, int> compiled = simplified.Compile();

        Assert.AreEqual(42, compiled(false));
        Assert.ThrowsExactly<InvalidOperationException>(() => compiled(true));
    }

    /// <summary>Regression: <c>ExpressionSimplifier.PrepareExpression</c> forwarded a <see langword="null"/> <see cref="Expression.Rethrow()"/> operand straight into <c>Transform</c>, throwing <see cref="NullReferenceException"/> before reconstruction ever ran.</summary>
    [TestMethod]
    public void ExactSimplifier_Rethrow_NullOperand_RemainsValid()
    {
        UnaryExpression rethrow = Expression.Rethrow();

        var simplified = (UnaryExpression)new ExpressionSimplifier().Simplify(rethrow);

        Assert.AreEqual(ExpressionType.Throw, simplified.NodeType);
        Assert.AreEqual(typeof(void), simplified.Type);
        Assert.IsNull(simplified.Operand);
    }

    // ------------------------------------------------------------------------------------------
    // Finding 3 - lambda metadata (root)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Regression, combining three metadata bits with a genuine body simplification in one lambda (so the
    /// fix cannot be satisfied by merely skipping reconstruction): a root lambda with a custom delegate
    /// <see cref="LambdaExpression.Type"/>, a non-default <see cref="LambdaExpression.Name"/>, and
    /// <see cref="LambdaExpression.TailCall"/> set must retain all three after <c>x + 0</c> is simplified
    /// down to <c>x</c>.
    /// </summary>
    [TestMethod]
    public void ExactSimplifier_RootLambdaMetadata_PreservedAlongsideRealBodySimplification()
    {
        ParameterExpression x = X();
        Expression body = Expression.Add(x, Expression.Constant(0.0));
        LambdaExpression original = Expression.Lambda(typeof(DelegateA), body, "SentinelName", tailCall: true, [x]);

        var simplified = (LambdaExpression)new ExpressionSimplifier().Simplify(original);

        Assert.AreEqual(typeof(DelegateA), simplified.Type);
        Assert.AreEqual("SentinelName", simplified.Name);
        Assert.IsTrue(simplified.TailCall);
        Assert.AreEqual(ExpressionType.Parameter, simplified.Body.NodeType, "The body must actually be simplified from 'x + 0' down to 'x'.");

        var compiled = (DelegateA)((LambdaExpression)simplified).Compile();
        Assert.AreEqual(3.5, compiled(3.5), 1e-9);
    }

    /// <summary>Control: an ordinary <see cref="Expression{Func}"/> lambda (the overwhelmingly common shape) continues to simplify and compile normally.</summary>
    [TestMethod]
    public void ExactSimplifier_OrdinaryFuncLambda_NonRegression()
    {
        ParameterExpression x = X();
        Expression<Func<double, double>> source = x2 => x2 + 0;

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual(typeof(Func<double, double>), simplified.Type);
        Assert.AreEqual(ExpressionType.Parameter, simplified.Body.NodeType);
        Assert.AreEqual(5.0, simplified.Compile()(5.0));
    }

    /// <summary>Required: <see cref="ExpressionComparer"/> deliberately ignores <see cref="LambdaExpression.Name"/> - preserving it as reconstruction metadata must not couple it into the equality contract.</summary>
    [TestMethod]
    public void ExactSimplifier_LambdaName_PreservedBySimplify_ButStillIgnoredByComparer()
    {
        ParameterExpression x = X();
        ParameterExpression y = X();
        LambdaExpression left = Expression.Lambda(typeof(Func<double, double>), Expression.Add(x, Expression.Constant(1.0)), "Foo", false, [x]);
        LambdaExpression right = Expression.Lambda(typeof(Func<double, double>), Expression.Add(y, Expression.Constant(1.0)), "Bar", false, [y]);

        var simplifiedLeft = (LambdaExpression)new ExpressionSimplifier().Simplify(left);
        var simplifiedRight = (LambdaExpression)new ExpressionSimplifier().Simplify(right);

        Assert.AreEqual("Foo", simplifiedLeft.Name);
        Assert.AreEqual("Bar", simplifiedRight.Name);
        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right), "Equals already re-simplifies internally; Name must still be ignored.");
    }

    // ------------------------------------------------------------------------------------------
    // Finding 3 - lambda metadata (nested)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Regression: PR #590 could only protect the ROOT lambda's <see cref="LambdaExpression.Type"/> (by
    /// comparing it before simplification, in the public <c>ExpressionComparer.Equals</c>). A NESTED
    /// lambda's delegate type was still erased by <c>PrepareLambda</c>'s recursive rebuild, so two outer
    /// expressions differing only in a nested lambda's delegate type compared equal. Fixing
    /// <c>ExpressionSimplifier.Simplify</c> itself (not just the comparer) closes this at every nesting
    /// depth, since the same reconstruction hook runs recursively.
    /// </summary>
    [TestMethod]
    public void Comparer_DistinguishesNestedCustomDelegateType_AfterReconstructionFix()
    {
        ParameterExpression xa = X();
        ParameterExpression xb = X();
        LambdaExpression innerA = Expression.Lambda(typeof(DelegateA), Expression.Add(xa, Expression.Constant(0.0)), [xa]);
        LambdaExpression innerB = Expression.Lambda(typeof(DelegateB), Expression.Add(xb, Expression.Constant(0.0)), [xb]);

        var outerA = Expression.Lambda<Func<Delegate>>(Expression.Convert(innerA, typeof(Delegate)));
        var outerB = Expression.Lambda<Func<Delegate>>(Expression.Convert(innerB, typeof(Delegate)));

        Assert.IsFalse(ExpressionComparer.Default.Equals(outerA, outerB));
    }

    /// <summary>Regression: a nested lambda's <see cref="LambdaExpression.TailCall"/> difference was likewise erased before this fix, at any nesting depth beyond the root.</summary>
    [TestMethod]
    public void Comparer_DistinguishesNestedTailCall_AfterReconstructionFix()
    {
        ParameterExpression xa = X();
        ParameterExpression xb = X();
        LambdaExpression innerA = Expression.Lambda(typeof(DelegateA), Expression.Add(xa, Expression.Constant(0.0)), tailCall: false, [xa]);
        LambdaExpression innerB = Expression.Lambda(typeof(DelegateA), Expression.Add(xb, Expression.Constant(0.0)), tailCall: true, [xb]);

        var outerA = Expression.Lambda<Func<Delegate>>(Expression.Convert(innerA, typeof(Delegate)));
        var outerB = Expression.Lambda<Func<Delegate>>(Expression.Convert(innerB, typeof(Delegate)));

        Assert.IsFalse(ExpressionComparer.Default.Equals(outerA, outerB));
    }

    // ------------------------------------------------------------------------------------------
    // Compatibility boundary - generic ExpressionTransformer and derived ExpressionSimplifier
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Subclass compatibility pin: a derived <see cref="ExpressionSimplifier"/> with no overrides must keep
    /// dropping a custom unary <see cref="UnaryExpression.Method"/>, exactly like the historical baseline,
    /// because only the EXACT built-in runtime type opts into metadata-faithful reconstruction.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifier_CustomUnaryMethod_StillDroppedLikeHistoricalBaseline()
    {
        UnaryExpression original = Expression.Negate(X(), CustomNegateAMethod);

        var simplified = (UnaryExpression)new DerivedSimplifier().Simplify(original);

        Assert.IsNull(simplified.Method);
    }

    /// <summary>
    /// Subclass compatibility pin: a derived <see cref="ExpressionSimplifier"/> with no overrides must keep
    /// erasing a custom root lambda delegate <see cref="LambdaExpression.Type"/> and
    /// <see cref="LambdaExpression.TailCall"/> (both collapse to the type-inferred default), exactly like
    /// the historical baseline.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifier_LambdaTypeAndTailCall_StillErasedLikeHistoricalBaseline()
    {
        ParameterExpression x = X();
        LambdaExpression original = Expression.Lambda(typeof(DelegateA), Expression.Add(x, Expression.Constant(0.0)), tailCall: true, [x]);

        var simplified = (LambdaExpression)new DerivedSimplifier().Simplify(original);

        Assert.AreNotEqual(typeof(DelegateA), simplified.Type);
        Assert.AreEqual(typeof(Func<double, double>), simplified.Type);
        Assert.IsFalse(simplified.TailCall);
    }
}
