using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for <see cref="ExpressionTransformer"/> defects fixed in the
/// TODO-2026-07-11-pass3 follow-up: loss of <see cref="BinaryExpression.Method"/> (including for
/// <see cref="ExpressionType.Power"/>), <see cref="ConditionalExpression"/> dispatch, and loss of a
/// method-call instance receiver.
/// </summary>
[TestClass]
public class ExpressionTransformerTests
{
    /// <summary>A helper carrying an explicit float-typed Pow method for the Power/Method test.</summary>
    private static float FloatPow(float x, float y) => MathF.Pow(x, y);

    /// <summary>
    /// Builds a <see cref="ExpressionType.Power"/> node with an explicit non-double <c>Pow</c> method and
    /// verifies the transformer preserves that method (rather than losing it and failing to rebuild).
    /// </summary>
    [TestMethod]
    public void Power_WithExplicitFloatMethod_PreservesMethod()
    {
        MethodInfo pow = typeof(ExpressionTransformerTests)
            .GetMethod(nameof(FloatPow), BindingFlags.NonPublic | BindingFlags.Static)!;

        ParameterExpression x = Expression.Parameter(typeof(float), "x");
        BinaryExpression power = Expression.Power(x, Expression.Constant(3f), pow);
        var lambda = Expression.Lambda<Func<float, float>>(power, x);

        var simplified = (Expression<Func<float, float>>)lambda.Simplify();

        // The rebuilt Power node must still carry the explicit method.
        var body = simplified.Body as BinaryExpression;
        Assert.IsNotNull(body, "Simplified body should still be a binary Power expression.");
        Assert.AreEqual(ExpressionType.Power, body.NodeType);
        Assert.AreSame(pow, body.Method, "The explicit Pow method must be preserved.");

        // And it must still compute the right value.
        Assert.AreEqual(8f, simplified.Compile()(2f), 1e-5f);
    }

    /// <summary>
    /// Non-regression: a classic double Power node (no explicit method) still transforms and evaluates.
    /// </summary>
    [TestMethod]
    public void Power_ClassicDouble_StillWorks()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Power(x, Expression.Constant(3.0)), x);

        var simplified = (Expression<Func<double, double>>)lambda.Simplify();

        Assert.AreEqual(8.0, simplified.Compile()(2.0), 1e-9);
    }

    /// <summary>
    /// A ternary <c>x =&gt; x &gt; 0 ? x : -x</c> (absolute value) must simplify without throwing
    /// <see cref="IndexOutOfRangeException"/> and compile to the correct result.
    /// </summary>
    [TestMethod]
    public void Simplify_Conditional_AbsoluteValue()
    {
        Expression<Func<double, double>> f = x => x > 0 ? x : -x;

        var simplified = (Expression<Func<double, double>>)((Expression)f).Simplify();
        var compiled = simplified.Compile();

        Assert.AreEqual(3.0, compiled(3.0), 1e-9);
        Assert.AreEqual(4.0, compiled(-4.0), 1e-9);
        Assert.AreEqual(0.0, compiled(0.0), 1e-9);
    }

    /// <summary>
    /// A ternary used as a method-call argument (<c>Math.Sqrt(x &gt; 0 ? x : -x)</c>) must simplify
    /// correctly — this is the exact shape called out in the pass3 item #43 note.
    /// </summary>
    [TestMethod]
    public void Simplify_Conditional_AsMethodArgument()
    {
        Expression<Func<double, double>> f = x => Math.Sqrt(x > 0 ? x : -x);

        var simplified = (Expression<Func<double, double>>)((Expression)f).Simplify();
        var compiled = simplified.Compile();

        Assert.AreEqual(2.0, compiled(4.0), 1e-9);
        Assert.AreEqual(3.0, compiled(-9.0), 1e-9);
    }

    /// <summary>
    /// A ternary whose branches are actually simplified (<c>x + 0</c> and <c>x * 1</c>) must produce
    /// the reduced branches while remaining semantically correct.
    /// </summary>
    [TestMethod]
    public void Simplify_Conditional_WithTransformableBranches()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression test = Expression.GreaterThan(x, Expression.Constant(0.0));
        Expression ifTrue = Expression.Add(x, Expression.Constant(0.0));   // => x
        Expression ifFalse = Expression.Multiply(x, Expression.Constant(1.0)); // => x
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Condition(test, ifTrue, ifFalse), x);

        var simplified = (Expression<Func<double, double>>)((Expression)lambda).Simplify();
        var compiled = simplified.Compile();

        Assert.AreEqual(5.0, compiled(5.0), 1e-9);
        Assert.AreEqual(-2.0, compiled(-2.0), 1e-9);
    }

    /// <summary>An instance method used to verify the receiver survives transformation.</summary>
    private sealed class Box
    {
        public double Factor { get; }
        public Box(double factor) => Factor = factor;
        public double Scale(double x) => x * Factor;
    }

    /// <summary>Exposes the protected ReplaceArguments method for direct unit testing.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>
        /// Calls the protected <see cref="ExpressionTransformer.ReplaceArguments"/> method, making
        /// it accessible from unit tests without subclassing the full transformer hierarchy.
        /// </summary>
        public Expression ExposeReplaceArguments(
            Expression e,
            ParameterExpression[] oldParameters,
            Expression[] newParameters)
            => ReplaceArguments(e, oldParameters, newParameters);

        /// <summary>
        /// Calls the protected <see cref="ExpressionTransformer.Transform"/> method. This transformer
        /// declares no <see cref="ExpressionSignatureAttribute"/>-annotated rule, so every call falls
        /// through to <see cref="FinalizeExpression"/>.
        /// </summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>
        /// Copies the expression with the supplied sub-expressions so the transformer does not
        /// throw when no signature method matches (required by the abstract base class contract).
        /// </summary>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// An instance method call must keep its receiver object: the previous transformer rebuilt every
    /// call with the static overload and dropped <see cref="MethodCallExpression.Object"/>, throwing.
    /// </summary>
    [TestMethod]
    public void Simplify_InstanceMethodCall_PreservesReceiver()
    {
        var box = new Box(3.0);
        Expression<Func<double, double>> f = x => box.Scale(x + 0.0);

        var simplified = (Expression<Func<double, double>>)((Expression)f).Simplify();
        var compiled = simplified.Compile();

        Assert.AreEqual(6.0, compiled(2.0), 1e-9);
    }

    /// <summary>
    /// A <c>??</c> (Coalesce) node with an explicit conversion lambda must preserve that lambda
    /// after transformation. The type-specific <see cref="System.Linq.Expressions.Expression.Coalesce"/>
    /// factory drops <see cref="System.Linq.Expressions.BinaryExpression.Conversion"/>;
    /// <see cref="System.Linq.Expressions.Expression.MakeBinary"/> preserves it.
    /// </summary>
    [TestMethod]
    public void Simplify_CoalesceWithConversionLambda_PreservesConversion()
    {
        ParameterExpression x = Expression.Parameter(typeof(int?), "x");
        // Conversion: (int n) => n * 10  — takes the UNWRAPPED value (int, not int?)
        // Expression.Coalesce with conversion requires the lambda parameter to be the
        // non-nullable value type of the left operand.
        ParameterExpression convParam = Expression.Parameter(typeof(int), "n");
        LambdaExpression conversion = Expression.Lambda(
            Expression.Multiply(convParam, Expression.Constant(10)),
            convParam);
        // x ?? 0 with conversion: x.HasValue ? x.Value * 10 : 0  (result type: int)
        BinaryExpression coalesce = Expression.Coalesce(x, Expression.Constant(0), conversion);
        var lambda = Expression.Lambda<Func<int?, int>>(coalesce, x);

        // Simplify (round-trips through the transformer; must not lose the Conversion lambda).
        // Without the MakeBinary fix, CopyExpression used Expression.Coalesce(left, right)
        // which drops Conversion, changing the result type and semantics.
        var simplified = (Expression<Func<int?, int>>)((Expression)lambda).Simplify();
        var compiled = simplified.Compile();

        Assert.AreEqual(50, compiled(5));    // x=5 → 5*10 = 50
        Assert.AreEqual(0, compiled(null));   // x=null → 0
    }

    /// <summary>
    /// The receiver of an instance method call must be recursively transformed (not left as the
    /// original sub-tree). This verifies that <see cref="ExpressionTransformer"/> calls
    /// <c>PrepareExpression</c> on <see cref="MethodCallExpression.Object"/> just as it does
    /// on arguments.
    /// </summary>
    [TestMethod]
    public void Simplify_InstanceMethodCallWithConditionalReceiver_ReceiverIsTransformed()
    {
        var box1 = new Box(2.0);
        var box2 = new Box(3.0);
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        // Object = (x + 0.0) > 0.0 ? box1 : box2
        // The simplifier should visit the object and reduce x+0.0 → x inside the condition.
        Expression xPlusZero = Expression.Add(x, Expression.Constant(0.0));
        Expression cond = Expression.GreaterThan(xPlusZero, Expression.Constant(0.0));
        Expression obj = Expression.Condition(cond, Expression.Constant(box1), Expression.Constant(box2));
        System.Reflection.MethodInfo scale = typeof(Box).GetMethod(nameof(Box.Scale))!;
        // Argument also contains a simplifiable x + 0.0 to confirm both paths are exercised.
        Expression arg = Expression.Add(x, Expression.Constant(0.0));
        Expression call = Expression.Call(obj, scale, arg);
        var lambda = Expression.Lambda<Func<double, double>>(call, x);

        var simplified = (Expression<Func<double, double>>)((Expression)lambda).Simplify();
        var compiled = simplified.Compile();

        // x=2 > 0 → box1 (factor 2): box1.Scale(2) = 4
        Assert.AreEqual(4.0, compiled(2.0), 1e-9);
        // x=-1 ≤ 0 → box2 (factor 3): box2.Scale(-1) = -3
        Assert.AreEqual(-3.0, compiled(-1.0), 1e-9);
    }

    /// <summary>
    /// The expression target of an <see cref="InvocationExpression"/> must be recursively prepared,
    /// mirroring the fix applied to <see cref="MethodCallExpression.Object"/>.
    /// </summary>
    [TestMethod]
    public void Simplify_InvocationExpression_TransformsInvocationTarget()
    {
        Func<double, double> mul2 = y => y * 2.0;
        Func<double, double> mul3 = y => y * 3.0;

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        // Target: ((x + 0.0) > 0.0) ? mul2 : mul3 — PrepareExpression must visit this subtree.
        Expression xPlusZero = Expression.Add(x, Expression.Constant(0.0));
        Expression target = Expression.Condition(
            Expression.GreaterThan(xPlusZero, Expression.Constant(0.0)),
            Expression.Constant(mul2),
            Expression.Constant(mul3));
        // Argument also contains a simplifiable x + 0.0 (both paths are exercised).
        Expression arg = Expression.Add(x, Expression.Constant(0.0));
        Expression invocation = Expression.Invoke(target, arg);
        var lambda = Expression.Lambda<Func<double, double>>(invocation, x);

        var simplified = (Expression<Func<double, double>>)((Expression)lambda).Simplify();
        var compiled = simplified.Compile();

        Assert.AreEqual(4.0, compiled(2.0), 1e-9);    // 2 > 0 → mul2(2) = 4
        Assert.AreEqual(-9.0, compiled(-3.0), 1e-9);   // -3 ≤ 0 → mul3(-3) = -9
    }

    /// <summary>
    /// <see cref="ExpressionTransformer.ReplaceArguments"/> must substitute parameters inside
    /// the invocation target expression, not only in the argument list.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_InvocationExpression_ReplacesParametersInTarget()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");

        Func<double, double> mul2 = y => y * 2.0;
        Func<double, double> mul3 = y => y * 3.0;
        // Target conditional references p; argument is also p.
        Expression target = Expression.Condition(
            Expression.GreaterThan(p, Expression.Constant(0.0)),
            Expression.Constant(mul2),
            Expression.Constant(mul3));
        Expression invocation = Expression.Invoke(target, p);

        // Replace p → 4.0
        Expression result = transformer.ExposeReplaceArguments(
            invocation,
            new[] { p },
            new Expression[] { Expression.Constant(4.0) });

        // 4 > 0 → mul2(4) = 8
        double value = Expression.Lambda<Func<double>>(result).Compile()();
        Assert.AreEqual(8.0, value, 1e-9);
    }

    /// <summary>
    /// <see cref="ExpressionTransformer.ReplaceArguments"/> must recurse into Test, IfTrue, and
    /// IfFalse of a <see cref="ConditionalExpression"/>. Without an explicit case it fell through
    /// to <c>return e</c>, leaving parameters unsubstituted in all three branches.
    /// </summary>
    [TestMethod]
    public void ReplaceArguments_ConditionalExpression_ReplacesParametersInAllBranches()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression p = Expression.Parameter(typeof(double), "p");

        // p > 0.0 ? p * 2.0 : p * (-1.0)
        Expression conditional = Expression.Condition(
            Expression.GreaterThan(p, Expression.Constant(0.0)),
            Expression.Multiply(p, Expression.Constant(2.0)),
            Expression.Multiply(p, Expression.Constant(-1.0)));

        // Replace p → 5.0
        Expression result = transformer.ExposeReplaceArguments(
            conditional,
            new[] { p },
            new Expression[] { Expression.Constant(5.0) });

        // 5 > 0 → 5 * 2 = 10
        double value = Expression.Lambda<Func<double>>(result).Compile()();
        Assert.AreEqual(10.0, value, 1e-9);
    }

    // ---------------------------------------------------------------------------------------------
    // Coverage added for the structural refactor of Transform(Expression) into PrepareTransform/
    // TryTransform/TryInvokeTransformMethod. These tests document pre-existing behavior; none of
    // them change what is being asserted about the transformer's observable output.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A lone <see cref="ConstantExpression"/> with no matching rule must reach
    /// <see cref="ExpressionTransformer.FinalizeExpression"/> with an empty sub-expression array.
    /// </summary>
    [TestMethod]
    public void Transform_ConstantExpression_Isolated_ReachesFinalize()
    {
        var transformer = new ExposedTransformer();
        ConstantExpression constant = Expression.Constant(5.0);

        Expression result = transformer.ExposeTransform(constant);

        var resultConstant = result as ConstantExpression;
        Assert.IsNotNull(resultConstant, "Result should still be a ConstantExpression.");
        Assert.AreEqual(5.0, resultConstant.Value);
    }

    /// <summary>
    /// A lone <see cref="ParameterExpression"/> with no matching rule must reach
    /// <see cref="ExpressionTransformer.FinalizeExpression"/> and come back unchanged
    /// (<see cref="ExpressionTransformer.CopyExpression"/> passes Parameter nodes through as-is).
    /// </summary>
    [TestMethod]
    public void Transform_ParameterExpression_Isolated_ReachesFinalize()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression parameter = Expression.Parameter(typeof(double), "x");

        Expression result = transformer.ExposeTransform(parameter);

        Assert.AreSame(parameter, result);
    }

    /// <summary>
    /// A lone <see cref="UnaryExpression"/> (<c>-x</c>) with no matching rule must be rebuilt with the
    /// same <see cref="ExpressionType"/> and operand.
    /// </summary>
    [TestMethod]
    public void Transform_UnaryExpression_Isolated_PreservesNodeTypeAndOperand()
    {
        var transformer = new ExposedTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression negate = Expression.Negate(x);

        Expression result = transformer.ExposeTransform(negate);

        var resultUnary = result as UnaryExpression;
        Assert.IsNotNull(resultUnary, "Result should still be a UnaryExpression.");
        Assert.AreEqual(ExpressionType.Negate, resultUnary.NodeType);
        Assert.AreSame(x, resultUnary.Operand);
    }

    /// <summary>A transformer with a single rule reducing <c>x + 0</c> to <c>x</c>.</summary>
    private sealed class AddZeroTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Reduces <c>left + 0</c> to <c>left</c>; leaves other additions unmatched.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression AddZero(BinaryExpression e, Expression left, ConstantExpression right)
        {
            return Convert.ToDouble(right.Value) == 0.0 ? left : null;
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// The body of a <see cref="LambdaExpression"/> must be recursively transformed via the direct
    /// <c>Transform(le.Body)</c> call inside the Lambda preparation step (not merely copied).
    /// </summary>
    [TestMethod]
    public void Transform_LambdaExpression_Isolated_BodyIsTransformedRecursively()
    {
        var transformer = new AddZeroTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression body = Expression.Add(x, Expression.Constant(0.0)); // reducible to x
        var lambda = Expression.Lambda<Func<double, double>>(body, x);

        Expression result = transformer.ExposeTransform(lambda);

        var resultLambda = result as LambdaExpression;
        Assert.IsNotNull(resultLambda, "Result should still be a LambdaExpression.");
        Assert.AreSame(x, resultLambda.Body, "The body should have been reduced to the bare parameter x.");
    }

    /// <summary>A transformer whose only rule can never match a BinaryExpression node.</summary>
    private sealed class IncompatibleRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="NeverMatches"/> was ever invoked.</summary>
        public bool RuleWasInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>
        /// Declares <see cref="ExpressionType.Add"/> (so <c>attr.Match</c> succeeds) but requires a
        /// <see cref="ConstantExpression"/> as the first parameter, which a <see cref="BinaryExpression"/>
        /// node can never satisfy.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression NeverMatches(ConstantExpression e)
        {
            RuleWasInvoked = true;
            return e;
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule whose <see cref="ExpressionSignatureAttribute"/> matches the node type but whose first
    /// parameter type is incompatible with the actual node must be skipped, falling through to
    /// <see cref="ExpressionTransformer.FinalizeExpression"/> without ever being invoked.
    /// </summary>
    [TestMethod]
    public void Transform_IncompatibleRule_IsSkipped_FallsThroughToFinalize()
    {
        var transformer = new IncompatibleRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        Expression result = transformer.ExposeTransform(add);

        Assert.IsFalse(transformer.RuleWasInvoked, "The incompatible rule must never be invoked.");
        Assert.AreEqual(ExpressionType.Add, result.NodeType);
    }

    /// <summary>
    /// Two rules registered for the same <see cref="ExpressionType"/>: the first returns
    /// <see langword="null"/> (multi-parameter branch), which must let the second, compatible rule
    /// run instead of aborting the dispatch.
    /// </summary>
    private sealed class NullThenMatchTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="FirstRuleReturnsNull"/> was invoked.</summary>
        public bool FirstRuleInvoked { get; private set; }

        /// <summary>Whether <see cref="SecondRuleMatches"/> was invoked.</summary>
        public bool SecondRuleInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Always matches but always defers to the next rule by returning <see langword="null"/>.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression FirstRuleReturnsNull(BinaryExpression e, Expression left, Expression right)
        {
            FirstRuleInvoked = true;
            return null;
        }

        /// <summary>Matches after <see cref="FirstRuleReturnsNull"/> defers, returning a fixed constant.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression SecondRuleMatches(BinaryExpression e, Expression left, Expression right)
        {
            SecondRuleInvoked = true;
            return Expression.Constant(42.0);
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule returning <see langword="null"/> from the multi-parameter invocation branch must not
    /// stop the dispatch: the next matching rule must still run and its result must be returned.
    /// </summary>
    [TestMethod]
    public void Transform_RuleReturningNull_MultiParameterBranch_FallsThroughToNextRule()
    {
        var transformer = new NullThenMatchTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        Expression result = transformer.ExposeTransform(add);

        Assert.IsTrue(transformer.FirstRuleInvoked, "The null-returning rule must have been tried.");
        Assert.IsTrue(transformer.SecondRuleInvoked, "The next matching rule must have run afterwards.");
        var resultConstant = result as ConstantExpression;
        Assert.IsNotNull(resultConstant);
        Assert.AreEqual(42.0, resultConstant.Value);
    }

    /// <summary>A transformer whose rule uses the special <c>Expression[]</c> second-parameter shape.</summary>
    private sealed class ExpressionArrayRuleTransformer : ExpressionTransformer
    {
        /// <summary>The sub-expression array received by <see cref="CaptureArgs"/>, for assertions.</summary>
        public Expression[] CapturedArgs { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>
        /// Matches any <see cref="ExpressionType.Call"/> node and records the full array of prepared
        /// sub-expressions it receives via the special <c>Expression[]</c> second-parameter shape.
        /// </summary>
        [ExpressionSignature(ExpressionType.Call)]
        private Expression CaptureArgs(Expression e, Expression[] args)
        {
            CapturedArgs = args;
            return CopyExpression(e, args);
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule declared with a second parameter of exactly type <c>Expression[]</c> must receive the
    /// full array of prepared sub-expressions, in order, rather than positional typed parameters.
    /// </summary>
    [TestMethod]
    public void Transform_ExpressionArrayShapedRule_ReceivesAllSubExpressions()
    {
        var transformer = new ExpressionArrayRuleTransformer();
        MethodInfo max = typeof(Math).GetMethod(nameof(Math.Max), new[] { typeof(double), typeof(double) })!;
        MethodCallExpression call = Expression.Call(max, Expression.Constant(3.0), Expression.Constant(4.0));

        transformer.ExposeTransform(call);

        Assert.IsNotNull(transformer.CapturedArgs);
        Assert.AreEqual(2, transformer.CapturedArgs.Length);
        Assert.AreEqual(3.0, ((ConstantExpression)transformer.CapturedArgs[0]).Value);
        Assert.AreEqual(4.0, ((ConstantExpression)transformer.CapturedArgs[1]).Value);
    }

    /// <summary>A transformer whose rule declares only a single (node) parameter.</summary>
    private sealed class SingleParameterRuleTransformer : ExpressionTransformer
    {
        /// <summary>The exact node instance passed to <see cref="DoubleConstant"/>, for assertions.</summary>
        public ConstantExpression ReceivedParameter { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>
        /// A rule declaring exactly one parameter (the node itself): doubles a numeric constant.
        /// </summary>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression DoubleConstant(ConstantExpression cc)
        {
            ReceivedParameter = cc;
            return Expression.Constant(Convert.ToDouble(cc.Value) * 2.0);
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule declaring exactly one parameter must be invoked with that single node instance.
    /// </summary>
    [TestMethod]
    public void Transform_SingleParameterRule_MatchesExactExpressionInstance()
    {
        var transformer = new SingleParameterRuleTransformer();
        ConstantExpression constant = Expression.Constant(5.0);

        Expression result = transformer.ExposeTransform(constant);

        Assert.AreSame(constant, transformer.ReceivedParameter);
        var resultConstant = result as ConstantExpression;
        Assert.IsNotNull(resultConstant);
        Assert.AreEqual(10.0, resultConstant.Value);
    }

    /// <summary>A transformer whose rule restricts a parameter to a specific constant value.</summary>
    private sealed class ConstantConstrainedRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="AddOne"/> was invoked.</summary>
        public bool Invoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>
        /// Matches <c>left + 1.0</c> only: the <see cref="ConstantNumericAttribute"/> on <paramref name="right"/>
        /// restricts this rule to that specific constant value.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression AddOne(BinaryExpression e, Expression left, [ConstantNumeric(1.0)] ConstantExpression right)
        {
            Invoked = true;
            return left;
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule parameter carrying its own <see cref="ExpressionSignatureAttribute"/>-derived attribute
    /// (here <see cref="ConstantNumericAttribute"/>) must filter out otherwise type-compatible
    /// sub-expressions that don't satisfy that attribute, while accepting ones that do.
    /// </summary>
    [TestMethod]
    public void Transform_ParameterWithExpressionSignatureConstraint_FiltersCorrectly()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        var matchingTransformer = new ConstantConstrainedRuleTransformer();
        Expression matchingResult = matchingTransformer.ExposeTransform(Expression.Add(x, Expression.Constant(1.0)));
        Assert.IsTrue(matchingTransformer.Invoked, "The rule must run when the constant satisfies ConstantNumeric(1.0).");
        Assert.AreSame(x, matchingResult);

        var nonMatchingTransformer = new ConstantConstrainedRuleTransformer();
        Expression nonMatchingResult = nonMatchingTransformer.ExposeTransform(Expression.Add(x, Expression.Constant(2.0)));
        Assert.IsFalse(nonMatchingTransformer.Invoked, "The rule must be skipped when the constant is not 1.0.");
        Assert.AreEqual(ExpressionType.Add, nonMatchingResult.NodeType);
    }

    // ---------------------------------------------------------------------------------------------
    // Coverage added for the dispatcher optimization (TransformPlan / TransformRule / TransformParameter
    // indexing candidate rules by ExpressionType). These tests lock in the properties the index must
    // preserve: a rule for a different ExpressionType is never even considered, same-type rules keep
    // their exact declaration order, wildcard rules remain candidates for every node type, wildcard and
    // type-specific rules interleave in their original order rather than being grouped, and specialized
    // Match() constraints (e.g. by call target) still filter within a bucket.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A transformer with one rule per <see cref="ExpressionType"/>, each recording whether it ran.</summary>
    private sealed class DistinctTypeRulesTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="OnAdd"/> was invoked.</summary>
        public bool AddRuleInvoked { get; private set; }

        /// <summary>Whether <see cref="OnMultiply"/> was invoked.</summary>
        public bool MultiplyRuleInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches any <see cref="ExpressionType.Add"/> node.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression OnAdd(BinaryExpression e, Expression left, Expression right)
        {
            AddRuleInvoked = true;
            return Expression.Constant(1.0);
        }

        /// <summary>Matches any <see cref="ExpressionType.Multiply"/> node; must never run for an Add node.</summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        private Expression OnMultiply(BinaryExpression e, Expression left, Expression right)
        {
            MultiplyRuleInvoked = true;
            return Expression.Constant(2.0);
        }
    }

    /// <summary>
    /// A rule declared for one <see cref="ExpressionType"/> must never be considered a candidate for a
    /// node of a different type: the index must exclude it, not merely rely on <c>Match</c> to reject it.
    /// </summary>
    [TestMethod]
    public void Transform_RuleForDifferentExpressionType_IsNeverInvoked()
    {
        var transformer = new DistinctTypeRulesTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(3.0));

        Expression result = transformer.ExposeTransform(add);

        Assert.IsTrue(transformer.AddRuleInvoked, "The Add rule must run for an Add node.");
        Assert.IsFalse(transformer.MultiplyRuleInvoked,
            "The Multiply rule belongs to a different ExpressionType bucket and must never run for an Add node.");
        Assert.AreEqual(1.0, ((ConstantExpression)result).Value);
    }

    /// <summary>Three rules for the same <see cref="ExpressionType.Add"/>: the first two defer via <see langword="null"/>.</summary>
    private sealed class ThreeAddRulesTransformer : ExpressionTransformer
    {
        /// <summary>The names of the rules invoked, in the order they ran.</summary>
        public List<string> InvokedOrder { get; } = new();

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Always defers to the next rule.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule1(BinaryExpression e, Expression left, Expression right)
        {
            InvokedOrder.Add(nameof(Rule1));
            return null;
        }

        /// <summary>Runs after <see cref="Rule1"/> defers and wins; <see cref="Rule3"/> must never run afterwards.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule2(BinaryExpression e, Expression left, Expression right)
        {
            InvokedOrder.Add(nameof(Rule2));
            return Expression.Constant(99.0);
        }

        /// <summary>Would run third, but the dispatch must already have returned <see cref="Rule2"/>'s result.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule3(BinaryExpression e, Expression left, Expression right)
        {
            InvokedOrder.Add(nameof(Rule3));
            return Expression.Constant(-1.0);
        }
    }

    /// <summary>
    /// Multiple rules for the same <see cref="ExpressionType"/> must be tried in their exact declaration
    /// order: the first one's <see langword="null"/> must defer to the second, and the second's non-null
    /// result must short-circuit before the third rule ever runs.
    /// </summary>
    [TestMethod]
    public void Transform_MultipleRulesForSameExpressionType_PreserveDeclarationOrder()
    {
        var transformer = new ThreeAddRulesTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(3.0));

        Expression result = transformer.ExposeTransform(add);

        CollectionAssert.AreEqual(new[] { "Rule1", "Rule2" }, transformer.InvokedOrder,
            "Rule1 must run and defer via null, Rule2 must run next and win; Rule3 must never run.");
        Assert.AreEqual(99.0, ((ConstantExpression)result).Value);
    }

    /// <summary>A transformer with a single wildcard (<c>ExpressionType == -1</c>) rule.</summary>
    private sealed class WildcardRuleTransformer : ExpressionTransformer
    {
        /// <summary>The node types <see cref="OnAny"/> was invoked for, in invocation order.</summary>
        public List<ExpressionType> MatchedNodeTypes { get; } = new();

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches every node type via the <c>(ExpressionType)(-1)</c> wildcard sentinel.</summary>
        [ExpressionSignature((ExpressionType)(-1))]
        private Expression OnAny(Expression e)
        {
            MatchedNodeTypes.Add(e.NodeType);
            return e;
        }
    }

    /// <summary>
    /// A rule declared with the <c>ExpressionType == -1</c> wildcard sentinel must remain a candidate
    /// for every node type, not just the type of the first expression it happens to see.
    /// </summary>
    [TestMethod]
    public void Transform_WildcardRule_IsCandidateForMultipleNodeTypes()
    {
        var transformer = new WildcardRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        transformer.ExposeTransform(x);
        transformer.ExposeTransform(Expression.Constant(1.0));
        transformer.ExposeTransform(Expression.Add(x, Expression.Constant(1.0)));

        CollectionAssert.AreEqual(
            new[] { ExpressionType.Parameter, ExpressionType.Constant, ExpressionType.Add },
            transformer.MatchedNodeTypes);
    }

    /// <summary>
    /// Two wildcard rules interleaved with two <see cref="ExpressionType.Add"/>-specific rules, in this
    /// exact declaration order: Wildcard1, Add1, Wildcard2, Add2. The first three defer via
    /// <see langword="null"/>.
    /// </summary>
    private sealed class WildcardAndSpecificOrderTransformer : ExpressionTransformer
    {
        /// <summary>The names of the rules invoked, in the order they ran.</summary>
        public List<string> InvokedOrder { get; } = new();

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>A wildcard rule, declared first; always defers.</summary>
        [ExpressionSignature((ExpressionType)(-1))]
        private Expression Wildcard1(BinaryExpression e, Expression left, Expression right)
        {
            InvokedOrder.Add(nameof(Wildcard1));
            return null;
        }

        /// <summary>An <see cref="ExpressionType.Add"/>-specific rule, declared second; always defers.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Add1(BinaryExpression e, Expression left, Expression right)
        {
            InvokedOrder.Add(nameof(Add1));
            return null;
        }

        /// <summary>A second wildcard rule, declared third; always defers.</summary>
        [ExpressionSignature((ExpressionType)(-1))]
        private Expression Wildcard2(BinaryExpression e, Expression left, Expression right)
        {
            InvokedOrder.Add(nameof(Wildcard2));
            return null;
        }

        /// <summary>An <see cref="ExpressionType.Add"/>-specific rule, declared last; wins.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Add2(BinaryExpression e, Expression left, Expression right)
        {
            InvokedOrder.Add(nameof(Add2));
            return Expression.Constant(123.0);
        }
    }

    /// <summary>
    /// The most important ordering guarantee: wildcard and type-specific rules must be tried in their
    /// exact original interleaved declaration order. Grouping all type-specific rules before (or after)
    /// all wildcard rules — a natural-looking but incorrect optimization — would reorder this sequence
    /// and must NOT happen.
    /// </summary>
    [TestMethod]
    public void Transform_CombinedWildcardAndSpecificRules_PreserveExactDeclarationOrder()
    {
        var transformer = new WildcardAndSpecificOrderTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(3.0));

        Expression result = transformer.ExposeTransform(add);

        CollectionAssert.AreEqual(
            new[] { "Wildcard1", "Add1", "Wildcard2", "Add2" },
            transformer.InvokedOrder,
            "Declaration order must be preserved exactly as interleaved, not grouped by wildcard vs. specific.");
        Assert.AreEqual(123.0, ((ConstantExpression)result).Value);
    }

    /// <summary>
    /// Two <see cref="ExpressionCallSignatureAttribute"/> rules that both declare
    /// <see cref="ExpressionType.Call"/> (so both share the same index bucket) but constrain different
    /// method names.
    /// </summary>
    private sealed class CallSignatureRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="OnSqrt"/> was invoked.</summary>
        public bool SqrtRuleInvoked { get; private set; }

        /// <summary>Whether <see cref="OnAbs"/> was invoked.</summary>
        public bool AbsRuleInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Matches only calls to <see cref="double.Sqrt(double)"/>.</summary>
        [ExpressionCallSignature(typeof(double), nameof(double.Sqrt))]
        private Expression OnSqrt(Expression e, Expression[] args)
        {
            SqrtRuleInvoked = true;
            return Expression.Constant(-1.0);
        }

        /// <summary>Matches only calls to <see cref="double.Abs(double)"/>; must not run for a Sqrt call.</summary>
        [ExpressionCallSignature(typeof(double), nameof(double.Abs))]
        private Expression OnAbs(Expression e, Expression[] args)
        {
            AbsRuleInvoked = true;
            return Expression.Constant(-2.0);
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// Sharing the same <see cref="ExpressionType.Call"/> index bucket must not be enough to select a
    /// rule: <see cref="ExpressionCallSignatureAttribute.Match(Expression)"/>'s function-name constraint
    /// must still be evaluated to pick the right one.
    /// </summary>
    [TestMethod]
    public void Transform_SpecializedCallSignatureAttribute_StillFiltersWithinTheCallBucket()
    {
        var transformer = new CallSignatureRuleTransformer();
        MethodInfo sqrt = typeof(double).GetMethod(nameof(double.Sqrt), new[] { typeof(double) })!;
        MethodCallExpression call = Expression.Call(sqrt, Expression.Constant(4.0));

        Expression result = transformer.ExposeTransform(call);

        Assert.IsTrue(transformer.SqrtRuleInvoked, "The Sqrt-specific rule must run.");
        Assert.IsFalse(transformer.AbsRuleInvoked,
            "The Abs-specific rule shares the Call bucket with the Sqrt rule but must be excluded by Match's function-name check.");
        Assert.AreEqual(-1.0, ((ConstantExpression)result).Value);
    }
}
