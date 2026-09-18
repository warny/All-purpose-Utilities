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

    private static bool Contains(Expression root, Expression target) => EnumerateNodes(root).Any(n => ReferenceEquals(n, target));

    private static IEnumerable<double> EnumerateConstantDoubles(Expression root) =>
        EnumerateNodes(root).OfType<ConstantExpression>().Where(c => c.Type == typeof(double)).Select(c => (double)c.Value!);

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
        public int ToStringCallCount { get; private set; }
        public override Type Type => typeof(double);
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override bool CanReduce => false;

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

    // ------------------------------------------------------------------------------------------
    // 5. Same textual representation does not imply structural identity
    // ------------------------------------------------------------------------------------------

    private static double CustomOpA(double a, double b) => a - b;
    private static double CustomOpB(double a, double b) => a * b;

    private static readonly MethodInfo CustomOpAMethod =
        typeof(ExpressionSimplifierStructuralCanonicalizationTests).GetMethod(nameof(CustomOpA), BindingFlags.NonPublic | BindingFlags.Static)!;

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

    private static class MathVariantA
    {
        public static double Compute(double x) => x + 1.0;
    }

    private static class MathVariantB
    {
        public static double Compute(double x) => x + 2.0;
    }

    /// <summary>
    /// Two distinct methods sharing the same name and signature ("Compute(double)") but declared on
    /// different types must remain distinguished by the complete canonical key, not merged by name/text.
    /// Reversing the two source terms must produce the same canonical result.
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

        var resultForward = simplifier.Simplify(sourceForward);
        var resultBackward = simplifier.Simplify(sourceBackward);

        Assert.AreEqual(resultForward, resultBackward, ExpressionComparer.Default);

        var callsForward = EnumerateNodes(resultForward).OfType<MethodCallExpression>().Select(c => c.Method).ToList();
        CollectionAssert.Contains(callsForward, computeA);
        CollectionAssert.Contains(callsForward, computeB);
    }

    // ------------------------------------------------------------------------------------------
    // 7. Power-wrapped function grouping remains intentional
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Power-wrapped calls sharing the same source arguments/function are grouped adjacent (existing,
    /// intentionally coarser grouping behavior), but the COMPLETE key still distinguishes different
    /// exponents: both exponents must survive, and the result must be deterministic regardless of source
    /// order.
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

        var resultForward = simplifier.Simplify(sourceForward);
        var resultBackward = simplifier.Simplify(sourceBackward);

        Assert.AreEqual(resultForward, resultBackward, ExpressionComparer.Default);

        List<double> exponents = EnumerateConstantDoubles(resultForward).Where(v => v is 2.0 or 3.0).OrderBy(v => v).ToList();
        CollectionAssert.AreEqual(new[] { 2.0, 3.0 }, exponents);
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
