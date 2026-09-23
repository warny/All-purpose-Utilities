using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S4 ("Replace textual canonical identity with structural
/// canonical keys" — see <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>): canonical
/// additive/multiplicative ordering and grouping must be driven by expression structure (node metadata,
/// exact reflection identity, lexical parameter binding position) rather than <see cref="Expression.ToString()"/>
/// or <see cref="ParameterExpression.Name"/>.
/// </summary>
/// <remarks>
/// Every test exercises the PUBLIC <c>new ExpressionSimplifier().Simplify(...)</c> path. Where a test needs
/// to inspect canonical order/binding position directly (rather than only via <see cref="ExpressionComparer"/>,
/// which itself re-simplifies its operands and could mask a first-pass canonicalization defect), it walks
/// the raw resulting tree with <see cref="ReferenceEquals(object?, object?)"/>-based position checks instead.
/// </remarks>
[TestClass]
public class ExpressionSimplifierStructuralCanonicalizationTests
{
    // ------------------------------------------------------------------------------------------
    // Shared test helpers
    // ------------------------------------------------------------------------------------------

    /// <summary>For a two-parameter lambda whose body is a single <see cref="BinaryExpression"/>, returns which declared parameter position ended up on the left and right.</summary>
    private static (int LeftPosition, int RightPosition) GetOperandPositions(LambdaExpression lambda)
    {
        var body = (BinaryExpression)lambda.Body;
        int leftPosition = -1, rightPosition = -1;
        for (int i = 0; i < lambda.Parameters.Count; i++)
        {
            if (ReferenceEquals(lambda.Parameters[i], body.Left)) leftPosition = i;
            if (ReferenceEquals(lambda.Parameters[i], body.Right)) rightPosition = i;
        }
        return (leftPosition, rightPosition);
    }

    /// <summary>Depth-first enumeration of every sub-expression reachable from <paramref name="root"/>, for the node kinds these tests build.</summary>
    private static IEnumerable<Expression> EnumerateNodes(Expression? root)
    {
        if (root is null) yield break;
        yield return root;

        switch (root)
        {
            case LambdaExpression le:
                foreach (ParameterExpression p in le.Parameters)
                {
                    foreach (Expression n in EnumerateNodes(p)) yield return n;
                }
                foreach (Expression n in EnumerateNodes(le.Body)) yield return n;
                break;
            case BinaryExpression be:
                foreach (Expression n in EnumerateNodes(be.Left)) yield return n;
                foreach (Expression n in EnumerateNodes(be.Right)) yield return n;
                foreach (Expression n in EnumerateNodes(be.Conversion)) yield return n;
                break;
            case UnaryExpression ue:
                foreach (Expression n in EnumerateNodes(ue.Operand)) yield return n;
                break;
            case MethodCallExpression mce:
                foreach (Expression n in EnumerateNodes(mce.Object)) yield return n;
                foreach (Expression argument in mce.Arguments)
                {
                    foreach (Expression n in EnumerateNodes(argument)) yield return n;
                }
                break;
            case ConditionalExpression ce:
                foreach (Expression n in EnumerateNodes(ce.Test)) yield return n;
                foreach (Expression n in EnumerateNodes(ce.IfTrue)) yield return n;
                foreach (Expression n in EnumerateNodes(ce.IfFalse)) yield return n;
                break;
            case MemberExpression me:
                foreach (Expression n in EnumerateNodes(me.Expression)) yield return n;
                break;
        }
    }

    /// <summary>Whether <paramref name="target"/> is reachable from <paramref name="root"/> by reference identity (not structural equality).</summary>
    /// <param name="root">The tree to search.</param>
    /// <param name="target">The specific node instance to look for.</param>
    /// <returns><see langword="true"/> if <paramref name="target"/> is one of <paramref name="root"/>'s sub-expressions, by reference.</returns>
    private static bool Contains(Expression root, Expression target) => EnumerateNodes(root).Any(n => ReferenceEquals(n, target));

    // ------------------------------------------------------------------------------------------
    // 1. Bound parameter names do not affect canonical order
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Two alpha-equivalent two-parameter lambdas with the same declaration-position semantics but
    /// deliberately reversed lexical names (position 0 is "z" in one, "a" in the other) must choose the
    /// same ordering by BINDING POSITION, not by <see cref="ParameterExpression.Name"/>. Fails on the
    /// pre-S4 baseline: the old textual key sorted by parameter name ("a" &lt; "z"), which puts position-0
    /// first in one lambda and position-1 first in the other.
    /// </summary>
    [TestMethod]
    public void BoundParameterNames_DoNotAffectCanonicalOrder()
    {
        var simplifier = new ExpressionSimplifier();

        ParameterExpression p0z = Expression.Parameter(typeof(double), "z");
        ParameterExpression p1a = Expression.Parameter(typeof(double), "a");
        var lambda1 = Expression.Lambda<Func<double, double, double>>(Expression.Add(p1a, p0z), p0z, p1a);

        ParameterExpression p0a = Expression.Parameter(typeof(double), "a");
        ParameterExpression p1z = Expression.Parameter(typeof(double), "z");
        var lambda2 = Expression.Lambda<Func<double, double, double>>(Expression.Add(p1z, p0a), p0a, p1z);

        var result1 = (LambdaExpression)simplifier.Simplify(lambda1);
        var result2 = (LambdaExpression)simplifier.Simplify(lambda2);

        var positions1 = GetOperandPositions(result1);
        var positions2 = GetOperandPositions(result2);

        Assert.AreEqual(positions1, positions2, "Canonical operand order must depend only on declaration position, not on parameter names.");
    }

    // ------------------------------------------------------------------------------------------
    // 2. Duplicate bound parameter names
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Two different parameters in the same lambda both named "v": simplifying <c>p1 + p0</c> must
    /// converge to the same structural canonical result as <c>p0 + p1</c>, based on declaration position
    /// rather than the identical text "v". Fails on the pre-S4 baseline (identical ToString text makes the
    /// old key's ordering undefined/unstable between the two operands).
    /// </summary>
    [TestMethod]
    public void DuplicateBoundParameterNames_Addition_ConvergesByDeclarationPosition()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression p0 = Expression.Parameter(typeof(double), "v");
        ParameterExpression p1 = Expression.Parameter(typeof(double), "v");

        var lambdaP1PlusP0 = Expression.Lambda<Func<double, double, double>>(Expression.Add(p1, p0), p0, p1);
        var lambdaP0PlusP1 = Expression.Lambda<Func<double, double, double>>(Expression.Add(p0, p1), p0, p1);

        var result1 = (LambdaExpression)simplifier.Simplify(lambdaP1PlusP0);
        var result2 = (LambdaExpression)simplifier.Simplify(lambdaP0PlusP1);

        Assert.AreEqual(GetOperandPositions(result1), GetOperandPositions(result2));
        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Multiplicative equivalent of <see cref="DuplicateBoundParameterNames_Addition_ConvergesByDeclarationPosition"/>.</summary>
    [TestMethod]
    public void DuplicateBoundParameterNames_Multiplication_ConvergesByDeclarationPosition()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression p0 = Expression.Parameter(typeof(double), "v");
        ParameterExpression p1 = Expression.Parameter(typeof(double), "v");

        var lambdaP1TimesP0 = Expression.Lambda<Func<double, double, double>>(Expression.Multiply(p1, p0), p0, p1);
        var lambdaP0TimesP1 = Expression.Lambda<Func<double, double, double>>(Expression.Multiply(p0, p1), p0, p1);

        var result1 = (LambdaExpression)simplifier.Simplify(lambdaP1TimesP0);
        var result2 = (LambdaExpression)simplifier.Simplify(lambdaP0TimesP1);

        Assert.AreEqual(GetOperandPositions(result1), GetOperandPositions(result2));
        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    // ------------------------------------------------------------------------------------------
    // 3. Nested lambda scopes / captures
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A nested lambda whose body captures the outer parameter and uses the inner parameter must order
    /// them by lexical scope/declaration position (innermost first), regardless of alpha-renaming or
    /// source (textual) operand order.
    /// </summary>
    [TestMethod]
    public void NestedLambdaScopes_OrderByDepthRegardlessOfNamesOrSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();

        Expression<Func<double, Func<double, double>>> sourceXThenY = x => y => x + y;
        Expression<Func<double, Func<double, double>>> sourceYThenX = outer => inner => inner + outer;

        var result1 = (LambdaExpression)simplifier.Simplify(sourceXThenY);
        var result2 = (LambdaExpression)simplifier.Simplify(sourceYThenX);

        var innerLambda1 = (LambdaExpression)result1.Body;
        var innerLambda2 = (LambdaExpression)result2.Body;
        var body1 = (BinaryExpression)innerLambda1.Body;
        var body2 = (BinaryExpression)innerLambda2.Body;

        // The innermost (depth 0) parameter must sort first in both cases: the inner lambda's own
        // parameter on the Left, the captured outer parameter on the Right.
        Assert.IsTrue(ReferenceEquals(innerLambda1.Parameters[0], body1.Left));
        Assert.IsTrue(ReferenceEquals(result1.Parameters[0], body1.Right));
        Assert.IsTrue(ReferenceEquals(innerLambda2.Parameters[0], body2.Left));
        Assert.IsTrue(ReferenceEquals(result2.Parameters[0], body2.Right));

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Shadowing: the inner lambda redeclares the same parameter name as the outer one; the body must resolve to the inner (shadowing) parameter, proven behaviorally.</summary>
    [TestMethod]
    public void NestedLambdaScopes_Shadowing_ResolvesToInnerParameter()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression outerX = Expression.Parameter(typeof(double), "x");
        ParameterExpression innerX = Expression.Parameter(typeof(double), "x");

        // outer => inner => inner * 2.0   (body references only the shadowing inner "x")
        var innerLambda = Expression.Lambda<Func<double, double>>(Expression.Multiply(innerX, Expression.Constant(2.0)), innerX);
        var source = Expression.Lambda<Func<double, Func<double, double>>>(innerLambda, outerX);

        var result = (LambdaExpression)simplifier.Simplify(source);
        var compiledOuter = (Func<double, Func<double, double>>)result.Compile();
        Func<double, double> compiledInner = compiledOuter(1000.0); // outer value must be irrelevant

        Assert.AreEqual(6.0, compiledInner(3.0));
        Assert.AreEqual(6.0, compiledInner(3.0)); // outer(1000) had no effect; re-invoking confirms determinism
    }

    // ------------------------------------------------------------------------------------------
    // 4. ToString() must not execute during structural canonicalization
    // ------------------------------------------------------------------------------------------

    /// <summary>Marker exception thrown by <see cref="ThrowingExpression"/>'s <see cref="ThrowingExpression.ToString"/>.</summary>
    private sealed class ToStringProbeException : Exception { }

    /// <summary>
    /// A non-reducible <see cref="ExpressionType.Extension"/> node whose <see cref="ToString"/> throws and
    /// records a call count, used as an atomic term beneath ordinary numeric addition/multiplication.
    /// </summary>
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

    /// <summary>An unsupported atomic term beneath an ordinary <c>Add</c> must survive conservatively without <see cref="Expression.ToString()"/> ever being called.</summary>
    [TestMethod]
    public void ThrowingExtensionTerm_UnderAddition_SurvivesWithoutToString()
    {
        var simplifier = new ExpressionSimplifier();
        var throwing = new ThrowingExpression();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var source = Expression.Lambda<Func<double, double>>(Expression.Add(x, throwing), x);

        Expression result = simplifier.Simplify(source);

        Assert.AreEqual(0, throwing.ToStringCallCount);
        Assert.IsTrue(Contains(result, throwing), "The unsupported extension term must survive in the simplified tree.");
    }

    /// <summary>The multiplicative equivalent of <see cref="ThrowingExtensionTerm_UnderAddition_SurvivesWithoutToString"/>.</summary>
    [TestMethod]
    public void ThrowingExtensionTerm_UnderMultiplication_SurvivesWithoutToString()
    {
        var simplifier = new ExpressionSimplifier();
        var throwing = new ThrowingExpression();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var source = Expression.Lambda<Func<double, double>>(Expression.Multiply(x, throwing), x);

        Expression result = simplifier.Simplify(source);

        Assert.AreEqual(0, throwing.ToStringCallCount);
        Assert.IsTrue(Contains(result, throwing));
    }

    /// <summary>
    /// A throwing extension expression used as an argument of a method call that itself participates in
    /// additive canonicalization (the code path that used to reach <c>string.Join&lt;Expression&gt;</c>,
    /// calling every argument's <see cref="Expression.ToString()"/>) must not call <see cref="Expression.ToString()"/>.
    /// </summary>
    [TestMethod]
    public void ThrowingExtensionArgument_OfMethodCallInAdditiveTerm_DoesNotCallToString()
    {
        var simplifier = new ExpressionSimplifier();
        var throwing = new ThrowingExpression();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo max = typeof(double).GetMethod(nameof(double.Max), BindingFlags.Public | BindingFlags.Static, [typeof(double), typeof(double)])!;
        MethodCallExpression call = Expression.Call(max, throwing, x);
        var source = Expression.Lambda<Func<double, double>>(Expression.Add(call, x), x);

        Expression result = simplifier.Simplify(source);

        Assert.AreEqual(0, throwing.ToStringCallCount);
        Assert.IsTrue(Contains(result, throwing));
    }

    /// <summary>Marker exception used by <see cref="HostileConstant"/> to prove its members are never invoked.</summary>
    private sealed class HostileConstantException : Exception { }

    /// <summary>
    /// A reference-type constant value whose <see cref="object.Equals(object?)"/> and
    /// <see cref="object.GetHashCode"/> overrides both record a call and throw. Used to prove that the S4
    /// additive-grouping equality (<c>ExpressionSimplifier.AdditiveGroupingEqualityComparer</c>) never
    /// executes a possibly user-defined <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/>
    /// override on an opaque (non-numeric) constant's boxed value merely to decide whether two additive
    /// terms belong in the same group — see <c>ExpressionComparer.StructuralEqualsRaw</c>/
    /// <c>StructuralHashRaw</c>'s <c>safeConstantsOnly</c> policy.
    /// </summary>
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

    /// <summary>
    /// Two additive terms embedding the SAME <see cref="HostileConstant"/> instance as a method-call
    /// argument force a hash collision in the grouping equality comparer's internal bucketing (since a safe
    /// reference-identity hash is deterministically equal for the same instance), which in turn forces an
    /// actual equality check between them. Neither <see cref="HostileConstant.Equals(object?)"/> nor
    /// <see cref="HostileConstant.GetHashCode"/> must ever be called: grouping must resolve entirely via
    /// reference identity.
    /// </summary>
    [TestMethod]
    public void HostileConstantArgument_InAdditiveGrouping_NeverCallsUserEqualsOrGetHashCode()
    {
        var simplifier = new ExpressionSimplifier();
        var hostile = new HostileConstant();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo identity = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(
            nameof(IdentityFromObject), BindingFlags.NonPublic | BindingFlags.Static)!;

        MethodCallExpression term1 = Expression.Call(identity, Expression.Constant(hostile, typeof(object)));
        MethodCallExpression term2 = Expression.Call(identity, Expression.Constant(hostile, typeof(object)));

        // Deliberately NOT Add(term1, term2) directly: that shape also reaches the pre-existing,
        // S4-unrelated AdditionOfEqualsElements factoring rule, which calls the PUBLIC (non-safe)
        // ExpressionComparer.Default.Equals on the two adjacent addends before canonicalization/grouping
        // ever runs - a separate, out-of-scope hazard this test is not about. Add(Add(term1, x), term2)
        // keeps term1 and term2 non-adjacent at every rule-matching step (their immediate BinaryExpression
        // siblings always differ in NodeType, so that rule's own equality short-circuits without reaching
        // either constant), while CollectAdditiveTerms still flattens the inner Add so term1 and term2 end
        // up compared against each other by the additive-grouping equality this test targets.
        var source = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Add(term1, x), term2), x);

        Expression result = simplifier.Simplify(source);

        Assert.AreEqual(0, hostile.EqualsCallCount, "The grouping equality comparer must never call a constant value's own Equals override.");
        Assert.AreEqual(0, hostile.GetHashCodeCallCount, "The grouping equality comparer must never call a constant value's own GetHashCode override.");
        Assert.IsNotNull(result);
    }

    // ------------------------------------------------------------------------------------------
    // 5. Same textual representation does not imply structural identity
    // ------------------------------------------------------------------------------------------

    /// <summary>Custom addition operator whose behavior (subtraction) deliberately differs from ordinary <c>+</c>, used to prove <see cref="BinaryExpression.Method"/> survives simplification.</summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns><paramref name="a"/> minus <paramref name="b"/>.</returns>
    private static double CustomOpA(double a, double b) => a - b;

    /// <summary>Custom addition operator whose behavior (multiplication) deliberately differs from ordinary <c>+</c> and from <see cref="CustomOpA"/>, used to prove <see cref="BinaryExpression.Method"/> survives simplification.</summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns><paramref name="a"/> times <paramref name="b"/>.</returns>
    private static double CustomOpB(double a, double b) => a * b;

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomOpA"/>.</summary>
    private static readonly MethodInfo CustomOpAMethod =
        typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(CustomOpA), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomOpB"/>.</summary>
    private static readonly MethodInfo CustomOpBMethod =
        typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(CustomOpB), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Two atomic custom-operator expressions built from the same operands but different exact
    /// <see cref="MethodInfo"/> (and therefore identical <see cref="Expression.ToString()"/> text, "a + b")
    /// must remain distinguishable after simplification: the complete structural key must not conflate
    /// them, and each exact method must survive with its own (observably different) runtime behavior.
    /// </summary>
    [TestMethod]
    public void CustomOperator_SameOperands_DifferentMethod_RemainsDistinguishable()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");

        BinaryExpression customA = (BinaryExpression)Expression.MakeBinary(ExpressionType.Add, a, b, false, CustomOpAMethod);
        BinaryExpression customB = (BinaryExpression)Expression.MakeBinary(ExpressionType.Add, a, b, false, CustomOpBMethod);

        var sourceA = Expression.Lambda<Func<double, double, double, double>>(Expression.Add(customA, z), a, b, z);
        var sourceB = Expression.Lambda<Func<double, double, double, double>>(Expression.Add(customB, z), a, b, z);

        var resultA = (Expression<Func<double, double, double, double>>)simplifier.Simplify(sourceA);
        var resultB = (Expression<Func<double, double, double, double>>)simplifier.Simplify(sourceB);

        Assert.IsFalse(ExpressionComparer.Default.Equals(resultA, resultB), "Different custom operator methods over the same operands must not compare structurally equal.");

        var compiledA = resultA.Compile();
        var compiledB = resultB.Compile();
        Assert.AreEqual((3.0 - 4.0) + 5.0, compiledA(3.0, 4.0, 5.0));
        Assert.AreEqual((3.0 * 4.0) + 5.0, compiledB(3.0, 4.0, 5.0));
    }

    // ------------------------------------------------------------------------------------------
    // 6. Exact method-call identity
    // ------------------------------------------------------------------------------------------

    /// <summary>Declares a "Compute(double)" method observably different from <see cref="MathVariantB.Compute(double)"/>, used to prove exact declaring-type identity participates in the canonical key.</summary>
    private static class MathVariantA
    {
        /// <param name="x">The operand.</param>
        /// <returns><paramref name="x"/> plus 1.</returns>
        public static double Compute(double x) => x + 1.0;
    }

    /// <summary>Declares a "Compute(double)" method observably different from <see cref="MathVariantA.Compute(double)"/>, used to prove exact declaring-type identity participates in the canonical key.</summary>
    private static class MathVariantB
    {
        /// <param name="x">The operand.</param>
        /// <returns><paramref name="x"/> plus 2.</returns>
        public static double Compute(double x) => x + 2.0;
    }

    /// <summary>
    /// Two distinct methods sharing the same name and signature ("Compute(double)") but declared on
    /// different types must remain distinguished by the complete canonical key, not merged by name/text.
    /// Reversing the two source terms must produce the same canonical result. Inspects the raw tree
    /// produced by each FIRST <c>Simplify()</c> call directly (rather than relying only on
    /// <see cref="ExpressionComparer.Default"/>, which re-simplifies both sides and could mask a
    /// first-pass canonicalization defect — the exact pitfall identified during the S4 audit).
    /// </summary>
    [TestMethod]
    public void ExactMethodCallIdentity_SameNameDifferentDeclaringType_IsDistinguishedAndOrderIsSymmetric()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo computeA = typeof(MathVariantA).GetMethod(nameof(MathVariantA.Compute))!;
        MethodInfo computeB = typeof(MathVariantB).GetMethod(nameof(MathVariantB.Compute))!;

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");

        var sourceForward = Expression.Lambda<Func<double, double, double>>(
            Expression.Add(Expression.Call(computeA, x), Expression.Call(computeB, y)), x, y);
        var sourceBackward = Expression.Lambda<Func<double, double, double>>(
            Expression.Add(Expression.Call(computeB, y), Expression.Call(computeA, x)), x, y);

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        MethodInfo leftMethodForward = ((MethodCallExpression)bodyForward.Left).Method;
        MethodInfo rightMethodForward = ((MethodCallExpression)bodyForward.Right).Method;
        MethodInfo leftMethodBackward = ((MethodCallExpression)bodyBackward.Left).Method;
        MethodInfo rightMethodBackward = ((MethodCallExpression)bodyBackward.Right).Method;

        Assert.AreEqual(leftMethodForward, leftMethodBackward, "Regardless of source order, the same method must end up on the Left in both results.");
        Assert.AreEqual(rightMethodForward, rightMethodBackward, "Regardless of source order, the same method must end up on the Right in both results.");
        CollectionAssert.AreEquivalent(new[] { computeA, computeB }, new[] { leftMethodForward, rightMethodForward }, "Both exact methods must survive, never merged or dropped.");
    }

    // ------------------------------------------------------------------------------------------
    // 7. Power-wrapped function grouping remains intentional
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Power-wrapped calls sharing the same source arguments/function are grouped adjacent (existing,
    /// intentionally coarser grouping behavior), but the COMPLETE key still distinguishes different
    /// exponents: both exponents must survive, and the result must be deterministic regardless of source
    /// order. Inspects the raw tree produced by each FIRST <c>Simplify()</c> call directly, not only via
    /// <see cref="ExpressionComparer.Default"/> (see the S4-audit pitfall note on
    /// <see cref="ExactMethodCallIdentity_SameNameDifferentDeclaringType_IsDistinguishedAndOrderIsSymmetric"/>).
    /// </summary>
    [TestMethod]
    public void PowerWrappedFunctionGrouping_DistinguishesExponentsInCompleteKey()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo cos = typeof(double).GetMethod(nameof(double.Cos), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

        MethodCallExpression cosX1 = Expression.Call(cos, x);
        MethodCallExpression cosX2 = Expression.Call(cos, x);

        var sourceForward = Expression.Lambda<Func<double, double>>(
            Expression.Add(Expression.Power(cosX1, Expression.Constant(2.0)), Expression.Power(cosX2, Expression.Constant(3.0))), x);
        var sourceBackward = Expression.Lambda<Func<double, double>>(
            Expression.Add(Expression.Power(cosX2, Expression.Constant(3.0)), Expression.Power(cosX1, Expression.Constant(2.0))), x);

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        static double ExponentOf(Expression powerTerm) => (double)((ConstantExpression)((BinaryExpression)powerTerm).Right).Value!;

        double leftExponentForward = ExponentOf(bodyForward.Left);
        double rightExponentForward = ExponentOf(bodyForward.Right);
        double leftExponentBackward = ExponentOf(bodyBackward.Left);
        double rightExponentBackward = ExponentOf(bodyBackward.Right);

        Assert.AreEqual(leftExponentForward, leftExponentBackward, "Regardless of source order, the same exponent must end up on the Left in both results.");
        Assert.AreEqual(rightExponentForward, rightExponentBackward, "Regardless of source order, the same exponent must end up on the Right in both results.");
        CollectionAssert.AreEquivalent(new[] { 2.0, 3.0 }, new[] { leftExponentForward, rightExponentForward }, "Both exponents must survive, never merged or dropped by the coarser grouping.");
    }

    /// <summary>Positive control: <c>sin²(x) + cos²(x)</c> still simplifies to <c>1</c> (existing power-wrapped-function grouping behavior stays intact).</summary>
    [TestMethod]
    public void PowerWrappedFunctionGrouping_SinSquaredPlusCosSquared_StillSimplifiesToOne()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double>> source = x => double.Pow(double.Cos(x), 2.0) + double.Pow(double.Sin(x), 2.0);

        Expression simplified = simplifier.Simplify(source);
        var compiled = ((Expression<Func<double, double>>)simplified).Compile();

        Assert.AreEqual(1.0, compiled(0.7), 1e-9);
    }

    // ------------------------------------------------------------------------------------------
    // Post-review hardening: non-throwing reflection metadata access, and SZ-array/bounded-array
    // identity. Neither is part of the original numbered matrix; both were found during code review
    // of the initial S4 implementation.
    // ------------------------------------------------------------------------------------------

    /// <summary>Reflected <see cref="MethodInfo"/> for the internal <c>ExpressionCanonicalOrder.CompareType(Type, Type)</c> helper, resolved once for the array-identity regression tests below.</summary>
    private static readonly MethodInfo CompareTypeMethod = typeof(ExpressionSimplifier).Assembly
        .GetType("Utils.Mathematics.Expressions.ExpressionCanonicalOrder")!
        .GetMethod("CompareType", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Invokes the internal <c>ExpressionCanonicalOrder.CompareType(Type, Type)</c> helper via reflection, without changing its accessibility.</summary>
    /// <param name="a">The first type.</param>
    /// <param name="b">The second type.</param>
    /// <returns>The comparison result.</returns>
    private static int InvokeCompareType(Type? a, Type? b) => (int)CompareTypeMethod.Invoke(null, [a, b])!;

    /// <summary>
    /// A single-dimensional zero-based ("SZ"/vector) array type (<c>int[]</c>) and a general
    /// single-dimensional array type with explicit bounds (<c>int[*]</c>) both report
    /// <see cref="Type.GetArrayRank"/> <c>== 1</c> and the same element type, but are distinct, non-
    /// interchangeable CLR types. The canonical type comparer must not conflate them.
    /// </summary>
    [TestMethod]
    public void CompareType_DistinguishesSzArrayFromBoundedRank1Array()
    {
        Type szArrayType = typeof(int).MakeArrayType();
        Type boundedArrayType = typeof(int).MakeArrayType(1);

        Assert.AreNotEqual(typeof(int).MakeArrayType(), typeof(int).MakeArrayType(1), "Precondition: the CLR itself must treat these as distinct types.");

        int forward = InvokeCompareType(szArrayType, boundedArrayType);
        int backward = InvokeCompareType(boundedArrayType, szArrayType);

        Assert.AreNotEqual(0, forward, "int[] and int[*] must not compare equal.");
        Assert.AreEqual(Math.Sign(forward), -Math.Sign(backward), "Comparison must be antisymmetric.");
        Assert.AreEqual(0, InvokeCompareType(szArrayType, typeof(int).MakeArrayType()));
        Assert.AreEqual(0, InvokeCompareType(boundedArrayType, typeof(int).MakeArrayType(1)));
    }

    /// <summary>The SZ-array/bounded-array distinction must also hold when the array type is nested inside a constructed generic argument, since that reaches the same comparison through generic-argument recursion.</summary>
    [TestMethod]
    public void CompareType_DistinguishesSzArrayFromBoundedRank1Array_NestedInGenericArgument()
    {
        Type listOfSzArray = typeof(List<>).MakeGenericType(typeof(int).MakeArrayType());
        Type listOfBoundedArray = typeof(List<>).MakeGenericType(typeof(int).MakeArrayType(1));

        Assert.AreNotEqual(0, InvokeCompareType(listOfSzArray, listOfBoundedArray));
    }

    /// <summary>Reflected <see cref="MethodInfo"/> for the internal <c>ExpressionCanonicalOrder.CompareMethod(MethodInfo, MethodInfo)</c> helper.</summary>
    private static readonly MethodInfo CompareMethodMethod = typeof(ExpressionSimplifier).Assembly
        .GetType("Utils.Mathematics.Expressions.ExpressionCanonicalOrder")!
        .GetMethod("CompareMethod", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Invokes the internal <c>ExpressionCanonicalOrder.CompareMethod(MethodInfo, MethodInfo)</c> helper via reflection, without changing its accessibility.</summary>
    /// <param name="a">The first method.</param>
    /// <param name="b">The second method.</param>
    /// <returns>The comparison result.</returns>
    private static int InvokeCompareMethod(MethodInfo? a, MethodInfo? b) => (int)CompareMethodMethod.Invoke(null, [a, b])!;

    /// <summary>
    /// Two dynamically-generated methods (<see cref="System.Reflection.Emit.DynamicMethod"/>) sharing the
    /// same name and signature (and, being owner-less, both a <see langword="null"/>
    /// <see cref="MethodBase.DeclaringType"/>) can reach the canonical key's final member tie-break with an
    /// unbaked <see cref="MemberInfo.MetadataToken"/>, which throws <see cref="InvalidOperationException"/>
    /// for such a method on some runtimes. The comparison must not throw merely because two otherwise-
    /// identical-looking methods happen to be such dynamic methods.
    /// </summary>
    /// <remarks>
    /// Exercises <c>ExpressionCanonicalOrder.CompareMethod</c> directly via reflection rather than through
    /// the full <c>Simplify()</c> pipeline: routing two owner-less <see cref="System.Reflection.Emit.DynamicMethod"/>
    /// calls through <c>Simplify()</c> hits an unrelated, pre-existing gap in
    /// <c>ExpressionCallSignatureAttribute.Match</c> (it dereferences a <see langword="null"/>
    /// <see cref="MethodBase.DeclaringType"/> before this canonical-key code is ever reached) — a
    /// legitimate finding, but a different, out-of-scope bug from the one this test targets.
    /// </remarks>
    [TestMethod]
    public void CompareMethod_TwoDynamicMethodsWithIdenticalSignature_DoesNotThrow()
    {
        var dynamicMethod1 = new System.Reflection.Emit.DynamicMethod("Compute", typeof(double), [typeof(double)]);
        System.Reflection.Emit.ILGenerator il1 = dynamicMethod1.GetILGenerator();
        il1.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        il1.Emit(System.Reflection.Emit.OpCodes.Ret);

        var dynamicMethod2 = new System.Reflection.Emit.DynamicMethod("Compute", typeof(double), [typeof(double)]);
        System.Reflection.Emit.ILGenerator il2 = dynamicMethod2.GetILGenerator();
        il2.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        il2.Emit(System.Reflection.Emit.OpCodes.Ret);

        int forward = InvokeCompareMethod(dynamicMethod1, dynamicMethod2);
        int backward = InvokeCompareMethod(dynamicMethod2, dynamicMethod1);

        Assert.AreEqual(Math.Sign(forward), -Math.Sign(backward), "Comparison must be antisymmetric, even for a conservative tie (0 == -0).");
    }

    /// <summary>
    /// Creates a real, baked, owner-less "global" method (<see cref="MethodBase.DeclaringType"/> is
    /// <see langword="null"/>, exactly like an owner-less <see cref="System.Reflection.Emit.DynamicMethod"/>)
    /// with a genuine <see cref="MemberInfo.MetadataToken"/>, in its own freshly-created dynamic assembly, so
    /// its assembly identity is distinct and controllable. <paramref name="paddingMethodCount"/> additional
    /// no-op global methods are baked into the SAME module BEFORE <c>"Compute"</c>, so its metadata token can
    /// be pushed arbitrarily higher (tokens are assigned in definition order within a module) - used to make
    /// an assembly-name order DISAGREE with a metadata-token order between two such methods.
    /// </summary>
    /// <param name="assemblyName">The dynamic assembly's simple name.</param>
    /// <param name="paddingMethodCount">The number of no-op global methods to bake before <c>"Compute"</c>, raising its token.</param>
    /// <returns>The baked global method, named <c>"Compute"</c>, signature <c>double -&gt; double</c>, static.</returns>
    private static MethodInfo CreateBakedGlobalMethod(string assemblyName, int paddingMethodCount = 0)
    {
        var asmBuilder = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new System.Reflection.AssemblyName(assemblyName), System.Reflection.Emit.AssemblyBuilderAccess.Run);
        System.Reflection.Emit.ModuleBuilder modBuilder = asmBuilder.DefineDynamicModule("MainModule");

        for (int i = 0; i < paddingMethodCount; i++)
        {
            System.Reflection.Emit.MethodBuilder padding = modBuilder.DefineGlobalMethod(
                $"Padding{i}", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
            padding.GetILGenerator().Emit(System.Reflection.Emit.OpCodes.Ret);
        }

        System.Reflection.Emit.MethodBuilder methodBuilder = modBuilder.DefineGlobalMethod(
            "Compute", MethodAttributes.Public | MethodAttributes.Static, typeof(double), [typeof(double)]);
        System.Reflection.Emit.ILGenerator il = methodBuilder.GetILGenerator();
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        il.Emit(System.Reflection.Emit.OpCodes.Ret);
        modBuilder.CreateGlobalFunctions();
        return modBuilder.GetMethod("Compute")!;
    }

    /// <summary>
    /// Reproduces the S4 review round-7 P2 finding directly: <c>CompareFinalMemberTiebreak</c> must compare
    /// assembly identity BEFORE metadata token, not let each pair independently pick whichever dimension
    /// happens to distinguish it. Two real, baked, owner-less "global" methods (see
    /// <see cref="CreateBakedGlobalMethod"/>) are constructed so their ASSEMBLY-name order and their
    /// METADATA-TOKEN order deliberately DISAGREE: <c>methodLowAssemblyHighToken</c> is in an
    /// alphabetically-EARLIER-named assembly but, thanks to padding methods baked before it, has a
    /// numerically-HIGHER token than <c>methodHighAssemblyLowToken</c>, which is in an alphabetically-LATER
    /// assembly with no padding (so its <c>"Compute"</c> keeps the module's very first token). Before this
    /// fix (token compared first, whenever both sides had one and they differed), this pair would compare by
    /// TOKEN - putting the higher-token method (the alphabetically-earlier assembly) AFTER the other,
    /// contradicting assembly order. The fixed dimension order (assembly, then module name, then module
    /// version, then token) now compares by ASSEMBLY first, so the alphabetically-earlier-assembly method
    /// must sort first regardless of its higher token - the opposite conclusion the old, pair-dependent
    /// dimension choice would reach.
    /// </summary>
    [TestMethod]
    public void CompareMethod_MetadataTiebreak_AssemblyOrderTakesPriorityOverContradictingTokenOrder()
    {
        MethodInfo methodLowAssemblyHighToken = CreateBakedGlobalMethod("S4Round7_AAA_LowAssembly", paddingMethodCount: 3);
        MethodInfo methodHighAssemblyLowToken = CreateBakedGlobalMethod("S4Round7_ZZZ_HighAssembly", paddingMethodCount: 0);

        Assert.IsNull(methodLowAssemblyHighToken.DeclaringType);
        Assert.IsNull(methodHighAssemblyLowToken.DeclaringType);
        Assert.IsTrue(methodLowAssemblyHighToken.MetadataToken > methodHighAssemblyLowToken.MetadataToken,
            "Precondition: the padded method's token must be numerically higher.");
        Assert.IsTrue(
            string.CompareOrdinal(methodLowAssemblyHighToken.Module.Assembly.FullName, methodHighAssemblyLowToken.Module.Assembly.FullName) < 0,
            "Precondition: the padded method's assembly name must sort alphabetically earlier.");

        int forward = InvokeCompareMethod(methodLowAssemblyHighToken, methodHighAssemblyLowToken);
        int backward = InvokeCompareMethod(methodHighAssemblyLowToken, methodLowAssemblyHighToken);

        Assert.AreEqual(Math.Sign(forward), -Math.Sign(backward), "Comparison must be antisymmetric.");
        Assert.IsTrue(forward < 0,
            "The alphabetically-earlier-assembly method must sort first, even though its own metadata token is numerically higher - proving assembly is compared before token, not the reverse.");
    }

    /// <summary>
    /// Reproduces the S4 review round-8 P2 finding directly: <c>CompareFinalMemberTiebreak</c> must compare
    /// module VERSION (<see cref="Module.ModuleVersionId"/>) before metadata token, since
    /// <see cref="Module.Name"/> alone is not a reliable module identity - every dynamically-created module
    /// on this runtime reports the identical literal text <c>"&lt;In Memory Module&gt;"</c>, regardless of
    /// which assembly it belongs to (verified below). Two real, baked, owner-less "global" methods are
    /// constructed in two SEPARATE dynamic assemblies deliberately sharing the IDENTICAL assembly-name text
    /// (both dynamic assemblies are given the exact same simple name), the IDENTICAL module-name text, AND
    /// the IDENTICAL metadata token (each is its own module's very first global method) - yet they are
    /// genuinely different members, distinguishable only by their module's <see cref="Module.ModuleVersionId"/>.
    /// Before this fix, such a pair tied (<c>0</c>) on every dimension the tie-break inspected, since it read
    /// only <see cref="Module.Name"/>, never <see cref="Module.ModuleVersionId"/>.
    /// </summary>
    [TestMethod]
    public void CompareMethod_MetadataTiebreak_ModuleVersionTakesPriorityOverSameNameSameToken()
    {
        MethodInfo methodOne = CreateBakedGlobalMethod("S4Round8_SharedAssemblyName");
        MethodInfo methodTwo = CreateBakedGlobalMethod("S4Round8_SharedAssemblyName");

        Assert.AreEqual(methodOne.Module.Assembly.FullName, methodTwo.Module.Assembly.FullName,
            "Precondition: both methods' assembly identity strings must be textually identical.");
        Assert.AreEqual(methodOne.Module.Name, methodTwo.Module.Name,
            "Precondition: both methods' module-name strings must be textually identical.");
        Assert.AreEqual(methodOne.MetadataToken, methodTwo.MetadataToken,
            "Precondition: both methods must share the same metadata token (each is its own module's first global method).");
        Assert.AreNotEqual(methodOne.Module.ModuleVersionId, methodTwo.Module.ModuleVersionId,
            "Precondition: the two methods must still belong to genuinely different module instances.");

        int forward = InvokeCompareMethod(methodOne, methodTwo);
        int backward = InvokeCompareMethod(methodTwo, methodOne);

        Assert.AreEqual(Math.Sign(forward), -Math.Sign(backward), "Comparison must be antisymmetric.");
        Assert.AreNotEqual(0, forward,
            "Two members sharing assembly/module-name/token text but belonging to different module instances must not tie - ModuleVersionId must distinguish them.");
    }

    /// <summary>Reflected <see cref="MethodInfo"/> for the internal <c>ExpressionCanonicalOrder.Compare(Expression, Expression, IReadOnlyList&lt;ParameterExpression[]&gt;)</c> helper.</summary>
    private static readonly MethodInfo CompareExpressionMethod = typeof(ExpressionSimplifier).Assembly
        .GetType("Utils.Mathematics.Expressions.ExpressionCanonicalOrder")!
        .GetMethod("Compare", BindingFlags.NonPublic | BindingFlags.Static, [typeof(Expression), typeof(Expression), typeof(IReadOnlyList<ParameterExpression[]>)])!;

    /// <summary>Invokes the internal <c>ExpressionCanonicalOrder.Compare(Expression, Expression, IReadOnlyList&lt;ParameterExpression[]&gt;)</c> helper via reflection, without changing its accessibility, with no enclosing lexical scope.</summary>
    /// <param name="x">The first expression.</param>
    /// <param name="y">The second expression.</param>
    /// <returns>The comparison result.</returns>
    private static int InvokeCompareExpression(Expression? x, Expression? y) =>
        (int)CompareExpressionMethod.Invoke(null, [x, y, System.Array.Empty<ParameterExpression[]>()])!;

    /// <summary>
    /// End-to-end (structural-key-level) counterpart of
    /// <see cref="CompareMethod_MetadataTiebreak_ModuleVersionTakesPriorityOverSameNameSameToken"/>:
    /// <c>ExpressionCanonicalOrder.Compare</c> - the exact comparison <c>OrderBy</c> uses to canonicalize an
    /// additive term list - must resolve <c>Call(methodOne, x)</c> versus <c>Call(methodTwo, x)</c>
    /// deterministically and antisymmetrically, for two methods distinguishable only by
    /// <see cref="Module.ModuleVersionId"/>. Both calls deliberately share the SAME argument (<c>x</c>) so
    /// the argument-list dimension ties completely, forcing the decision down to <c>MethodCallKey</c>'s own
    /// method comparison, which is where this fix actually lives. Compared directly via
    /// <c>ExpressionCanonicalOrder.Compare</c> rather than through a full <c>Simplify()</c> call: routing an
    /// owner-less method (<see cref="MethodBase.DeclaringType"/> <see langword="null"/>, exactly like these
    /// two baked global methods) through the full pipeline hits the same unrelated, pre-existing
    /// <c>ExpressionCallSignatureAttribute.Match</c> null-<see cref="MethodBase.DeclaringType"/> gap noted on
    /// <see cref="CompareMethod_TwoDynamicMethodsWithIdenticalSignature_DoesNotThrow"/> - a different,
    /// out-of-scope bug from the one this test targets.
    /// </summary>
    [TestMethod]
    public void CompareExpression_CallsOfMethodsDistinguishedOnlyByModuleVersion_OrderDeterministically()
    {
        MethodInfo methodOne = CreateBakedGlobalMethod("S4Round8_EndToEnd_SharedAssemblyName");
        MethodInfo methodTwo = CreateBakedGlobalMethod("S4Round8_EndToEnd_SharedAssemblyName");
        Assert.AreNotEqual(methodOne.Module.ModuleVersionId, methodTwo.Module.ModuleVersionId,
            "Precondition: the two methods must belong to genuinely different module instances.");

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodCallExpression callOne = Expression.Call(methodOne, x);
        MethodCallExpression callTwo = Expression.Call(methodTwo, x);

        int forward = InvokeCompareExpression(callOne, callTwo);
        int backward = InvokeCompareExpression(callTwo, callOne);

        Assert.AreEqual(Math.Sign(forward), -Math.Sign(backward), "Comparison must be antisymmetric.");
        Assert.AreNotEqual(0, forward,
            "Two calls to methods sharing assembly/module-name/token text but belonging to different module instances, with identical arguments, must not tie - the same convergence OrderBy relies on to canonicalize Call(methodOne, x) + Call(methodTwo, x) regardless of source order.");
    }

    // ------------------------------------------------------------------------------------------
    // Post-review hardening, round 2: bool/char/enum constants are known-safe (never call a
    // user-defined Equals/GetHashCode/culture-dependent IComparable) and must therefore still
    // participate in the deterministic complete ORDER key, not fall back to a tie.
    // ------------------------------------------------------------------------------------------

    /// <summary>Test-only method distinguishing its result by a <see cref="bool"/> constant argument, used to prove <see cref="bool"/> constants order deterministically.</summary>
    /// <param name="value">The argument.</param>
    /// <returns>1.0 for <see langword="true"/>, 0.0 for <see langword="false"/>.</returns>
    private static double FromBool(bool value) => value ? 1.0 : 0.0;

    /// <summary>Test-only method distinguishing its result by a <see cref="char"/> constant argument, used to prove <see cref="char"/> constants order deterministically.</summary>
    /// <param name="value">The argument.</param>
    /// <returns>The character's numeric code point.</returns>
    private static double FromChar(char value) => value;

    /// <summary>Test-only enum with two members, used to prove enum constants order deterministically.</summary>
    private enum SampleColor
    {
        /// <summary>The first member.</summary>
        Red,

        /// <summary>The second member.</summary>
        Blue,
    }

    /// <summary>Test-only method distinguishing its result by a <see cref="SampleColor"/> constant argument, used to prove enum constants order deterministically.</summary>
    /// <param name="value">The argument.</param>
    /// <returns>The member's underlying numeric value.</returns>
    private static double FromColor(SampleColor value) => (int)value;

    /// <summary>Extracts the single constant argument's boxed value from a method-call additive term.</summary>
    /// <param name="callTerm">A <see cref="MethodCallExpression"/> with exactly one <see cref="ConstantExpression"/> argument.</param>
    /// <returns>The argument's boxed value.</returns>
    private static object ConstantArgumentOf(Expression callTerm) => ((ConstantExpression)((MethodCallExpression)callTerm).Arguments[0]).Value!;

    /// <summary>
    /// Two additive terms differing only by a <see cref="bool"/> constant argument must canonicalize to the
    /// same relative order regardless of source order: <see cref="bool"/> is a known-safe constant type
    /// (see <c>ExpressionComparer.IsKnownSafeConstantValue</c>), so the complete order key must distinguish
    /// them deterministically rather than tying (which would let source order leak through).
    /// </summary>
    [TestMethod]
    public void ConstantOrdering_BoolConstants_CanonicalizeDeterministicallyRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo fromBool = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromBool), BindingFlags.NonPublic | BindingFlags.Static)!;

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(Expression.Call(fromBool, Expression.Constant(false)), Expression.Call(fromBool, Expression.Constant(true))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(Expression.Call(fromBool, Expression.Constant(true)), Expression.Call(fromBool, Expression.Constant(false))));

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        Assert.AreEqual(ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyBackward.Left));
        Assert.AreEqual(ConstantArgumentOf(bodyForward.Right), ConstantArgumentOf(bodyBackward.Right));
        CollectionAssert.AreEquivalent(new object[] { false, true }, new[] { ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyForward.Right) });
    }

    /// <summary>Character-constant equivalent of <see cref="ConstantOrdering_BoolConstants_CanonicalizeDeterministicallyRegardlessOfSourceOrder"/>.</summary>
    [TestMethod]
    public void ConstantOrdering_CharConstants_CanonicalizeDeterministicallyRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo fromChar = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromChar), BindingFlags.NonPublic | BindingFlags.Static)!;

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(Expression.Call(fromChar, Expression.Constant('a')), Expression.Call(fromChar, Expression.Constant('b'))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(Expression.Call(fromChar, Expression.Constant('b')), Expression.Call(fromChar, Expression.Constant('a'))));

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        Assert.AreEqual(ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyBackward.Left));
        Assert.AreEqual(ConstantArgumentOf(bodyForward.Right), ConstantArgumentOf(bodyBackward.Right));
        CollectionAssert.AreEquivalent(new object[] { 'a', 'b' }, new[] { ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyForward.Right) });
    }

    /// <summary>Enum-constant equivalent of <see cref="ConstantOrdering_BoolConstants_CanonicalizeDeterministicallyRegardlessOfSourceOrder"/>.</summary>
    [TestMethod]
    public void ConstantOrdering_EnumConstants_CanonicalizeDeterministicallyRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo fromColor = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromColor), BindingFlags.NonPublic | BindingFlags.Static)!;

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(Expression.Call(fromColor, Expression.Constant(SampleColor.Red)), Expression.Call(fromColor, Expression.Constant(SampleColor.Blue))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(Expression.Call(fromColor, Expression.Constant(SampleColor.Blue)), Expression.Call(fromColor, Expression.Constant(SampleColor.Red))));

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        Assert.AreEqual(ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyBackward.Left));
        Assert.AreEqual(ConstantArgumentOf(bodyForward.Right), ConstantArgumentOf(bodyBackward.Right));
        CollectionAssert.AreEquivalent(new object[] { SampleColor.Red, SampleColor.Blue }, new[] { ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyForward.Right) });
    }

    // ------------------------------------------------------------------------------------------
    // Post-review hardening, round 3: a ConstantExpression's DECLARED Type can be wider than its
    // boxed value's runtime type (Expression.Constant(value, typeof(object))), which must not mask
    // the runtime-type-based comparisons added in round 2. Also: the "known-safe constant" grouping
    // policy (ExpressionComparer.SafeConstantsOnly) must still group two equal-but-differently-
    // referenced/boxed safe values, not just distinguish unequal ones (round 2's tests only proved
    // the latter).
    // ------------------------------------------------------------------------------------------

    /// <summary>Test-only method accepting an opaque <see cref="object"/> argument, used to embed differently-typed constants declared as <see cref="object"/>.</summary>
    /// <param name="value">An arbitrary opaque value, ignored beyond ordering.</param>
    /// <returns>A constant, arbitrary result.</returns>
    private static double FromObjectConstant(object value) => 1.0;

    /// <summary>
    /// Two additive terms whose constant arguments are individually known-safe (<see cref="bool"/> and
    /// <see cref="string"/>) but declared as <see cref="object"/> — so <see cref="ConstantExpression.Type"/>
    /// is the SAME wide type for both — must still canonicalize deterministically regardless of source
    /// order: the declared type alone must not be allowed to mask the runtime-type distinction.
    /// </summary>
    [TestMethod]
    public void ConstantOrdering_DifferentSafeRuntimeTypesDeclaredAsObject_CanonicalizeDeterministically()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo fromObject = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromObjectConstant), BindingFlags.NonPublic | BindingFlags.Static)!;

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(false, typeof(object))),
                Expression.Call(fromObject, Expression.Constant("x", typeof(object)))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant("x", typeof(object))),
                Expression.Call(fromObject, Expression.Constant(false, typeof(object)))));

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        Assert.AreEqual(ConstantArgumentOf(bodyForward.Left).GetType(), ConstantArgumentOf(bodyBackward.Left).GetType(),
            "Regardless of source order, the same runtime-typed constant must end up on the Left in both results.");
        Assert.AreEqual(ConstantArgumentOf(bodyForward.Right).GetType(), ConstantArgumentOf(bodyBackward.Right).GetType());
    }

    /// <summary>
    /// A boxed numeric constant declared as <see cref="object"/> (<c>Expression.Constant(1, typeof(object))</c>)
    /// must still be recognized as numeric via its runtime type and ordered by exact value, not tie merely
    /// because <see cref="ConstantExpression.Type"/> does not itself indicate a native numeric type.
    /// </summary>
    [TestMethod]
    public void ConstantOrdering_BoxedNumericConstantsDeclaredAsObject_CanonicalizeDeterministically()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo fromObject = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromObjectConstant), BindingFlags.NonPublic | BindingFlags.Static)!;

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(1, typeof(object))),
                Expression.Call(fromObject, Expression.Constant(2, typeof(object)))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(2, typeof(object))),
                Expression.Call(fromObject, Expression.Constant(1, typeof(object)))));

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        Assert.AreEqual(ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyBackward.Left));
        Assert.AreEqual(ConstantArgumentOf(bodyForward.Right), ConstantArgumentOf(bodyBackward.Right));
        CollectionAssert.AreEquivalent(new object[] { 1, 2 }, new[] { ConstantArgumentOf(bodyForward.Left), ConstantArgumentOf(bodyForward.Right) });
    }

    /// <summary>Reflected <see cref="MethodInfo"/> for the internal <c>ExpressionComparer.StructuralEqualsRaw(Expression, Expression)</c> helper.</summary>
    private static readonly MethodInfo StructuralEqualsRawMethod = typeof(ExpressionComparer)
        .GetMethod("StructuralEqualsRaw", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Reflected <see cref="MethodInfo"/> for the internal <c>ExpressionComparer.StructuralHashRaw(Expression)</c> helper.</summary>
    private static readonly MethodInfo StructuralHashRawMethod = typeof(ExpressionComparer)
        .GetMethod("StructuralHashRaw", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Invokes the internal <c>ExpressionComparer.StructuralEqualsRaw(Expression, Expression)</c> helper via reflection, without changing its accessibility.</summary>
    /// <param name="x">The first expression.</param>
    /// <param name="y">The second expression.</param>
    /// <returns>The equality result.</returns>
    private static bool InvokeStructuralEqualsRaw(Expression? x, Expression? y) => (bool)StructuralEqualsRawMethod.Invoke(null, [x, y])!;

    /// <summary>Invokes the internal <c>ExpressionComparer.StructuralHashRaw(Expression)</c> helper via reflection, without changing its accessibility.</summary>
    /// <param name="e">The expression to hash.</param>
    /// <returns>The hash code.</returns>
    private static int InvokeStructuralHashRaw(Expression? e) => (int)StructuralHashRawMethod.Invoke(null, [e])!;

    /// <summary>
    /// Two equal-content <see cref="string"/> constants built from separate character arrays (guaranteed
    /// distinct references) must be recognized as equal — and hash equally — by
    /// <c>ExpressionComparer.StructuralEqualsRaw</c>/<c>StructuralHashRaw</c>, the mechanism S4's additive
    /// grouping uses: <see cref="string"/> is a known-safe constant type, so the round-2 fix restores
    /// grouping-by-value for it rather than leaving it on the conservative reference-identity fallback.
    /// </summary>
    [TestMethod]
    public void StructuralEqualsRaw_EqualButDistinctlyReferencedStringConstants_AreGroupedByValue()
    {
        string left = new(['h', 'e', 'l', 'l', 'o']);
        string right = new(['h', 'e', 'l', 'l', 'o']);
        Assert.AreNotSame(left, right, "Precondition: the two strings must be distinct references sharing equal content.");

        Expression leftConstant = Expression.Constant(left);
        Expression rightConstant = Expression.Constant(right);

        Assert.IsTrue(InvokeStructuralEqualsRaw(leftConstant, rightConstant));
        Assert.AreEqual(InvokeStructuralHashRaw(leftConstant), InvokeStructuralHashRaw(rightConstant));
    }

    /// <summary>Boolean equivalent of <see cref="StructuralEqualsRaw_EqualButDistinctlyReferencedStringConstants_AreGroupedByValue"/>, using two separately-boxed equal <see cref="bool"/> values.</summary>
    [TestMethod]
    public void StructuralEqualsRaw_EqualButSeparatelyBoxedBoolConstants_AreGroupedByValue()
    {
        static object Box(bool value) => value;
        object left = Box(true);
        object right = Box(true);
        Assert.AreNotSame(left, right, "Precondition: the two boxed values must be distinct references sharing an equal value.");

        Expression leftConstant = Expression.Constant(left, typeof(bool));
        Expression rightConstant = Expression.Constant(right, typeof(bool));

        Assert.IsTrue(InvokeStructuralEqualsRaw(leftConstant, rightConstant));
        Assert.AreEqual(InvokeStructuralHashRaw(leftConstant), InvokeStructuralHashRaw(rightConstant));
    }

    /// <summary>Enum equivalent of <see cref="StructuralEqualsRaw_EqualButDistinctlyReferencedStringConstants_AreGroupedByValue"/>, using two separately-boxed equal <see cref="SampleColor"/> values.</summary>
    [TestMethod]
    public void StructuralEqualsRaw_EqualButSeparatelyBoxedEnumConstants_AreGroupedByValue()
    {
        static object Box(SampleColor value) => value;
        object left = Box(SampleColor.Red);
        object right = Box(SampleColor.Red);
        Assert.AreNotSame(left, right, "Precondition: the two boxed values must be distinct references sharing an equal value.");

        Expression leftConstant = Expression.Constant(left, typeof(SampleColor));
        Expression rightConstant = Expression.Constant(right, typeof(SampleColor));

        Assert.IsTrue(InvokeStructuralEqualsRaw(leftConstant, rightConstant));
        Assert.AreEqual(InvokeStructuralHashRaw(leftConstant), InvokeStructuralHashRaw(rightConstant));
    }

    /// <summary>Negative control: two DISTINCT (unequal) opaque, non-known-safe constant values must not be reported equal by the safe grouping policy.</summary>
    [TestMethod]
    public void StructuralEqualsRaw_DistinctOpaqueConstants_AreNotEqual()
    {
        var left = new HostileConstant();
        var right = new HostileConstant();

        Expression leftConstant = Expression.Constant(left, typeof(object));
        Expression rightConstant = Expression.Constant(right, typeof(object));

        Assert.IsFalse(InvokeStructuralEqualsRaw(leftConstant, rightConstant));
        Assert.AreEqual(0, left.EqualsCallCount);
        Assert.AreEqual(0, left.GetHashCodeCallCount);
        Assert.AreEqual(0, right.EqualsCallCount);
        Assert.AreEqual(0, right.GetHashCodeCallCount);
    }

    // ------------------------------------------------------------------------------------------
    // Post-review hardening, round 4: two remaining S4 defects found by re-reading commit 32aac339 (the
    // round-3 fix) against master. (1) The ambient [ThreadStatic] lexical-scope stack (ExpressionSimplifier.
    // _lexicalScopeStack) was populated/consumed only via TransformCore, with no boundary at the PUBLIC
    // Transform()/Simplify() entry point - so a completely independent, unrelated Simplify() call triggered
    // reentrantly from arbitrary code the simplifier does not control (a constant's own Equals() override,
    // reachable through the pre-existing, S4-unrelated AdditionOfEqualsElements rule's non-safe
    // ExpressionComparer.Default comparison) could incorrectly inherit an OUTER, still-in-progress
    // simplification's open lambda scope merely by running on the same thread. (2) ConstantKey's numeric
    // branch returned the bare ExactNumericValue.CompareTo() result the moment both sides were numeric,
    // even when that comparison tied (mathematically equal) but the two constants' underlying RUNTIME types
    // differed (e.g. a declared-object constant boxing an int 1 vs one boxing a double 1.0) - round 3 made
    // TryGetNumericValue recognize such runtime-typed numerics, but never gave CompareSameRank a
    // corresponding tie-break, so two reversed source orderings of these two (structurally unequal, per
    // ExpressionComparer.ConstantsEqual) terms both just kept their own source order instead of converging.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A constant value whose <see cref="object.Equals(object?)"/> override starts a brand-new, independent
    /// <see cref="ExpressionSimplifier"/> simplification, reusing the SAME <see cref="ParameterExpression"/>
    /// instances an OUTER, still-in-progress simplification has bound in its own lambda scope - but combined
    /// into a bare, unwrapped <see cref="BinaryExpression"/> with no enclosing <see cref="LambdaExpression"/>
    /// of its own. Used to prove that the ambient lexical-scope stack a nested <c>Simplify()</c> call
    /// observes is scoped to that call alone: it must never inherit a DIFFERENT, concurrently-open
    /// simplification's scope merely because both happen to run on the same thread.
    /// </summary>
    private sealed class ReentrantScopeProbe
    {
        private readonly ParameterExpression _p0;
        private readonly ParameterExpression _p1;

        /// <summary>Initializes a new <see cref="ReentrantScopeProbe"/> that will reuse <paramref name="p0"/>/<paramref name="p1"/> in its own nested simplification.</summary>
        /// <param name="p0">The outer lambda's first bound parameter, reused as the nested call's second (rightmost) operand.</param>
        /// <param name="p1">The outer lambda's second bound parameter, reused as the nested call's first (leftmost) operand.</param>
        public ReentrantScopeProbe(ParameterExpression p0, ParameterExpression p1)
        {
            _p0 = p0;
            _p1 = p1;
        }

        /// <summary>The independent nested <c>Simplify()</c> call's result, captured the first time <see cref="Equals(object?)"/> runs; <see langword="null"/> until then.</summary>
        public BinaryExpression NestedResult { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (NestedResult is null)
            {
                var nestedSimplifier = new ExpressionSimplifier();
                NestedResult = (BinaryExpression)nestedSimplifier.Simplify(Expression.Add(_p1, _p0));
            }

            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>
    /// Reproduces the S4 review round-4 P2 finding: an independent, reentrantly-triggered
    /// <c>new ExpressionSimplifier().Simplify(...)</c> call must not observe an unrelated OUTER
    /// simplification's still-open ambient lambda scope.
    /// </summary>
    [TestMethod]
    public void Reentrancy_IndependentNestedSimplifyTriggeredDuringPublicComparerEquals_DoesNotLeakAmbientScope()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression p0 = Expression.Parameter(typeof(double), "p0");
        ParameterExpression p1 = Expression.Parameter(typeof(double), "p1");
        var probe = new ReentrantScopeProbe(p0, p1);

        MethodInfo identity = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(
            nameof(IdentityFromObject), BindingFlags.NonPublic | BindingFlags.Static)!;

        // Add(term1, term2), inside a lambda binding p0/p1 (so [p0, p1] is on the ambient stack while this
        // Add node is processed), reaches the pre-existing AdditionOfEqualsElements factoring rule - see
        // HostileConstantArgument_InAdditiveGrouping_NeverCallsUserEqualsOrGetHashCode's comment for why this
        // exact shape reaches ExpressionComparer.Default.Equals(term1, term2) - which, being the PUBLIC
        // (non-safe) comparer, calls probe.Equals(probe) while that outer scope frame is still open.
        MethodCallExpression term1 = Expression.Call(identity, Expression.Constant(probe, typeof(object)));
        MethodCallExpression term2 = Expression.Call(identity, Expression.Constant(probe, typeof(object)));
        var outer = Expression.Lambda<Func<double, double, double>>(Expression.Add(term1, term2), p0, p1);

        simplifier.Simplify(outer);

        Assert.IsNotNull(probe.NestedResult, "Precondition: the reentrant Equals() call must have run.");
        // p1, p0 are free relative to the INDEPENDENT nested Simplify() call: S4 preserves free-parameter
        // source order (see ExpressionCanonicalOrder.ParameterKey.CompareSameRank's free-parameter remarks),
        // so the result must still read (p1, p0) - NOT be reordered to (p0, p1) as if bound by the OUTER
        // lambda's [p0, p1] scope leaking into this unrelated call.
        Assert.IsTrue(ReferenceEquals(p1, probe.NestedResult.Left));
        Assert.IsTrue(ReferenceEquals(p0, probe.NestedResult.Right));
    }

    /// <summary>Test-only method accepting an opaque <see cref="object"/> argument, used to embed differently-runtime-typed boxed numeric constants declared as <see cref="object"/>.</summary>
    /// <param name="value">An arbitrary opaque value, ignored beyond ordering.</param>
    /// <returns>A constant, arbitrary result.</returns>
    private static double FromObjectNumericConstant(object value) => 1.0;

    /// <summary>
    /// Two boxed numeric constants declared as <see cref="object"/> whose exact mathematical values are EQUAL
    /// but whose underlying RUNTIME types differ (<see cref="int"/> <c>1</c> vs <see cref="double"/>
    /// <c>1.0</c>) must still canonicalize deterministically regardless of source order: tying on exact
    /// numeric value alone (as round 3 left it) is not enough, because
    /// <see cref="ExpressionComparer"/>'s own equality does not treat these two as equal (its numeric
    /// fast path only ever consults the DECLARED <see cref="Type"/>, unlike the order key's <c>
    /// TryGetNumericValue</c>), so an order-key tie here means two reversed source orderings of these two
    /// (structurally unequal) terms would otherwise both just keep their own source order.
    /// </summary>
    [TestMethod]
    public void ConstantOrdering_BoxedNumericConstantsDeclaredAsObjectWithDifferentRuntimeTypes_CanonicalizeDeterministically()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo fromObject = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromObjectNumericConstant), BindingFlags.NonPublic | BindingFlags.Static)!;

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(1, typeof(object))),
                Expression.Call(fromObject, Expression.Constant(1.0, typeof(object)))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(1.0, typeof(object))),
                Expression.Call(fromObject, Expression.Constant(1, typeof(object)))));

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        Assert.AreEqual(ConstantArgumentOf(bodyForward.Left).GetType(), ConstantArgumentOf(bodyBackward.Left).GetType(),
            "Regardless of source order, the same runtime-typed constant must end up on the Left in both results.");
        Assert.AreEqual(ConstantArgumentOf(bodyForward.Right).GetType(), ConstantArgumentOf(bodyBackward.Right).GetType());
        CollectionAssert.AreEquivalent(new object[] { typeof(int), typeof(double) },
            new[] { ConstantArgumentOf(bodyForward.Left).GetType(), ConstantArgumentOf(bodyForward.Right).GetType() });
    }

    /// <summary>Returns the <see cref="ConstantExpression.Type"/> (the DECLARED type, as opposed to <see cref="ConstantArgumentOf"/>'s boxed-value RUNTIME type) of a method-call term's sole constant argument.</summary>
    /// <param name="callTerm">The <see cref="MethodCallExpression"/> whose single argument to inspect.</param>
    /// <returns>The argument constant's declared <see cref="Type"/>.</returns>
    private static Type ConstantDeclaredTypeOf(Expression callTerm) => ((ConstantExpression)((MethodCallExpression)callTerm).Arguments[0]).Type;

    /// <summary>
    /// Two constants with the EXACT SAME numeric value AND the SAME boxed RUNTIME type, but DIFFERENT
    /// DECLARED types (<see cref="object"/> vs <see cref="IConvertible"/>), must still canonicalize
    /// deterministically regardless of source order (S4 review, round 5): round 4's runtime-type tie-break
    /// alone is not enough here, since the runtime types themselves also tie (both <see cref="double"/>) -
    /// only the DECLARED types differ, and <c>ExpressionComparer.ConstantsEqual</c>'s fallback path (reached
    /// whenever the fast numeric-value check does not apply to BOTH sides, exactly the scenario here since
    /// neither <see cref="object"/> nor <see cref="IConvertible"/> is itself a native numeric type) requires
    /// <c>x.Type == y.Type</c>, so these two constants are NOT structurally equal despite tying on both
    /// value and runtime type; without a further tie-break, two reversed source orderings of these two
    /// (unequal) terms would each just keep their own source order.
    /// </summary>
    [TestMethod]
    public void ConstantOrdering_EqualNumericValueSameRuntimeTypeDifferentDeclaredTypes_CanonicalizesDeterministically()
    {
        var simplifier = new ExpressionSimplifier();
        MethodInfo fromObject = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromObjectNumericConstant), BindingFlags.NonPublic | BindingFlags.Static)!;

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(1.0, typeof(object))),
                Expression.Call(fromObject, Expression.Constant(1.0, typeof(IConvertible)))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(1.0, typeof(IConvertible))),
                Expression.Call(fromObject, Expression.Constant(1.0, typeof(object)))));

        var resultForward = (LambdaExpression)simplifier.Simplify(sourceForward);
        var resultBackward = (LambdaExpression)simplifier.Simplify(sourceBackward);

        var bodyForward = (BinaryExpression)resultForward.Body;
        var bodyBackward = (BinaryExpression)resultBackward.Body;

        Assert.AreEqual(ConstantDeclaredTypeOf(bodyForward.Left), ConstantDeclaredTypeOf(bodyBackward.Left),
            "Regardless of source order, the same declared-type constant must end up on the Left in both results.");
        Assert.AreEqual(ConstantDeclaredTypeOf(bodyForward.Right), ConstantDeclaredTypeOf(bodyBackward.Right));
        CollectionAssert.AreEquivalent(new[] { typeof(object), typeof(IConvertible) },
            new[] { ConstantDeclaredTypeOf(bodyForward.Left), ConstantDeclaredTypeOf(bodyForward.Right) });
    }

    // ------------------------------------------------------------------------------------------
    // Post-review hardening, round 6/7: FinalizeExpression called CanonicalizeAdditiveExpression/
    // CanonicalizeMultiplicativeExpression unconditionally, while OnEnterLambdaScope/OnExitLambdaScope (which
    // populate the ambient lexical-scope stack these two methods depend on) were gated to
    // GetType() == typeof(ExpressionSimplifier). Since those two hooks no-op for any other runtime type, a
    // subclass reaching additive/multiplicative canonicalization saw an always-empty ambient scope, so its
    // OWN bound ParameterExpressions were silently misclassified as free - a real canonicalization-quality
    // regression (not present before S4, since the removed ToString()-based ordering needed no ambient
    // state) with no prior test coverage for any ExpressionSimplifier subclass processing a bound lambda
    // through these two rules.
    //
    // Round 6 initially worked around this by gating FinalizeExpression's canonicalization dispatch to the
    // exact type too, so a subclass skipped canonicalization for these nodes entirely instead of
    // misclassifying anything - conservative, but itself a NEW, observable behavior change for any
    // ExpressionSimplifier subclass (which had FULL canonicalization pre-S4, via the removed ToString()-based
    // ordering), and it required editing an existing, correct characterization test
    // (ExpressionTransformationRuleBranchTests.Simplify_NegateWithSubstraction_DirectRuleBody_RewritesOperands)
    // to match the new, degraded behavior - exactly the kind of "modify an existing test to accommodate a
    // production behavior change" AGENTS.md asks not to do casually.
    //
    // Round 7 instead fixes the actual root cause: OnEnterLambdaScope/OnExitLambdaScope (and Transform's
    // ambient-scope reset boundary) are now unconditional - there is no pre-S4 historical behavior to
    // preserve for these hooks (they are new to S4, unlike the reconstruction-fidelity guards on
    // RebuildLambdaExpression/RebuildUnaryExpression), and being internal, only a same-assembly subclass
    // could ever reach them anyway. FinalizeExpression's canonicalization dispatch is therefore back to
    // unconditional too (matching its pre-round-6 shape), and
    // Simplify_NegateWithSubstraction_DirectRuleBody_RewritesOperands is back to its original, unmodified
    // assertion - it was correct all along; only the round-6 workaround made it seem otherwise.
    // ------------------------------------------------------------------------------------------

    /// <summary>A minimal <see cref="ExpressionSimplifier"/> subclass with no overrides, used to prove additive/multiplicative canonicalization correctly resolves bound parameters - rather than silently misclassifying them as free - for any non-exact runtime type.</summary>
    private sealed class MinimalDerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>
    /// Reproduces the S4 review round-6/7 finding: <c>new MinimalDerivedSimplifier().Simplify((p0, p1) =&gt; p1 + p0)</c>
    /// (a zero-override <see cref="ExpressionSimplifier"/> subclass) must canonicalize IDENTICALLY to
    /// <see cref="ExpressionSimplifier"/>'s own exact type for the very same source lambda - reordering to
    /// <c>p0 + p1</c> (declaration-position order), NOT staying <c>p1 + p0</c> unchanged as if <c>p0</c>/
    /// <c>p1</c> were free (the round-6-and-earlier bug: an always-empty ambient scope for any subclass) and
    /// NOT skipping canonicalization altogether (round 6's own interim workaround). See
    /// <see cref="PositiveControl_BoundAddition_CanonicalizesRegardlessOfSourceOrder"/> for the exact-type
    /// baseline this subclass must now match.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifier_BoundAddition_CanonicalizesIdenticallyToExactType()
    {
        var simplifier = new MinimalDerivedSimplifier();
        ParameterExpression p0 = Expression.Parameter(typeof(double), "p0");
        ParameterExpression p1 = Expression.Parameter(typeof(double), "p1");
        var source = Expression.Lambda<Func<double, double, double>>(Expression.Add(p1, p0), p0, p1);

        var result = (LambdaExpression)simplifier.Simplify(source);
        var body = (BinaryExpression)result.Body;

        Assert.IsTrue(ReferenceEquals(p0, body.Left), "The subclass must reorder to declaration-position order, matching the exact type - not keep source order (misclassified-as-free) nor skip canonicalization (round 6's interim workaround).");
        Assert.IsTrue(ReferenceEquals(p1, body.Right));
    }

    /// <summary>
    /// Characterizes an already-known, deliberately-deferred order/equality inconsistency (see round 4's
    /// "Noted but deliberately NOT fixed this round" item): two separately-boxed constants with the SAME
    /// exact numeric value AND the SAME declared type (both <see cref="object"/>, both boxing <c>1.0</c>)
    /// still tie in <c>ConstantKey.CompareSameRank</c> - the declared-type and runtime-type tie-breaks both
    /// find no difference, since both are genuinely identical on every axis the order key inspects except
    /// object identity, which it deliberately never uses (see this file's remarks on free
    /// parameters/opaque constants). This does NOT translate into an observable
    /// <see cref="ExpressionComparer.Default"/> regression, unlike the round 3-5 findings this test's
    /// sibling tests cover: <see cref="ExpressionComparer.ConstantsEqual"/>'s PUBLIC (non-safe) path calls
    /// <c>1.0.Equals(1.0)</c>, which is <see langword="true"/> regardless of which boxed instance ends up on
    /// which side, so positional comparison already succeeds without ever needing the commutative-swap
    /// fallback - the two reversed source orderings below are correctly reported equal by the public
    /// comparer even though their canonical trees are not necessarily identical.
    /// </summary>
    [TestMethod]
    public void ConstantOrdering_SameDeclaredTypeDistinctlyBoxedEqualNumericConstants_PublicComparerStillAgrees()
    {
        MethodInfo fromObject = typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(FromObjectNumericConstant), BindingFlags.NonPublic | BindingFlags.Static)!;

        object boxA = 1.0;
        object boxB = 1.0;
        Assert.AreNotSame(boxA, boxB, "Precondition: the two boxed values must be distinct references sharing an equal value.");

        var sourceForward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(boxA, typeof(object))),
                Expression.Call(fromObject, Expression.Constant(boxB, typeof(object)))));
        var sourceBackward = Expression.Lambda<Func<double>>(
            Expression.Add(
                Expression.Call(fromObject, Expression.Constant(boxB, typeof(object))),
                Expression.Call(fromObject, Expression.Constant(boxA, typeof(object)))));

        Assert.IsTrue(ExpressionComparer.Default.Equals(sourceForward, sourceBackward));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(sourceForward), ExpressionComparer.Default.GetHashCode(sourceBackward));
    }

    // ------------------------------------------------------------------------------------------
    // 8. Ordinary positive controls
    // ------------------------------------------------------------------------------------------

    /// <summary>Ordinary bound-parameter addition canonicalizes regardless of source order.</summary>
    [TestMethod]
    public void PositiveControl_BoundAddition_CanonicalizesRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source1 = (a, b) => b + a;
        Expression<Func<double, double, double>> source2 = (a, b) => a + b;

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Ordinary bound-parameter multiplication canonicalizes regardless of source order.</summary>
    [TestMethod]
    public void PositiveControl_BoundMultiplication_CanonicalizesRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source1 = (a, b) => b * a;
        Expression<Func<double, double, double>> source2 = (a, b) => a * b;

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Three-term right-association: <c>(a + b) + c</c> canonicalizes to the same tree regardless of source grouping.</summary>
    [TestMethod]
    public void PositiveControl_ThreeTermAddition_RightAssociatesDeterministically()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double, double>> source1 = (a, b, c) => (a + b) + c;
        Expression<Func<double, double, double, double>> source2 = (a, b, c) => a + (b + c);

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Subtraction sign handling survives canonicalization: <c>b - a</c> and <c>-a + b</c> are equivalent.</summary>
    [TestMethod]
    public void PositiveControl_SubtractionSignHandling()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source1 = (a, b) => b - a;
        Expression<Func<double, double, double>> source2 = (a, b) => -a + b;

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Sin/Cos ordering positive control.</summary>
    [TestMethod]
    public void PositiveControl_SinCosOrdering()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double>> source1 = x => double.Sin(x) + double.Cos(x);
        Expression<Func<double, double>> source2 = x => double.Cos(x) + double.Sin(x);

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Max/Min call ordering positive control.</summary>
    [TestMethod]
    public void PositiveControl_MaxMinOrdering()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source1 = (x, y) => double.Min(x, y) + double.Max(x, y);
        Expression<Func<double, double, double>> source2 = (x, y) => double.Max(x, y) + double.Min(x, y);

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Decimal positive control: bound decimal addition canonicalizes regardless of source order.</summary>
    [TestMethod]
    public void PositiveControl_DecimalAddition_CanonicalizesRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<decimal, decimal, decimal>> source1 = (a, b) => b + a;
        Expression<Func<decimal, decimal, decimal>> source2 = (a, b) => a + b;

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
    }

    /// <summary>Custom/lifted operator safety (S3) survives S4's ordering change: a custom-method Add stays atomic and its order is still deterministic.</summary>
    [TestMethod]
    public void PositiveControl_CustomOperatorTerm_StaysAtomicAndOrderIsDeterministic()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        BinaryExpression customAdd = (BinaryExpression)Expression.MakeBinary(ExpressionType.Add, a, b, false, CustomOpAMethod);

        var source1 = Expression.Lambda<Func<double, double, double, double>>(Expression.Add(customAdd, z), a, b, z);
        var source2 = Expression.Lambda<Func<double, double, double, double>>(Expression.Add(z, customAdd), a, b, z);

        Expression result1 = simplifier.Simplify(source1);
        Expression result2 = simplifier.Simplify(source2);

        Assert.AreEqual(result1, result2, ExpressionComparer.Default);
        Assert.IsTrue(EnumerateNodes(result1).OfType<BinaryExpression>().Any(be => be.Method == CustomOpAMethod));
    }

    /// <summary>
    /// Lifted nullable arithmetic (S3) is preserved under S4: <c>int?</c> addition is lifted
    /// (<see cref="BinaryExpression.IsLifted"/>), which <c>IsOrdinaryBinaryArithmetic</c> deliberately
    /// excludes from canonicalization/reordering entirely (see the S3 roadmap entry) — S4 must not start
    /// reordering it. Both source orders must independently keep correct <see langword="null"/>-propagating
    /// behavior.
    /// </summary>
    [TestMethod]
    public void PositiveControl_LiftedNullableAddition_PreservesNullBehaviorBothSourceOrders()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<int?, int?, int?>> source1 = (a, b) => b + a;
        Expression<Func<int?, int?, int?>> source2 = (a, b) => a + b;

        var compiled1 = ((Expression<Func<int?, int?, int?>>)simplifier.Simplify(source1)).Compile();
        var compiled2 = ((Expression<Func<int?, int?, int?>>)simplifier.Simplify(source2)).Compile();

        Assert.IsNull(compiled1(null, 5));
        Assert.IsNull(compiled2(null, 5));
        Assert.AreEqual(7, compiled1(3, 4));
        Assert.AreEqual(7, compiled2(3, 4));
    }

    // ------------------------------------------------------------------------------------------
    // 9. Unsupported node conservatism
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A <see cref="ConditionalExpression"/> term beneath an ordinary addition survives conservatively and
    /// participates in deterministic (rank-based) ordering against a supported sibling term. The rebuilt
    /// result is checked structurally (node shape/position), not via <see cref="ExpressionComparer.Default"/>
    /// or reference identity: <see cref="ExpressionTransformer"/> always rebuilds a fresh
    /// <see cref="ConditionalExpression"/> instance (even when unchanged), and <see cref="ExpressionComparer"/>
    /// conservatively never proves two <see cref="ConditionalExpression"/> instances structurally equal
    /// (an unsupported node kind for it too) — neither of those is an S4 regression.
    /// </summary>
    [TestMethod]
    public void ConditionalExpressionTerm_SurvivesConservativelyBesideSupportedTerm()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ConditionalExpression conditional = Expression.Condition(
            Expression.GreaterThan(x, Expression.Constant(0.0)), y, Expression.Constant(-1.0));

        var source1 = Expression.Lambda<Func<double, double, double>>(Expression.Add(conditional, x), x, y);
        var source2 = Expression.Lambda<Func<double, double, double>>(Expression.Add(x, conditional), x, y);

        var result1 = (LambdaExpression)simplifier.Simplify(source1);
        var result2 = (LambdaExpression)simplifier.Simplify(source2);

        var body1 = (BinaryExpression)result1.Body;
        var body2 = (BinaryExpression)result2.Body;

        // The supported Parameter term (rank below Unsupported) must sort first in both cases, regardless
        // of source order.
        Assert.IsTrue(ReferenceEquals(result1.Parameters[0], body1.Left));
        Assert.IsInstanceOfType(body1.Right, typeof(ConditionalExpression));
        Assert.IsTrue(ReferenceEquals(result2.Parameters[0], body2.Left));
        Assert.IsInstanceOfType(body2.Right, typeof(ConditionalExpression));
    }

    /// <summary>
    /// Two structurally distinct <see cref="ConditionalExpression"/> terms must never be claimed
    /// structurally equal merely because both are unsupported; both must survive independently as two
    /// distinguishable nodes, not merged into one.
    /// </summary>
    [TestMethod]
    public void TwoDistinctConditionalTerms_NeverConflated()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConditionalExpression conditional1 = Expression.Condition(
            Expression.GreaterThan(x, Expression.Constant(0.0)), Expression.Constant(1.0), Expression.Constant(-1.0));
        ConditionalExpression conditional2 = Expression.Condition(
            Expression.LessThan(x, Expression.Constant(0.0)), Expression.Constant(2.0), Expression.Constant(-2.0));

        var source = Expression.Lambda<Func<double, double>>(Expression.Add(conditional1, conditional2), x);

        Expression result = simplifier.Simplify(source);

        List<ConditionalExpression> conditionals = EnumerateNodes(result).OfType<ConditionalExpression>().ToList();
        Assert.AreEqual(2, conditionals.Count, "Both distinct conditional terms must survive as two separate nodes, never merged.");
        Assert.IsTrue(conditionals.Any(c => c.Test.NodeType == ExpressionType.GreaterThan));
        Assert.IsTrue(conditionals.Any(c => c.Test.NodeType == ExpressionType.LessThan));
    }

    // ------------------------------------------------------------------------------------------
    // 10. Concurrency and re-entrancy
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Many concurrent calls through the SAME shared <see cref="ExpressionSimplifier"/> instance, with
    /// differently-scoped/named lambdas, must not leak lexical scope state between calls: every call must
    /// still resolve binding-position order correctly.
    /// </summary>
    [TestMethod]
    public void Concurrency_ManyThreadsSharedSimplifier_NoScopeStateLeakage()
    {
        var simplifier = new ExpressionSimplifier();
        const int iterations = 300;

        Parallel.For(0, iterations, i =>
        {
            if (i % 2 == 0)
            {
                ParameterExpression p0 = Expression.Parameter(typeof(double), "p0");
                ParameterExpression p1 = Expression.Parameter(typeof(double), "p1");
                var source = Expression.Lambda<Func<double, double, double>>(Expression.Add(p1, p0), p0, p1);

                var result = (LambdaExpression)simplifier.Simplify(source);
                var (leftPosition, rightPosition) = GetOperandPositions(result);
                Assert.AreEqual(0, leftPosition);
                Assert.AreEqual(1, rightPosition);
            }
            else
            {
                Expression<Func<double, Func<double, double>>> source = x => y => x + y;
                var result = (LambdaExpression)simplifier.Simplify(source);
                var innerLambda = (LambdaExpression)result.Body;
                var body = (BinaryExpression)innerLambda.Body;

                Assert.IsTrue(ReferenceEquals(innerLambda.Parameters[0], body.Left));
                Assert.IsTrue(ReferenceEquals(result.Parameters[0], body.Right));
            }
        });
    }

    /// <summary>
    /// Re-entrancy through <see cref="ExpressionComparer.Default"/>: a custom-operator zero/factoring rule
    /// invokes <see cref="ExpressionComparer.Default"/>, which simplifies its own operands, re-entering the
    /// simplifier while an outer additive canonicalization is in progress. The outer canonicalization must
    /// still resolve correctly afterward.
    /// </summary>
    [TestMethod]
    public void Reentrancy_ThroughExpressionComparerDefault_OuterCanonicalizationStillCorrect()
    {
        var simplifier = new ExpressionSimplifier();

        // a + b - a triggers AdditionOfEqualsElements/cancellation, which calls
        // ExpressionComparer.Default.Equals internally while this outer Add/Subtract node is still being
        // canonicalized, re-entering Simplify on sub-expressions of the very tree being simplified.
        Expression<Func<double, double, double>> source = (a, b) => (a + b) - a;

        Expression result = simplifier.Simplify(source);
        var compiled = ((Expression<Func<double, double, double>>)result).Compile();

        Assert.AreEqual(4.0, compiled(3.0, 4.0));
    }
}
