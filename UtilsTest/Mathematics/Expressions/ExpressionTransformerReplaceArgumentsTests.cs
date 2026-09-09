using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization and regression coverage for <see cref="ExpressionTransformer.ReplaceArguments"/>
/// and its collection-based core, plus end-to-end coverage of
/// <c>ExpressionSimplifier.InvokeExpression</c> (the lambda-inlining rule that is
/// <see cref="ExpressionTransformer.ReplaceArguments"/>'s main production caller). These tests were
/// written against the pre-optimization baseline (a direct <c>Select(...).ToArray()</c>-based
/// implementation) and must keep passing, unmodified, after the optimization: the observable
/// behavior of the protected member — including its deliberately limited node-type traversal,
/// first-duplicate-wins parameter mapping, and exact exception types/timing for malformed inputs —
/// is a compatibility contract, not something this performance work is allowed to change.
/// </summary>
[TestClass]
public class ExpressionTransformerReplaceArgumentsTests
{
    /// <summary>Exposes the protected <see cref="ExpressionTransformer.ReplaceArguments"/> member for direct testing.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.ReplaceArguments"/> method.</summary>
        public Expression ExposeReplaceArguments(Expression e, ParameterExpression[] oldParameters, Expression[] newParameters)
            => ReplaceArguments(e, oldParameters, newParameters);
    }

    /// <summary>An instance method used to verify a method call's receiver is replaced, not discarded.</summary>
    private sealed class Box
    {
        public double Factor { get; }
        public Box(double factor) => Factor = factor;
        public double Scale(double x) => x * Factor;
    }

    // ------------------------------------------------------------------------------------------
    // A/B: basic parameter mapping.
    // ------------------------------------------------------------------------------------------

    /// <summary>A parameter present in <c>oldParameters</c> is replaced by the corresponding <c>newParameters</c> entry.</summary>
    [TestMethod]
    public void ReplaceArguments_ParameterPresent_ReturnsCorrespondingReplacement()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        ConstantExpression replacement = Expression.Constant(1.0);

        Expression result = transformer.ExposeReplaceArguments(p, [p], [replacement]);

        Assert.AreSame(replacement, result);
    }

    /// <summary>A parameter absent from <c>oldParameters</c> is returned unchanged (same instance).</summary>
    [TestMethod]
    public void ReplaceArguments_ParameterAbsent_ReturnsOriginalInstance()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p1 = Expression.Parameter(typeof(double), "p1");
        ParameterExpression p2 = Expression.Parameter(typeof(double), "p2");

        Expression result = transformer.ExposeReplaceArguments(p2, [p1], [Expression.Constant(1.0)]);

        Assert.AreSame(p2, result);
    }

    // ------------------------------------------------------------------------------------------
    // C: duplicate entries in oldParameters — first match wins (Array.IndexOf semantics).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// When <c>oldParameters</c> contains the same parameter twice, the replacement at the FIRST
    /// occurrence's index wins, mirroring <see cref="Array.IndexOf{T}(T[], T)"/>. A future
    /// dictionary-based rewrite must not silently flip this to last-wins.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_DuplicateOldParameter_FirstOccurrenceWins()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        ConstantExpression replacement1 = Expression.Constant(1.0);
        ConstantExpression replacement2 = Expression.Constant(2.0);

        Expression result = transformer.ExposeReplaceArguments(p, [p, p], [replacement1, replacement2]);

        Assert.AreSame(replacement1, result);
    }

    // ------------------------------------------------------------------------------------------
    // D: newParameters shorter than the matched index.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A match found at an index beyond the end of a too-short <c>newParameters</c> array throws
    /// the native array bounds exception (<see cref="IndexOutOfRangeException"/>, not
    /// <see cref="ArgumentOutOfRangeException"/>) — this is a raw array element access, not a
    /// collection method call.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_NewParametersTooShort_ThrowsIndexOutOfRangeException()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p0 = Expression.Parameter(typeof(double), "p0");
        ParameterExpression p1 = Expression.Parameter(typeof(double), "p1");

        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => transformer.ExposeReplaceArguments(p1, [p0, p1], [Expression.Constant(1.0)]));
    }

    // ------------------------------------------------------------------------------------------
    // E: newParameters == null.
    // ------------------------------------------------------------------------------------------

    /// <summary>When no match is found, a null <c>newParameters</c> is never dereferenced.</summary>
    [TestMethod]
    public void ReplaceArguments_NoMatch_NullNewParameters_ReturnsOriginalInstance()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");

        Expression result = transformer.ExposeReplaceArguments(p, [], null!);

        Assert.AreSame(p, result);
    }

    /// <summary>A match found against a null <c>newParameters</c> throws on the null array element access.</summary>
    [TestMethod]
    public void ReplaceArguments_Match_NullNewParameters_ThrowsNullReferenceException()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");

        Assert.ThrowsExactly<NullReferenceException>(
            () => transformer.ExposeReplaceArguments(p, [p], null!));
    }

    // ------------------------------------------------------------------------------------------
    // F: oldParameters == null.
    // ------------------------------------------------------------------------------------------

    /// <summary>A node type that never reaches the ParameterExpression branch never dereferences a null <c>oldParameters</c>.</summary>
    [TestMethod]
    public void ReplaceArguments_UnreachedNode_NullOldParameters_ReturnsOriginalInstance()
    {
        var transformer = new ExposedTransformer();
        ConstantExpression constant = Expression.Constant(1);

        Expression result = transformer.ExposeReplaceArguments(constant, null!, null!);

        Assert.AreSame(constant, result);
    }

    /// <summary>A <see cref="ParameterExpression"/> against a null <c>oldParameters</c> throws the native array-null exception.</summary>
    [TestMethod]
    public void ReplaceArguments_Parameter_NullOldParameters_ThrowsArgumentNullException()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");

        var ex = Assert.ThrowsExactly<ArgumentNullException>(
            () => transformer.ExposeReplaceArguments(p, null!, [Expression.Constant(1.0)]));
        Assert.AreEqual("array", ex.ParamName);
    }

    // ------------------------------------------------------------------------------------------
    // G: unsupported node type containing a replaceable parameter — must not be traversed.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// <see cref="MemberExpression"/> has no dedicated branch in <c>ReplaceArguments</c>, so it (and
    /// its child referencing the parameter) must be returned completely untouched. Migrating to an
    /// <see cref="ExpressionVisitor"/> would silently start rewriting this — that is explicitly out
    /// of scope for this optimization.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_UnsupportedMemberExpression_IsNotTraversed()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(string), "p");
        MemberExpression member = Expression.Property(p, nameof(string.Length));

        Expression result = transformer.ExposeReplaceArguments(member, [p], [Expression.Constant("hello")]);

        Assert.AreSame(member, result);
        Assert.AreSame(p, ((MemberExpression)result).Expression);
    }

    // ------------------------------------------------------------------------------------------
    // H: nested LambdaExpression — not traversed either.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// <see cref="LambdaExpression"/> has no dedicated branch, so a lambda whose body references the
    /// outer parameter as a free variable is returned completely untouched — no lexical-scope-aware
    /// substitution is attempted by this member.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_NestedLambda_IsNotTraversed()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        LambdaExpression lambda = Expression.Lambda(Expression.Add(p, q), q);

        Expression result = transformer.ExposeReplaceArguments(lambda, [p], [Expression.Constant(5.0)]);

        Assert.AreSame(lambda, result);
    }

    // ------------------------------------------------------------------------------------------
    // I: supported node type with no actual replacement — still reconstructed (new instance).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A <see cref="BinaryExpression"/> containing no replaceable parameter is still rebuilt via
    /// <see cref="ExpressionTransformer.CopyExpression"/> (no "children unchanged" short-circuit), so
    /// the result is a different instance with equivalent structure. A future
    /// <c>Update(...)</c>-based rewrite would break this identity — that is explicitly out of scope.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_SupportedNodeWithoutReplacement_StillReturnsNewInstance()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression unrelated = Expression.Parameter(typeof(double), "unrelated");
        Expression binary = Expression.Add(Expression.Constant(1.0), Expression.Constant(2.0));

        Expression result = transformer.ExposeReplaceArguments(binary, [unrelated], [Expression.Constant(0.0)]);

        Assert.AreNotSame(binary, result);
        Assert.AreEqual(ExpressionType.Add, result.NodeType);
        double value = Expression.Lambda<Func<double>>(result).Compile()();
        Assert.AreEqual(3.0, value, 1e-9);
    }

    // ------------------------------------------------------------------------------------------
    // J: instance MethodCall — receiver replaced recursively and kept attached.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A method call's instance receiver is itself replaced recursively (here, a
    /// <see cref="ConditionalExpression"/> receiver referencing the substituted parameter) and stays
    /// attached to the rebuilt <see cref="MethodCallExpression"/> — never silently reconstructed as
    /// a static call.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_InstanceMethodCall_ReplacesReceiverRecursivelyAndKeepsItAttached()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        var boxA = new Box(10.0);
        var boxB = new Box(20.0);

        // Receiver is a Conditional referencing p, so substituting p changes which Box the call targets.
        Expression receiver = Expression.Condition(
            Expression.GreaterThan(p, Expression.Constant(0.0)),
            Expression.Constant(boxA),
            Expression.Constant(boxB));
        var scale = typeof(Box).GetMethod(nameof(Box.Scale))!;
        MethodCallExpression call = Expression.Call(receiver, scale, Expression.Constant(10.0));

        // p → 5.0 (> 0), so the receiver must resolve to boxA.
        Expression result = transformer.ExposeReplaceArguments(call, [p], [Expression.Constant(5.0)]);

        double value = Expression.Lambda<Func<double>>(result).Compile()();
        Assert.AreEqual(100.0, value, 1e-9); // boxA.Scale(10) = 10 * 10
    }

    // ------------------------------------------------------------------------------------------
    // ExpressionSimplifier.InvokeExpression: end-to-end lambda inlining via the collection-based core.
    // ------------------------------------------------------------------------------------------

    /// <summary>Invoking a zero-parameter lambda inlines its body correctly.</summary>
    [TestMethod]
    public void InvokeExpression_ZeroParameterLambda_InlinesBody()
    {
        Expression<Func<double>> lambda = () => 42.0;
        Expression invocation = Expression.Invoke(lambda);

        var simplified = (Expression<Func<double>>)((Expression)Expression.Lambda<Func<double>>(invocation)).Simplify();

        Assert.AreEqual(42.0, simplified.Compile()(), 1e-9);
    }

    /// <summary>Invoking a one-parameter lambda substitutes the argument correctly.</summary>
    [TestMethod]
    public void InvokeExpression_OneParameterLambda_SubstitutesArgument()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression<Func<double, double>> inner = y => y * 2.0;
        Expression invocation = Expression.Invoke(inner, x);
        var lambda = Expression.Lambda<Func<double, double>>(invocation, x);

        var simplified = (Expression<Func<double, double>>)((Expression)lambda).Simplify();

        Assert.AreEqual(14.0, simplified.Compile()(7.0), 1e-9);
    }

    /// <summary>
    /// Invoking a four-parameter lambda substitutes every argument in the correct position — this
    /// protects the argument order preserved by the collection-based core's indexed loop.
    /// </summary>
    [TestMethod]
    public void InvokeExpression_FourParameterLambda_SubstitutesArgumentsInOrder()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression c = Expression.Parameter(typeof(double), "c");
        ParameterExpression d = Expression.Parameter(typeof(double), "d");
        // Distinguishable coefficients so a wrong argument order produces a wrong, detectable result.
        Expression body = Expression.Add(
            Expression.Add(Expression.Multiply(a, Expression.Constant(1000.0)), Expression.Multiply(b, Expression.Constant(100.0))),
            Expression.Add(Expression.Multiply(c, Expression.Constant(10.0)), d));
        var innerLambda = Expression.Lambda(body, a, b, c, d);

        Expression invocation = Expression.Invoke(
            innerLambda,
            Expression.Constant(1.0), Expression.Constant(2.0), Expression.Constant(3.0), Expression.Constant(4.0));
        var outerLambda = Expression.Lambda<Func<double>>(invocation);

        var simplified = (Expression<Func<double>>)((Expression)outerLambda).Simplify();

        Assert.AreEqual(1234.0, simplified.Compile()(), 1e-9);
    }

    /// <summary>A lambda body containing a <see cref="MethodCallExpression"/> is correctly inlined.</summary>
    [TestMethod]
    public void InvokeExpression_BodyContainsMethodCall_InlinesCorrectly()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo sqrt = typeof(Math).GetMethod(nameof(Math.Sqrt), [typeof(double)])!;
        Expression<Func<double, double>> inner = Expression.Lambda<Func<double, double>>(Expression.Call(sqrt, x), x);
        Expression invocation = Expression.Invoke(inner, Expression.Constant(16.0));
        var outerLambda = Expression.Lambda<Func<double>>(invocation);

        var simplified = (Expression<Func<double>>)((Expression)outerLambda).Simplify();

        Assert.AreEqual(4.0, simplified.Compile()(), 1e-9);
    }

    /// <summary>A lambda body containing another <see cref="InvocationExpression"/> is correctly inlined, recursively.</summary>
    [TestMethod]
    public void InvokeExpression_BodyContainsInvocation_InlinesRecursively()
    {
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression<Func<double, double>> innermost = Expression.Lambda<Func<double, double>>(Expression.Add(z, Expression.Constant(1.0)), z); // z => z + 1

        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression outerBody = Expression.Invoke(innermost, y); // y => Invoke(innermost, y)
        var outer = Expression.Lambda<Func<double, double>>(outerBody, y);

        Expression invocation = Expression.Invoke(outer, Expression.Constant(3.0)); // Invoke(outer, 3.0)
        var topLambda = Expression.Lambda<Func<double>>(invocation);

        var simplified = (Expression<Func<double>>)((Expression)topLambda).Simplify();

        Assert.AreEqual(4.0, simplified.Compile()(), 1e-9); // 3 + 1
    }
}
