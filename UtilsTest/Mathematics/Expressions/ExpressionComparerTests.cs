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
/// Characterization and regression coverage for <see cref="ExpressionComparer"/>, the structural equality
/// primitive <see cref="ExpressionSimplifier"/> relies on to decide whether algebraic rewrites are legal
/// (S1 of <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>). Several tests here are
/// documented as "regression": on the pre-#590 baseline they either throw or return the wrong boolean,
/// and only pass once the hardened comparer lands. Others are "preservation": they must pass both before
/// and after. Every test constructs its own expressions deterministically; no fuzz input is used.
/// </summary>
[TestClass]
public class ExpressionComparerTests
{
    /// <summary>Test-only type with two distinct public properties, used for <see cref="MemberExpression"/> scenarios.</summary>
    private sealed class SampleContainer
    {
        /// <summary>An arbitrary first property, distinct from <see cref="Second"/> for member-identity tests.</summary>
        public int First { get; init; }

        /// <summary>An arbitrary second property, distinct from <see cref="First"/> for member-identity tests.</summary>
        public int Second { get; init; }
    }

    /// <summary>A static method with an observably different implementation from <see cref="CustomAddB"/>, used to prove that <see cref="BinaryExpression.Method"/> participates in equality.</summary>
    /// <param name="x">First operand.</param>
    /// <param name="y">Second operand.</param>
    /// <returns><paramref name="x"/> plus <paramref name="y"/>.</returns>
    private static double CustomAddA(double x, double y) => x + y;

    /// <summary>A static method with an observably different implementation from <see cref="CustomAddA"/>, used to prove that <see cref="BinaryExpression.Method"/> participates in equality.</summary>
    /// <param name="x">First operand.</param>
    /// <param name="y">Second operand.</param>
    /// <returns><paramref name="x"/> plus <paramref name="y"/>, doubled, so the two methods are observably different.</returns>
    private static double CustomAddB(double x, double y) => (x + y) * 2;

    private static readonly MethodInfo CustomAddAMethod =
        typeof(ExpressionComparerTests).GetMethod(nameof(CustomAddA), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CustomAddBMethod =
        typeof(ExpressionComparerTests).GetMethod(nameof(CustomAddB), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Creates a fresh <see cref="double"/> parameter, distinct from every other call even when given the same name.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>A new <see cref="double"/> parameter expression.</returns>
    private static ParameterExpression P(string name) => Expression.Parameter(typeof(double), name);

    // ------------------------------------------------------------------------------------------
    // Finding 1 - null / reflexivity contract
    // ------------------------------------------------------------------------------------------

    /// <summary>Regression: the baseline forwards both nulls into <see cref="ExpressionSimplifier.Simplify"/> and throws; the fixed comparer must follow ordinary <see cref="IEqualityComparer{T}"/> semantics.</summary>
    [TestMethod]
    public void Equals_NullNull_ReturnsTrue()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(null, null));
    }

    /// <summary>Regression: comparing null against a non-null expression must return false, not throw.</summary>
    [TestMethod]
    public void Equals_NullNonNull_ReturnsFalse()
    {
        Assert.IsFalse(ExpressionComparer.Default.Equals(null, Expression.Constant(1.0)));
    }

    /// <summary>Regression: comparing a non-null expression against null must return false, not throw.</summary>
    [TestMethod]
    public void Equals_NonNullNull_ReturnsFalse()
    {
        Assert.IsFalse(ExpressionComparer.Default.Equals(Expression.Constant(1.0), null));
    }

    /// <summary>
    /// Regression: <see cref="ExpressionSimplifier"/> has no structural support for <see cref="NewExpression"/>,
    /// so comparing two independently-simplified (and therefore no-longer-reference-equal) instances falls
    /// through to a conservative "false". The exact same source reference passed as both arguments must
    /// still compare equal to itself, via a reference check performed before simplification even runs.
    /// </summary>
    [TestMethod]
    public void Equals_UnsupportedNodeSameReference_ReturnsTrue()
    {
        NewExpression newExpression = Expression.New(typeof(object));

        Assert.IsTrue(ExpressionComparer.Default.Equals(newExpression, newExpression));
    }

    /// <summary>Preservation: two distinct, structurally identical but unsupported nodes remain conservatively unequal (not a regression fix target).</summary>
    [TestMethod]
    public void Equals_UnsupportedNodeDistinctInstances_ReturnsFalse()
    {
        NewExpression a = Expression.New(typeof(object));
        NewExpression b = Expression.New(typeof(object));

        Assert.IsFalse(ExpressionComparer.Default.Equals(a, b));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 1 (caveat) / Finding 2 - scope-aware parameter binding, not name-based
    // ------------------------------------------------------------------------------------------

    /// <summary>Preservation: alpha-equivalent lambdas using differently-named parameter instances compare equal.</summary>
    [TestMethod]
    public void SimpleAlphaEquivalence_DifferentParameterInstances_ReturnsTrue()
    {
        Expression<Func<double, double>> left = x => x + 1;
        Expression<Func<double, double>> right = y => y + 1;

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>
    /// Regression: on the baseline, parameter matching is done by <see cref="ParameterExpression.Name"/>.
    /// With both parameters on both sides sharing the same name ("v"), <c>List.IndexOf</c> resolves every
    /// lookup to the first same-named entry regardless of which instance is actually being asked about,
    /// so the baseline reports "p1 equivalent to q0" (both resolve to index 0) even though the right-hand
    /// body deliberately reuses its first parameter twice. The fixed comparer must bind by declaration
    /// position/instance, not by name, and correctly report inequivalence.
    /// </summary>
    [TestMethod]
    public void DuplicateParameterNames_DifferentMeaning_ReturnsFalse()
    {
        ParameterExpression p0 = P("v");
        ParameterExpression p1 = P("v");
        ParameterExpression q0 = P("v");
        ParameterExpression q1 = P("v");

        var left = Expression.Lambda<Func<double, double, double>>(Expression.Add(p0, p1), p0, p1);
        var right = Expression.Lambda<Func<double, double, double>>(Expression.Add(q0, q0), q0, q1);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>
    /// Regression: reusing the very same <see cref="ParameterExpression"/> instances <c>p</c>/<c>q</c> but
    /// declaring them in swapped positions changes which argument the shared body expression refers to.
    /// A naive recursive <c>ReferenceEquals(x, y)</c> shortcut evaluated before parameter-binding logic
    /// would incorrectly treat the (identical-object) body as trivially equal to itself; binding must be
    /// resolved by declaration position first.
    /// </summary>
    [TestMethod]
    public void SameParameterInstances_SwappedDeclarationPositions_ReturnsFalse()
    {
        ParameterExpression p = P("v");
        ParameterExpression q = P("v");

        var left = Expression.Lambda<Func<double, double, double>>(p, p, q);
        var right = Expression.Lambda<Func<double, double, double>>(p, q, p);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>
    /// Regression: the baseline's recursive <c>Equals</c> recomputes parameter arrays fresh at every nested
    /// <see cref="LambdaExpression"/> it encounters, discarding whatever scope was passed in, so the outer
    /// binding (<c>x</c>/<c>a</c>) is lost by the time the inner body (<c>x + y</c> / <c>a + b</c>) is
    /// compared. The fixed comparer must keep the outer binding visible while the inner lambda body is
    /// being compared.
    /// </summary>
    [TestMethod]
    public void NestedCapturedAlphaEquivalence_ReturnsTrue()
    {
        Expression<Func<double, Func<double, double>>> left = x => y => x + y;
        Expression<Func<double, Func<double, double>>> right = a => b => a + b;

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Preservation/required: an inner lambda re-declaring a parameter with the same name as an outer one must still bind by instance/scope, not by text.</summary>
    [TestMethod]
    public void NestedShadowing_SameNameDifferentInstance_ReturnsTrue()
    {
        ParameterExpression outerLeft = P("x");
        ParameterExpression innerLeft = P("x");
        ParameterExpression outerRight = P("x");
        ParameterExpression innerRight = P("x");

        var left = Expression.Lambda<Func<double, Func<double, double>>>(
            Expression.Lambda(Expression.Add(outerLeft, innerLeft), innerLeft), outerLeft);
        var right = Expression.Lambda<Func<double, Func<double, double>>>(
            Expression.Lambda(Expression.Add(outerRight, innerRight), innerRight), outerRight);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Required: alpha-equivalent lambdas whose parameters both have a null <see cref="ParameterExpression.Name"/> must still compare correctly.</summary>
    [TestMethod]
    public void NullParameterNames_AlphaEquivalent_ReturnsTrue()
    {
        ParameterExpression left = Expression.Parameter(typeof(double));
        ParameterExpression right = Expression.Parameter(typeof(double));
        Assert.IsNull(left.Name);
        Assert.IsNull(right.Name);

        var leftLambda = Expression.Lambda<Func<double, double>>(Expression.Add(left, Expression.Constant(1.0)), left);
        var rightLambda = Expression.Lambda<Func<double, double>>(Expression.Add(right, Expression.Constant(1.0)), right);

        Assert.IsTrue(ExpressionComparer.Default.Equals(leftLambda, rightLambda));
    }

    /// <summary>Preservation: two unrelated free (unbound) parameters, compared directly with no enclosing lambda, are only equal by reference.</summary>
    [TestMethod]
    public void FreeParameters_DistinctInstances_ReturnsFalse()
    {
        ParameterExpression x = P("x");
        ParameterExpression z = P("x");

        Assert.IsFalse(ExpressionComparer.Default.Equals(x, z));
    }

    /// <summary>Preservation: a free parameter compared against the exact same instance is equal.</summary>
    [TestMethod]
    public void FreeParameters_SameInstance_ReturnsTrue()
    {
        ParameterExpression x = P("x");

        Assert.IsTrue(ExpressionComparer.Default.Equals(x, x));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 3 - lambda metadata
    // ------------------------------------------------------------------------------------------

    /// <summary>Required: lambdas with a different <see cref="LambdaExpression.TailCall"/> flag are not equal.</summary>
    [TestMethod]
    public void Lambda_DifferentTailCall_ReturnsFalse()
    {
        ParameterExpression x = P("x");
        ParameterExpression y = P("y");

        var left = Expression.Lambda(Expression.Add(x, Expression.Constant(1.0)), tailCall: false, x);
        var right = Expression.Lambda(Expression.Add(y, Expression.Constant(1.0)), tailCall: true, y);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>A delegate type distinct from <see cref="DelegateB"/> despite an identical <c>double -&gt; double</c> signature.</summary>
    /// <param name="x">The operand.</param>
    /// <returns>A <see cref="double"/> result.</returns>
    private delegate double DelegateA(double x);

    /// <summary>A delegate type distinct from <see cref="DelegateA"/> despite an identical <c>double -&gt; double</c> signature.</summary>
    /// <param name="x">The operand.</param>
    /// <returns>A <see cref="double"/> result.</returns>
    private delegate double DelegateB(double x);

    /// <summary>
    /// Regression: <c>ExpressionTransformer.PrepareLambda</c> rebuilds every lambda via the type-inferring
    /// <c>Expression.Lambda(body, parameters)</c> overload, which always produces a <c>Func&lt;...&gt;</c> or
    /// <c>Action&lt;...&gt;</c> delegate type - so a root lambda's original custom delegate type
    /// (<see cref="DelegateA"/> vs <see cref="DelegateB"/>, both structurally <c>double -&gt; double</c>) is
    /// erased by the time the two simplified lambdas reach the generic <c>x.Type != y.Type</c> check, which
    /// then sees the same inferred <c>Func&lt;double, double&gt;</c> on both sides. The root delegate type
    /// must therefore be compared on the two ORIGINAL, not-yet-simplified lambdas, exactly like
    /// <see cref="LambdaExpression.TailCall"/>.
    /// </summary>
    [TestMethod]
    public void Lambda_DifferentCustomDelegateType_ReturnsFalse()
    {
        ParameterExpression x = P("x");
        ParameterExpression y = P("y");

        Expression<DelegateA> left = Expression.Lambda<DelegateA>(Expression.Add(x, Expression.Constant(1.0)), x);
        Expression<DelegateB> right = Expression.Lambda<DelegateB>(Expression.Add(y, Expression.Constant(1.0)), y);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 4 - unary metadata
    // ------------------------------------------------------------------------------------------

    /// <summary>Preservation: two unary conversions to the same target type, over alpha-equivalent operands, are equal.</summary>
    [TestMethod]
    public void Unary_SameOperandAndTarget_ReturnsTrue()
    {
        ParameterExpression x = P("x");
        ParameterExpression y = P("y");

        var left = Expression.Lambda<Func<double, long>>(Expression.Convert(x, typeof(long)), x);
        var right = Expression.Lambda<Func<double, long>>(Expression.Convert(y, typeof(long)), y);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Required: two unary conversions with the same operand but a different target type are not equal (caught by the node's own <see cref="Expression.Type"/>).</summary>
    [TestMethod]
    public void Unary_SameOperandDifferentTargetType_ReturnsFalse()
    {
        ParameterExpression x = P("x");
        ParameterExpression y = P("y");

        var left = Expression.Lambda<Func<double, long>>(Expression.Convert(x, typeof(long)), x);
        var right = Expression.Lambda<Func<double, int>>(Expression.Convert(y, typeof(int)), y);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 5 - binary metadata (custom operator Method)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Mandatory regression: the baseline binary comparison only recurses into <c>Left</c>/<c>Right</c> and
    /// ignores <see cref="BinaryExpression.Method"/> entirely, so two <see cref="ExpressionType.Add"/> nodes
    /// built from observably different custom operator methods, but with the same operands, were reported
    /// as equal - a false positive that could let the simplifier apply an unsafe rewrite. The fixed
    /// comparer must require the same operator method.
    /// </summary>
    [TestMethod]
    public void Binary_SameOperandsDifferentMethod_ReturnsFalse()
    {
        ParameterExpression x = P("x");
        ParameterExpression y = P("y");

        BinaryExpression a = Expression.MakeBinary(ExpressionType.Add, x, y, liftToNull: false, CustomAddAMethod);
        BinaryExpression b = Expression.MakeBinary(ExpressionType.Add, x, y, liftToNull: false, CustomAddBMethod);

        Assert.IsFalse(ExpressionComparer.Default.Equals(a, b));
    }

    /// <summary>Preservation: two binary nodes built from the same custom operator method, over alpha-equivalent operands, are equal.</summary>
    [TestMethod]
    public void Binary_SameOperandsSameMethod_ReturnsTrue()
    {
        ParameterExpression x1 = P("x");
        ParameterExpression y1 = P("y");
        ParameterExpression x2 = P("x");
        ParameterExpression y2 = P("y");

        BinaryExpression a = Expression.MakeBinary(ExpressionType.Add, x1, y1, liftToNull: false, CustomAddAMethod);
        BinaryExpression b = Expression.MakeBinary(ExpressionType.Add, x2, y2, liftToNull: false, CustomAddAMethod);

        var left = Expression.Lambda<Func<double, double, double>>(a, x1, y1);
        var right = Expression.Lambda<Func<double, double, double>>(b, x2, y2);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 8 - MethodCall receiver comparison
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Regression: the baseline compares <see cref="MethodCallExpression.Object"/> with a bare reference
    /// check (<c>xmco.Object == ymco.Object</c>), so two alpha-equivalent instance calls on different
    /// parameter instances were reported as unequal. The fixed comparer must recurse structurally into
    /// the receiver.
    /// </summary>
    [TestMethod]
    public void MethodCall_AlphaEquivalentReceiver_ReturnsTrue()
    {
        ParameterExpression s = Expression.Parameter(typeof(string), "s");
        ParameterExpression t = Expression.Parameter(typeof(string), "t");
        MethodInfo trim = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!;

        var left = Expression.Lambda<Func<string, string>>(Expression.Call(s, trim), s);
        var right = Expression.Lambda<Func<string, string>>(Expression.Call(t, trim), t);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Preservation: a static call (null receiver on both sides) compares correctly.</summary>
    [TestMethod]
    public void MethodCall_StaticCall_BothNullReceiver_ReturnsTrue()
    {
        ParameterExpression x = P("x");
        ParameterExpression y = P("y");
        MethodInfo sin = typeof(double).GetMethod(nameof(double.Sin), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

        var left = Expression.Lambda<Func<double, double>>(Expression.Call(sin, x), x);
        var right = Expression.Lambda<Func<double, double>>(Expression.Call(sin, y), y);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 9 - MemberExpression parameter context
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Regression: the baseline's <see cref="MemberExpression"/> branch passes the right-hand parameter
    /// array for the left-hand receiver too (<c>Equals(xmo.Expression, yParams, ymo.Expression, yParams)</c>),
    /// so an alpha-equivalent property access on differently-named parameters was reported as unequal.
    /// </summary>
    [TestMethod]
    public void Member_AlphaEquivalentReceiver_ReturnsTrue()
    {
        ParameterExpression x = Expression.Parameter(typeof(SampleContainer), "x");
        ParameterExpression y = Expression.Parameter(typeof(SampleContainer), "y");
        PropertyInfo first = typeof(SampleContainer).GetProperty(nameof(SampleContainer.First))!;

        var left = Expression.Lambda<Func<SampleContainer, int>>(Expression.Property(x, first), x);
        var right = Expression.Lambda<Func<SampleContainer, int>>(Expression.Property(y, first), y);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Required: the same receiver but a different member is not equal.</summary>
    [TestMethod]
    public void Member_SameReceiverDifferentMember_ReturnsFalse()
    {
        ParameterExpression x = Expression.Parameter(typeof(SampleContainer), "x");
        PropertyInfo first = typeof(SampleContainer).GetProperty(nameof(SampleContainer.First))!;
        PropertyInfo second = typeof(SampleContainer).GetProperty(nameof(SampleContainer.Second))!;

        var left = Expression.Lambda<Func<SampleContainer, int>>(Expression.Property(x, first), x);
        var right = Expression.Lambda<Func<SampleContainer, int>>(Expression.Property(x, second), x);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Required: the same member but an inequivalent (unrelated, unbound) receiver is not equal.</summary>
    [TestMethod]
    public void Member_SameMemberInequivalentReceiver_ReturnsFalse()
    {
        ParameterExpression x = Expression.Parameter(typeof(SampleContainer), "x");
        ParameterExpression z = Expression.Parameter(typeof(SampleContainer), "x");
        PropertyInfo first = typeof(SampleContainer).GetProperty(nameof(SampleContainer.First))!;

        MemberExpression left = Expression.Property(x, first);
        MemberExpression right = Expression.Property(z, first);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>
    /// Regression: a recursive <c>ReferenceEquals(x, y)</c> shortcut for structurally-supported nodes is
    /// unsafe once shared sub-expression objects can be reached through two different parameter bindings.
    /// Here the exact same <see cref="MemberExpression"/> object (<c>body = p.First</c>) is used as the
    /// body of two lambdas that declare <c>p</c> at different positions: <c>left = (p, q) =&gt; body</c>
    /// reads its first argument, <c>right = (q, p) =&gt; body</c> reads its second argument (because
    /// <c>body</c> only ever refers to <c>p</c>, and <c>p</c> is declared second in <c>right</c>). These
    /// are different functions and must compare <see langword="false"/>, but a shortcut that treats the
    /// shared <c>body</c> reference as trivially equal to itself would never re-check <c>p</c>'s binding
    /// and incorrectly report <see langword="true"/>. <see cref="MemberExpression"/> is a realistic vector
    /// for this because <c>ExpressionTransformer.CopyExpression</c> returns <see cref="ExpressionType.MemberAccess"/>
    /// nodes unchanged, so the very same object can easily survive independent simplification on both sides.
    /// </summary>
    [TestMethod]
    public void SharedMemberExpressionSubtree_UnderSwappedParameterBindings_ReturnsFalse()
    {
        ParameterExpression p = Expression.Parameter(typeof(SampleContainer), "p");
        ParameterExpression q = Expression.Parameter(typeof(SampleContainer), "q");
        PropertyInfo first = typeof(SampleContainer).GetProperty(nameof(SampleContainer.First))!;
        MemberExpression body = Expression.Property(p, first);

        var left = Expression.Lambda<Func<SampleContainer, SampleContainer, int>>(body, p, q);
        var right = Expression.Lambda<Func<SampleContainer, SampleContainer, int>>(body, q, p);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 6 - nonnumeric constants: null-safe and type-safe
    // ------------------------------------------------------------------------------------------

    /// <summary>Regression: the baseline calls <c>xco.Value.Equals(yco.Value)</c> unconditionally and throws <see cref="NullReferenceException"/> when either value is null.</summary>
    [TestMethod]
    public void Constant_NullStringVsNullString_ReturnsTrue()
    {
        ConstantExpression a = Expression.Constant(null, typeof(string));
        ConstantExpression b = Expression.Constant(null, typeof(string));

        Assert.IsTrue(ExpressionComparer.Default.Equals(a, b));
    }

    /// <summary>Required: a null string constant is not equal to a non-null string constant.</summary>
    [TestMethod]
    public void Constant_NullStringVsNonNullString_ReturnsFalse()
    {
        ConstantExpression a = Expression.Constant(null, typeof(string));
        ConstantExpression b = Expression.Constant("value", typeof(string));

        Assert.IsFalse(ExpressionComparer.Default.Equals(a, b));
    }

    /// <summary>Required: a null string constant is not equal to a null object constant - the declared <see cref="ConstantExpression.Type"/> must match.</summary>
    [TestMethod]
    public void Constant_NullStringVsNullObject_ReturnsFalse()
    {
        ConstantExpression a = Expression.Constant(null, typeof(string));
        ConstantExpression b = Expression.Constant(null, typeof(object));

        Assert.IsFalse(ExpressionComparer.Default.Equals(a, b));
    }

    /// <summary>Preservation: same nonnumeric type and equal value compares equal.</summary>
    [TestMethod]
    public void Constant_SameNonNumericTypeEqualValue_ReturnsTrue()
    {
        ConstantExpression a = Expression.Constant("value", typeof(string));
        ConstantExpression b = Expression.Constant("value", typeof(string));

        Assert.IsTrue(ExpressionComparer.Default.Equals(a, b));
    }

    /// <summary>Required: different nonnumeric type with an equal boxed value must not compare equal.</summary>
    [TestMethod]
    public void Constant_DifferentNonNumericTypeEqualBoxedValue_ReturnsFalse()
    {
        ConstantExpression a = Expression.Constant("value", typeof(string));
        ConstantExpression b = Expression.Constant((object)"value", typeof(object));

        Assert.IsFalse(ExpressionComparer.Default.Equals(a, b));
    }

    // ------------------------------------------------------------------------------------------
    // Finding 7 - exact numeric constant equivalence
    // ------------------------------------------------------------------------------------------

    /// <summary>Required: exact cross-type integer equality.</summary>
    [TestMethod]
    public void Constant_IntEqualsLong_ReturnsTrue()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(1), Expression.Constant(1L)));
    }

    /// <summary>Required: exact cross-type int/double equality.</summary>
    [TestMethod]
    public void Constant_IntEqualsDouble_ReturnsTrue()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(1), Expression.Constant(1.0)));
    }

    /// <summary>Required: 0.5 is exactly representable in binary and decimal, so float/double/decimal must all agree.</summary>
    [TestMethod]
    public void Constant_HalfIsExactAcrossFloatDoubleDecimal_ReturnsTrue()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(0.5f), Expression.Constant(0.5d)));
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(0.5d), Expression.Constant(0.5m)));
    }

    /// <summary>
    /// Regression: 0.1m (exactly one tenth) and 0.1d (the nearest binary double approximation of one tenth)
    /// are NOT the same mathematical value; the baseline's decimal-mediated conversion could paper over
    /// this, which is exactly the kind of false positive #590 must eliminate.
    /// </summary>
    [TestMethod]
    public void Constant_DecimalOneTenthVsDoubleOneTenth_ReturnsFalse()
    {
        Assert.IsFalse(ExpressionComparer.Default.Equals(Expression.Constant(0.1m), Expression.Constant(0.1d)));
    }

    /// <summary>Required: positive and negative zero compare equal.</summary>
    [TestMethod]
    public void Constant_PositiveZeroEqualsNegativeZero_ReturnsTrue()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(0.0), Expression.Constant(-0.0)));
    }

    /// <summary>Required: NaN compares equal to NaN under this comparer's exact-value contract (mirrors <see cref="double.Equals(double)"/>, unlike IEEE <c>==</c>).</summary>
    [TestMethod]
    public void Constant_NaNEqualsNaN_ReturnsTrue()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(double.NaN), Expression.Constant(double.NaN)));
    }

    /// <summary>Required: same-sign infinities compare equal; opposite-sign infinities do not.</summary>
    [TestMethod]
    public void Constant_Infinities_MatchSignExactly()
    {
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(double.PositiveInfinity), Expression.Constant(double.PositiveInfinity)));
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Constant(double.NegativeInfinity), Expression.Constant(double.NegativeInfinity)));
        Assert.IsFalse(ExpressionComparer.Default.Equals(Expression.Constant(double.PositiveInfinity), Expression.Constant(double.NegativeInfinity)));
    }

    /// <summary>
    /// Regression: the baseline's pairwise <c>Convert.ChangeType</c> algorithm throws <see cref="OverflowException"/>
    /// when asked to compare <c>-1L</c> against <see cref="ulong.MaxValue"/> because it attempts to convert
    /// -1 into <see cref="ulong"/>. The fixed comparer must report a clean "not equal" instead.
    /// </summary>
    [TestMethod]
    public void Constant_LongMinusOneVsUlongMaxValue_ReturnsFalseWithoutThrowing()
    {
        Assert.IsFalse(ExpressionComparer.Default.Equals(Expression.Constant(-1L), Expression.Constant(ulong.MaxValue)));
    }

    /// <summary>
    /// Required equivalence-relation test: for a curated set of numeric constants spanning every native
    /// numeric type in <c>Types.Number</c>, plus zero/NaN/infinities/large exact values, exact numeric
    /// equality must be reflexive, symmetric and transitive, and no valid pair may throw.
    /// </summary>
    [TestMethod]
    public void Constant_NumericEquivalence_IsReflexiveSymmetricAndTransitive()
    {
        object[] values =
        [
            (byte)1, (ushort)1, (uint)1, (ulong)1, (sbyte)1, (short)1, 1, 1L, 1m, 1f, 1d,
            (byte)0, 0, 0L, 0m, 0f, 0d, -0.0, -0.0f, -0.0m,
            (sbyte)-1, -1, -1L, -1m, -1f, -1d,
            byte.MaxValue, ushort.MaxValue, uint.MaxValue, ulong.MaxValue,
            sbyte.MinValue, short.MinValue, int.MinValue, long.MinValue,
            0.5f, 0.5d, 0.5m,
            0.1f, 0.1d, 0.1m,
            double.NaN, float.NaN,
            double.PositiveInfinity, float.PositiveInfinity,
            double.NegativeInfinity, float.NegativeInfinity,
            1000000000000L, 1000000000000m, 1000000000000d,
        ];

        ConstantExpression[] constants = System.Array.ConvertAll(values, v => Expression.Constant(v, v.GetType()));
        ExpressionComparer comparer = ExpressionComparer.Default;

        for (int i = 0; i < constants.Length; i++)
        {
            Assert.IsTrue(comparer.Equals(constants[i], constants[i]), $"reflexivity failed at index {i}");
        }

        var equal = new bool[constants.Length, constants.Length];
        for (int i = 0; i < constants.Length; i++)
        {
            for (int j = 0; j < constants.Length; j++)
            {
                equal[i, j] = comparer.Equals(constants[i], constants[j]);
            }
        }

        for (int i = 0; i < constants.Length; i++)
        {
            for (int j = 0; j < constants.Length; j++)
            {
                Assert.AreEqual(equal[i, j], equal[j, i], $"symmetry failed at ({i},{j})");
            }
        }

        for (int i = 0; i < constants.Length; i++)
        {
            for (int j = 0; j < constants.Length; j++)
            {
                if (!equal[i, j]) continue;
                for (int k = 0; k < constants.Length; k++)
                {
                    if (equal[j, k])
                    {
                        Assert.IsTrue(equal[i, k], $"transitivity failed at ({i},{j},{k})");
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // GetHashCode contract
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Regression: the baseline's <c>GetHashCode</c> is <c>Simplify(obj).ToString().GetHashCode()</c>, and
    /// <see cref="Expression.ToString()"/> prints parameter names, so two alpha-equivalent lambdas using
    /// different parameter names produced different strings and therefore different hash codes despite
    /// comparing equal - a violation of the <see cref="IEqualityComparer{T}"/> contract.
    /// </summary>
    [TestMethod]
    public void GetHashCode_AlphaEquivalentLambdas_ProducesSameHash()
    {
        Expression<Func<double, double>> left = x => x + 1;
        Expression<Func<double, double>> right = y => y + 1;

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(left), ExpressionComparer.Default.GetHashCode(right));
    }

    /// <summary>Required: nested alpha-equivalent captures produce the same hash.</summary>
    [TestMethod]
    public void GetHashCode_NestedAlphaEquivalentCaptures_ProducesSameHash()
    {
        Expression<Func<double, Func<double, double>>> left = x => y => x + y;
        Expression<Func<double, Func<double, double>>> right = a => b => a + b;

        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(left), ExpressionComparer.Default.GetHashCode(right));
    }

    /// <summary>Required: exact cross-type numeric constants that compare equal must also hash equal.</summary>
    [TestMethod]
    public void GetHashCode_ExactCrossTypeNumericConstants_ProducesSameHash()
    {
        Assert.AreEqual(
            ExpressionComparer.Default.GetHashCode(Expression.Constant(1)),
            ExpressionComparer.Default.GetHashCode(Expression.Constant(1.0)));
        Assert.AreEqual(
            ExpressionComparer.Default.GetHashCode(Expression.Constant(0.5f)),
            ExpressionComparer.Default.GetHashCode(Expression.Constant(0.5m)));
    }

    /// <summary>Required: a <see cref="HashSet{T}"/> keyed by <see cref="ExpressionComparer.Default"/> finds an alpha-equivalent lambda that was never explicitly added.</summary>
    [TestMethod]
    public void HashSet_AlphaEquivalentLambda_IsFound()
    {
        var set = new HashSet<Expression>(ExpressionComparer.Default);
        Expression<Func<double, double>> left = x => x + 1;
        Expression<Func<double, double>> right = y => y + 1;

        set.Add(left);

        Assert.IsTrue(set.Contains(right));
    }

    /// <summary>Required: a <see cref="HashSet{T}"/> keyed by <see cref="ExpressionComparer.Default"/> finds an exact cross-type numeric constant that was never explicitly added.</summary>
    [TestMethod]
    public void HashSet_ExactCrossTypeNumericConstant_IsFound()
    {
        var set = new HashSet<Expression>(ExpressionComparer.Default);
        set.Add(Expression.Constant(1));

        Assert.IsTrue(set.Contains(Expression.Constant(1.0)));
    }

    // ------------------------------------------------------------------------------------------
    // Mixed equality-relation test
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Required: a small curated set of mixed expression shapes (parameters, constants, alpha lambdas,
    /// nested lambdas, unary/binary nodes, method calls, member accesses, numeric constants) must satisfy
    /// reflexivity, symmetry, transitivity, and "Equals implies equal hash".
    /// </summary>
    [TestMethod]
    public void MixedExpressions_SatisfyEqualityRelationAndHashContract()
    {
        ParameterExpression x = P("x");
        ParameterExpression y = P("y");
        MethodInfo sin = typeof(double).GetMethod(nameof(double.Sin), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

        Expression[] group1 = [Expression.Lambda<Func<double, double>>(Expression.Add(x, Expression.Constant(1.0)), x)];
        Expression[] group2 = [Expression.Lambda<Func<double, double>>(Expression.Add(y, Expression.Constant(1.0)), y)];
        Expression[] group3 = [Expression.Constant(1), Expression.Constant(1L), Expression.Constant(1.0)];
        Expression[] group4 = [Expression.Lambda<Func<double, double>>(Expression.Call(sin, x), x)];
        Expression[] group5 = [Expression.Constant("text")];
        Expression[] group6 = [Expression.Convert(x, typeof(long))];

        ExpressionComparer comparer = ExpressionComparer.Default;

        // group1 and group2 are alpha-equivalent to each other; merge for the relation check.
        var merged = new List<(Expression Expr, int Group)>();
        int groupId = 0;
        foreach (var g in new[] { group1.Concat(group2).ToArray(), group3, group4, group5, group6 })
        {
            foreach (var e in g) merged.Add((e, groupId));
            groupId++;
        }

        for (int i = 0; i < merged.Count; i++)
        {
            Assert.IsTrue(comparer.Equals(merged[i].Expr, merged[i].Expr), $"reflexivity failed at {i}");
        }

        for (int i = 0; i < merged.Count; i++)
        {
            for (int j = 0; j < merged.Count; j++)
            {
                bool eq = comparer.Equals(merged[i].Expr, merged[j].Expr);
                bool eqSwapped = comparer.Equals(merged[j].Expr, merged[i].Expr);
                Assert.AreEqual(eq, eqSwapped, $"symmetry failed at ({i},{j})");

                bool expected = merged[i].Group == merged[j].Group;
                Assert.AreEqual(expected, eq, $"unexpected equality at ({i},{j})");

                if (eq)
                {
                    Assert.AreEqual(comparer.GetHashCode(merged[i].Expr), comparer.GetHashCode(merged[j].Expr), $"hash mismatch at ({i},{j})");
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Thread safety
    // ------------------------------------------------------------------------------------------

    /// <summary>Required: independent, concurrent comparisons of unrelated alpha-equivalent lambdas do not interfere with each other (no shared mutable state on <see cref="ExpressionComparer.Default"/>).</summary>
    [TestMethod]
    public void ConcurrentComparisons_AreIndependent()
    {
        const int taskCount = 16;
        const int iterationsPerTask = 200;

        var tasks = new Task<bool>[taskCount];
        for (int t = 0; t < taskCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerTask; i++)
                {
                    ParameterExpression x = P("x");
                    ParameterExpression y = P("y");
                    var left = Expression.Lambda<Func<double, double>>(Expression.Add(x, Expression.Constant(1.0)), x);
                    var right = Expression.Lambda<Func<double, double>>(Expression.Add(y, Expression.Constant(1.0)), y);

                    if (!ExpressionComparer.Default.Equals(left, right)) return false;
                    if (ExpressionComparer.Default.GetHashCode(left) != ExpressionComparer.Default.GetHashCode(right)) return false;
                }
                return true;
            });
        }

        Task.WaitAll(tasks);
        Assert.IsTrue(System.Array.TrueForAll(tasks, task => task.Result));
    }

    // ------------------------------------------------------------------------------------------
    // S4 compatibility - free-parameter commutative regression
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Regression: before roadmap stage S4, <see cref="ExpressionSimplifier"/>'s additive canonicalization
    /// ordered free (not lambda-bound) parameters by <see cref="ParameterExpression.Name"/> text, so
    /// <c>Simplify(Add(a, b))</c> and <c>Simplify(Add(b, a))</c> always converged to the same operand order
    /// and this comparer's positional operand check trivially agreed. S4 correctly stops inventing a
    /// name-based order for two distinct free parameters (see
    /// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>'s "Free parameters" policy), so each
    /// side can now simplify to its own source operand order instead. Without a comparer-side fix this
    /// would silently flip this comparer's own observable behavior for such expressions from <see langword="true"/>
    /// to <see langword="false"/> — a regression the S4 roadmap entry explicitly requires to be corrected
    /// narrowly in the comparer (via a commutative-operand fallback for ordinary <c>Add</c>/<c>Multiply</c>,
    /// see <see cref="ExpressionComparer"/>'s <c>IsCommutative</c>), not by reintroducing parameter names as
    /// identity. These expressions are deliberately NOT wrapped in an enclosing lambda: <paramref name="a"/>
    /// and <paramref name="b"/> are free parameters from this comparer's point of view.
    /// </summary>
    [TestMethod]
    public void FreeParameters_CommutativeAddition_NotWrappedInLambda_StillEqual()
    {
        ParameterExpression a = P("a");
        ParameterExpression b = P("b");

        Expression left = Expression.Add(a, b);
        Expression right = Expression.Add(b, a);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(left), ExpressionComparer.Default.GetHashCode(right));
    }

    /// <summary>Multiplicative counterpart of <see cref="FreeParameters_CommutativeAddition_NotWrappedInLambda_StillEqual"/>.</summary>
    [TestMethod]
    public void FreeParameters_CommutativeMultiplication_NotWrappedInLambda_StillEqual()
    {
        ParameterExpression a = P("a");
        ParameterExpression b = P("b");

        Expression left = Expression.Multiply(a, b);
        Expression right = Expression.Multiply(b, a);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(left), ExpressionComparer.Default.GetHashCode(right));
    }

    /// <summary>Negative control: two free parameters compared under a NON-commutative operator (<c>Subtract</c>) must not be equated merely because they would be under addition.</summary>
    [TestMethod]
    public void FreeParameters_NonCommutativeSubtraction_NotWrappedInLambda_RemainsDistinct()
    {
        ParameterExpression a = P("a");
        ParameterExpression b = P("b");

        Expression left = Expression.Subtract(a, b);
        Expression right = Expression.Subtract(b, a);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Negative control: the commutative fallback must not apply to a custom-method <c>Add</c> (S3 operator safety) even when swapping operands would otherwise match.</summary>
    [TestMethod]
    public void FreeParameters_CustomOperatorAddition_NotWrappedInLambda_RemainsDistinct()
    {
        ParameterExpression a = P("a");
        ParameterExpression b = P("b");

        Expression left = Expression.MakeBinary(ExpressionType.Add, a, b, false, CustomAddAMethod);
        Expression right = Expression.MakeBinary(ExpressionType.Add, b, a, false, CustomAddAMethod);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>
    /// Characterizes a deliberate, narrowly-scoped WIDENING of this comparer's public equivalence relation
    /// introduced by the commutative fallback fix above, distinct from the "same free parameter instance
    /// used twice" case those other tests cover: two DIFFERENT free <see cref="ParameterExpression"/>
    /// instances that happen to share the SAME <see cref="ParameterExpression.Name"/> ("v" for both).
    /// </summary>
    /// <remarks>
    /// Before the commutative fallback fix, <c>Equals(Add(p, q), Add(q, p))</c> for two such parameters
    /// returned <see langword="false"/>: the pre-S4 textual canonical key tied on the identical name text
    /// "v" for both operands, so the simplifier's stable sort preserved each side's own source operand
    /// order unchanged, and this comparer's then-purely-positional <c>BinaryEqual</c> then compared <c>p</c>
    /// against <c>q</c> positionally and found them reference-unequal. The commutative fallback now matches
    /// them via the swapped comparison (<c>p</c> against <c>p</c>, <c>q</c> against <c>q</c>), so this now
    /// returns <see langword="true"/>. This is intentional and mathematically justified (ordinary addition
    /// really is commutative), not an oversight: this test exists to make the widening an explicit,
    /// observable contract rather than an undocumented side effect of the free-parameter regression fix.
    /// </remarks>
    [TestMethod]
    public void FreeParameters_SameNameDistinctInstances_CommutativeAddition_NowEqual()
    {
        ParameterExpression p = P("v");
        ParameterExpression q = P("v");
        Assert.AreNotSame(p, q);

        Expression left = Expression.Add(p, q);
        Expression right = Expression.Add(q, p);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(left), ExpressionComparer.Default.GetHashCode(right));
    }

    /// <summary>
    /// Characterizes a DELIBERATE, DOCUMENTED scope boundary of the commutative-operand fallback above (S4
    /// review, round 5): it is a narrowly-scoped fix for the DIRECT two-operand case only, not a general
    /// restoration of every pre-S4 convergence behavior. Before S4, three (or more) free parameters chained
    /// through ordinary addition converged to a single canonical order via the old <see cref="ParameterExpression.Name"/>-based
    /// textual sort, regardless of source association/order - e.g. <c>Simplify((a+b)+c)</c> and
    /// <c>Simplify((c+b)+a)</c> both produced the same tree. S4 deliberately does not invent a cross-tree
    /// order for free parameters (see <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>'s "Free
    /// parameters" policy), so each side now keeps its own source order instead:
    /// <c>Simplify((a+b)+c)</c> stays (some association of) <c>a+(b+c)</c>,
    /// <c>Simplify((c+b)+a)</c> stays (some association of) <c>c+(b+a)</c>. The round-2 commutative fallback
    /// only swaps a SINGLE BinaryExpression node's two operands; it is not a general N-ary
    /// associative-commutative multiset match, so it does not bridge this gap. Recognizing arbitrary-length
    /// free-parameter chains as equal would require comparing/hashing ordinary <c>Add</c>/<c>Multiply</c>
    /// chains as associative-commutative collections of terms - a materially larger capability change than
    /// this narrow regression fix, deliberately deferred rather than attempted here (see the roadmap's round
    /// 5 decision). This test exists to make that scope boundary an explicit, observable characterization
    /// rather than an unspecified gap: it asserts the CURRENT, decided behavior, not a requirement.
    /// </summary>
    [TestMethod]
    public void FreeParameters_ThreeTermAdditionPermutation_CharacterizesPostS4ScopeBoundary()
    {
        ParameterExpression a = P("a");
        ParameterExpression b = P("b");
        ParameterExpression c = P("c");

        Expression left = Expression.Add(Expression.Add(a, b), c);
        Expression right = Expression.Add(Expression.Add(c, b), a);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }

    /// <summary>Multiplicative counterpart of <see cref="FreeParameters_ThreeTermAdditionPermutation_CharacterizesPostS4ScopeBoundary"/>.</summary>
    [TestMethod]
    public void FreeParameters_ThreeTermMultiplicationPermutation_CharacterizesPostS4ScopeBoundary()
    {
        ParameterExpression a = P("a");
        ParameterExpression b = P("b");
        ParameterExpression c = P("c");

        Expression left = Expression.Multiply(Expression.Multiply(a, b), c);
        Expression right = Expression.Multiply(Expression.Multiply(c, b), a);

        Assert.IsFalse(ExpressionComparer.Default.Equals(left, right));
    }
}
