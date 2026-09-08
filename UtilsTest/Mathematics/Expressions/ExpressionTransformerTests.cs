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

    // ---------------------------------------------------------------------------------------------
    // Coverage for the compatibility fix requested during PR #573 review: ExpressionSignatureAttribute
    // is public with a virtual Match, so a third-party subclass could legally (1) override Match to
    // accept a node type other than the one declared to the constructor, or (2) be stateful. Both must
    // keep working exactly as they did before the ExpressionType-bucketing/parameter-caching indexing
    // was introduced: (1) requires the rule to remain a dispatch candidate for every node type rather
    // than only the declared one, and (2) requires CheckParameter to keep re-fetching (and therefore
    // re-constructing) a fresh attribute instance on every check instead of reusing a single cached one.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A custom method-level signature attribute whose <see cref="Match"/> override accepts
    /// <see cref="ExpressionType.Subtract"/> nodes even though it is declared (constructed) with
    /// <see cref="ExpressionType.Add"/>. Not one of the attribute types shipped in
    /// <c>ExpressionTranformer.cs</c>, so the transformer cannot assume it only matches its declared type.
    /// </summary>
    private sealed class WidensMatchBeyondDeclaredTypeAttribute : ExpressionSignatureAttribute
    {
        public WidensMatchBeyondDeclaredTypeAttribute() : base(ExpressionType.Add) { }

        public override bool Match(Expression e) => e.NodeType is ExpressionType.Add or ExpressionType.Subtract;
    }

    /// <summary>
    /// A transformer whose only rule is declared "Add" but, via a custom attribute, actually matches
    /// both Add and Subtract nodes.
    /// </summary>
    private sealed class CustomWideningAttributeTransformer : ExpressionTransformer
    {
        /// <summary>The node types <see cref="OnAddOrSubtract"/> was invoked for, in invocation order.</summary>
        public List<ExpressionType> InvokedNodeTypes { get; } = new();

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Declared "Add" but, via <see cref="WidensMatchBeyondDeclaredTypeAttribute"/>, also matches Subtract.</summary>
        [WidensMatchBeyondDeclaredType]
        private Expression OnAddOrSubtract(BinaryExpression e, Expression left, Expression right)
        {
            InvokedNodeTypes.Add(e.NodeType);
            return Expression.Constant(e.NodeType == ExpressionType.Add ? 1.0 : 2.0);
        }
    }

    /// <summary>
    /// A rule whose custom attribute's <see cref="ExpressionSignatureAttribute.Match(Expression)"/>
    /// override accepts a node type other than the one declared to the attribute's constructor must
    /// still be considered a dispatch candidate for that other node type. Bucketing solely by the
    /// declared <see cref="ExpressionSignatureAttribute.ExpressionType"/> would otherwise silently drop
    /// it for the Subtract node before <c>Match</c> even runs — a regression this test guards against.
    /// </summary>
    [TestMethod]
    public void Transform_CustomAttributeWideningMatchBeyondDeclaredType_StillConsideredForTheWidenedType()
    {
        var transformer = new CustomWideningAttributeTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        Expression addResult = transformer.ExposeTransform(Expression.Add(x, Expression.Constant(1.0)));
        Expression subtractResult = transformer.ExposeTransform(Expression.Subtract(x, Expression.Constant(1.0)));

        CollectionAssert.AreEqual(new[] { ExpressionType.Add, ExpressionType.Subtract }, transformer.InvokedNodeTypes,
            "The rule must run for both the declared type (Add) and the type Match widens into (Subtract).");
        Assert.AreEqual(1.0, ((ConstantExpression)addResult).Value);
        Assert.AreEqual(2.0, ((ConstantExpression)subtractResult).Value);
    }

    /// <summary>
    /// A custom parameter-level signature attribute that only matches the first time it is asked,
    /// via mutable instance state. Not one of the attribute types shipped in
    /// <c>ExpressionTranformer.cs</c>, so the transformer cannot assume it is safe to cache a single
    /// instance across every check.
    /// </summary>
    private sealed class MatchesOnceAttribute : ExpressionSignatureAttribute
    {
        private bool _used;

        public MatchesOnceAttribute() : base(WildcardExpressionTypeForTests) { }

        public override bool Match(Expression e)
        {
            if (_used) return false;
            _used = true;
            return true;
        }
    }

    /// <summary>The wildcard sentinel, re-exposed for <see cref="MatchesOnceAttribute"/>'s base constructor call.</summary>
    private const ExpressionType WildcardExpressionTypeForTests = (ExpressionType)(-1);

    /// <summary>
    /// A transformer with two Add rules, each constraining its right operand with a fresh
    /// <see cref="MatchesOnceAttribute"/> instance (one per parameter declaration): both must match,
    /// because <see cref="MatchesOnceAttribute"/> is stateful per-instance and each attribute usage
    /// is its own instance — the point under test is that the transformer doesn't introduce cross-check
    /// state sharing of its own by caching and reusing one materialized instance across dispatches.
    /// </summary>
    private sealed class StatefulParameterAttributeTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Constrains its constant operand with a stateful, match-once custom attribute.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression AddWithOnceConstrainedConstant(BinaryExpression e, Expression left, [MatchesOnce] ConstantExpression right)
            => left;
    }

    /// <summary>
    /// A parameter-level custom attribute whose <see cref="ExpressionSignatureAttribute.Match(Expression)"/>
    /// is stateful must be re-fetched (and therefore re-constructed) fresh for every check, exactly like
    /// the pre-indexing implementation always did for every parameter attribute. If the transformer
    /// instead cached and reused a single materialized instance across dispatches (as it safely does for
    /// the four attribute types shipped in <c>ExpressionTranformer.cs</c>, which are known to be
    /// stateless), only the very first Add node encountered by this transformer instance would ever
    /// match; every subsequent one would wrongly fall through to <see cref="FinalizeExpression"/> and
    /// throw, because the cached instance's <c>_used</c> flag would already be set.
    /// </summary>
    [TestMethod]
    public void Transform_CustomStatefulParameterAttribute_MatchesAgainOnASecondIndependentDispatch()
    {
        var transformer = new StatefulParameterAttributeTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        // Two independent Add nodes, dispatched one after another on the same transformer instance.
        Expression first = transformer.ExposeTransform(Expression.Add(x, Expression.Constant(1.0)));
        Expression second = transformer.ExposeTransform(Expression.Add(x, Expression.Constant(2.0)));

        Assert.AreSame(x, first, "The first Add node must match: a fresh MatchesOnceAttribute instance always matches once.");
        Assert.AreSame(x, second,
            "The second, independent Add node must also match: reusing a single cached attribute instance across " +
            "dispatches would incorrectly make it look 'already used' by the first check.");
    }

    /// <summary>
    /// A custom parameter-level signature attribute that counts its own constructions, to pin down
    /// exactly when (not just whether) it is instantiated.
    /// </summary>
    private sealed class ConstructionCountingAttribute : ExpressionSignatureAttribute
    {
        /// <summary>The number of times this attribute type has been constructed; reset by each test.</summary>
        public static int ConstructionCount;

        public ConstructionCountingAttribute() : base(WildcardExpressionTypeForTests)
        {
            ConstructionCount++;
        }

        public override bool Match(Expression e) => true;
    }

    /// <summary>A transformer whose rule constrains a parameter with <see cref="ConstructionCountingAttribute"/>.</summary>
    private sealed class ConstructionCountingParameterAttributeTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Constrains its constant operand with a construction-counting custom attribute.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression AddWithCountedConstant(BinaryExpression e, Expression left, [ConstructionCounting] ConstantExpression right)
            => left;
    }

    /// <summary>
    /// A custom parameter-level attribute must not be instantiated merely to build the
    /// <see cref="ExpressionTransformer"/>'s dispatch plan (i.e. it must not be constructed, used to
    /// read its runtime type, and discarded): <c>BuildRule</c> must determine whether an attribute's
    /// type is known-safe to cache via <c>CustomAttributeData</c> (which exposes the attribute's type
    /// without invoking its constructor), never by instantiating it first and inspecting the instance.
    /// A custom attribute's constructor could have observable side effects or throw, and the
    /// pre-indexing implementation only ever constructed it inside <c>CheckParameter</c>, on demand.
    /// </summary>
    [TestMethod]
    public void Transform_CustomParameterAttribute_IsNotConstructedBeforeItIsActuallyChecked()
    {
        ConstructionCountingAttribute.ConstructionCount = 0;

        var transformer = new ConstructionCountingParameterAttributeTransformer();
        Assert.AreEqual(0, ConstructionCountingAttribute.ConstructionCount,
            "Building the transformer's dispatch plan must not construct a custom parameter attribute merely to inspect its type.");

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        transformer.ExposeTransform(Expression.Add(x, Expression.Constant(1.0)));
        Assert.AreEqual(1, ConstructionCountingAttribute.ConstructionCount,
            "The first dispatch that actually checks the parameter must construct exactly one instance.");

        transformer.ExposeTransform(Expression.Add(x, Expression.Constant(2.0)));
        Assert.AreEqual(2, ConstructionCountingAttribute.ConstructionCount,
            "A second, independent dispatch must construct a fresh instance rather than reusing a cached one.");
    }

    /// <summary>A transformer whose only rule declares an <see cref="ExpressionType"/> value outside the real enum.</summary>
    private sealed class OutOfRangeExpressionTypeRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="NeverMatchesAnything"/> was ever invoked.</summary>
        public bool RuleWasInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>
        /// Nothing in the public API stops a caller from writing an out-of-range <see cref="ExpressionType"/>
        /// value that is neither a real node type nor the <c>-1</c> wildcard sentinel; such a rule must
        /// simply never match anything, exactly as <c>e.NodeType == ExpressionType</c> never would have.
        /// </summary>
        [ExpressionSignature((ExpressionType)123456)]
        private Expression NeverMatchesAnything(Expression e)
        {
            RuleWasInvoked = true;
            return e;
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule declaring an <see cref="ExpressionType"/> value outside the real enum values (and not the
    /// wildcard sentinel) must not make dispatch-plan construction throw <see cref="KeyNotFoundException"/>
    /// — the plan's per-type buckets are only pre-populated for <see cref="Enum.GetValues{TEnum}"/>'s real
    /// values, so indexing straight into the dictionary for an out-of-range declared type would throw as
    /// soon as the transformer is constructed, before any expression is ever transformed.
    /// </summary>
    [TestMethod]
    public void Transform_RuleWithExpressionTypeOutsideTheRealEnum_ConstructsAndNeverMatches()
    {
        var transformer = new OutOfRangeExpressionTypeRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        Expression result = transformer.ExposeTransform(add);

        Assert.IsFalse(transformer.RuleWasInvoked,
            "A rule declaring an ExpressionType outside the real enum values must never be invoked.");
        Assert.AreEqual(ExpressionType.Add, result.NodeType);
    }

    /// <summary>
    /// A transformer whose only rule is declared on a <see langword="virtual"/> base method, with a
    /// <see cref="ConstantNumericAttribute"/> constraint on one of its parameters. The concrete
    /// transformer under test overrides that method without repeating either attribute, relying on
    /// <see cref="AttributeUsageAttribute.Inherited"/> being <see langword="true"/> on
    /// <see cref="ExpressionSignatureAttribute"/> for both the method-level and parameter-level
    /// constraints to still apply to the override.
    /// </summary>
    private abstract class BaseWithConstrainedVirtualRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="Rule"/> was invoked.</summary>
        public bool RuleWasInvoked { get; protected set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>
        /// Matches <c>left + 0</c> only: the <see cref="ConstantNumericAttribute"/> on <paramref name="right"/>
        /// restricts this rule to that specific constant value. Declared <see langword="virtual"/> so a
        /// derived class can override it without repeating either attribute.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        protected virtual Expression Rule(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right)
        {
            RuleWasInvoked = true;
            return left;
        }

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// Overrides <see cref="BaseWithConstrainedVirtualRuleTransformer.Rule"/> without repeating either
    /// the method-level <see cref="ExpressionSignatureAttribute"/> or the parameter-level
    /// <see cref="ConstantNumericAttribute"/> — both must still apply via .NET attribute inheritance.
    /// </summary>
    private sealed class DerivedWithConstrainedVirtualRuleTransformer : BaseWithConstrainedVirtualRuleTransformer
    {
        /// <inheritdoc />
        protected override Expression Rule(BinaryExpression e, Expression left, ConstantExpression right)
            => base.Rule(e, left, right);
    }

    /// <summary>
    /// A parameter-level <see cref="ExpressionSignatureAttribute"/>-derived constraint declared on a base
    /// virtual method's parameter must still apply when a derived class overrides that method without
    /// repeating the attribute — exactly as plain .NET reflection resolves it via
    /// <see cref="AttributeUsageAttribute.Inherited"/>. <c>BuildRule</c> must not use an attribute-lookup
    /// API that skips this inheritance chain to decide how to precompute the override's parameter
    /// metadata, or the constraint would be silently dropped instead of merely handled less efficiently.
    /// </summary>
    [TestMethod]
    public void Transform_ParameterAttributeInheritedFromOverriddenBaseMethod_StillConstrainsTheOverride()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        var matchingTransformer = new DerivedWithConstrainedVirtualRuleTransformer();
        Expression matchingResult = matchingTransformer.ExposeTransform(Expression.Add(x, Expression.Constant(0.0)));
        Assert.IsTrue(matchingTransformer.RuleWasInvoked,
            "The rule must run when the constant satisfies the inherited ConstantNumeric(0) constraint.");
        Assert.AreSame(x, matchingResult);

        var nonMatchingTransformer = new DerivedWithConstrainedVirtualRuleTransformer();
        Expression nonMatchingResult = nonMatchingTransformer.ExposeTransform(Expression.Add(x, Expression.Constant(2.0)));
        Assert.IsFalse(nonMatchingTransformer.RuleWasInvoked,
            "The rule must be skipped when the constant doesn't satisfy the inherited ConstantNumeric(0) " +
            "constraint, even though the override itself carries no attribute at all.");
        Assert.AreEqual(ExpressionType.Add, nonMatchingResult.NodeType);
    }

    /// <summary>
    /// A custom <see cref="Expression"/> subclass whose <c>NodeType</c> returns a value outside the real
    /// <see cref="ExpressionType"/> enum values. Legal: <see cref="Expression"/> is publicly derivable
    /// (its constructor is <see langword="protected"/>) and its <c>NodeType</c> property is
    /// <see langword="virtual"/>, so nothing in the public API stops this.
    /// </summary>
    private sealed class OutOfRangeNodeTypeExpression : Expression
    {
        /// <inheritdoc />
        public override ExpressionType NodeType => (ExpressionType)123456;

        /// <inheritdoc />
        public override Type Type => typeof(double);
    }

    /// <summary>
    /// The dispatch-plan buckets only cover the real <see cref="ExpressionType"/> values known at build
    /// time (<see cref="Enum.GetValues{TEnum}"/>); a node whose <c>NodeType</c> falls outside all of them
    /// (see <see cref="OutOfRangeNodeTypeExpression"/>) must still be offered every rule as candidates —
    /// both a rule declared for that exact out-of-range value and a wildcard rule — exactly as the
    /// pre-indexing linear scan evaluated every rule's <c>Match</c> against every node regardless of its
    /// <c>NodeType</c>. Reuses <see cref="OutOfRangeExpressionTypeRuleTransformer"/> (whose rule is
    /// declared for the exact same out-of-range value used here) and <see cref="WildcardRuleTransformer"/>.
    /// </summary>
    [TestMethod]
    public void Transform_ExpressionWithOutOfRangeNodeType_StillDispatchesMatchingAndWildcardRules()
    {
        var customNode = new OutOfRangeNodeTypeExpression();

        var specificTransformer = new OutOfRangeExpressionTypeRuleTransformer();
        specificTransformer.ExposeTransform(customNode);
        Assert.IsTrue(specificTransformer.RuleWasInvoked,
            "A rule declared for the exact out-of-range ExpressionType a custom Expression subclass's " +
            "NodeType returns must still be invoked for it: no bucket was ever pre-populated for that " +
            "value, so the dispatcher must fall back to the complete, unfiltered rule list.");

        var wildcardTransformer = new WildcardRuleTransformer();
        wildcardTransformer.ExposeTransform(customNode);
        CollectionAssert.Contains(wildcardTransformer.MatchedNodeTypes, customNode.NodeType,
            "A wildcard rule must remain a candidate for a node type outside the real ExpressionType enum " +
            "values too, not just for the ~80 pre-populated buckets.");
    }

    // ------------------------------------------------------------------------------------------
    // Characterization tests for the MethodInfo.Invoke-based dispatch, written and locked in
    // BEFORE introducing a MethodInvoker-based fast path (see the PR that added this section).
    // Every test below must pass unmodified both before and after that change: they pin down
    // exactly how a rule's exception is wrapped, and exactly what happens for malformed rule
    // signatures (too few/too many declared parameters relative to the node's sub-expressions),
    // so that a future optimization of the invocation mechanism cannot silently change either.
    // ------------------------------------------------------------------------------------------

    /// <summary>A transformer whose single positional (3-parameter) rule always throws.</summary>
    private sealed class PositionalThrowsTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Unconditionally throws to characterize how the positional invocation branch wraps a rule's exception.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule(BinaryExpression e, Expression left, Expression right)
            => throw new InvalidOperationException("positional rule failure");

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule invoked through the <c>InvocationKind.Positional</c> branch that throws must have
    /// its exception surface wrapped in <see cref="TargetInvocationException"/> (the behavior of
    /// <see cref="MethodBase.Invoke(object, object[])"/>), with the rule's own exception as
    /// <see cref="Exception.InnerException"/> and its message preserved.
    /// </summary>
    [TestMethod]
    public void Transform_PositionalRuleThrows_WrappedInTargetInvocationException()
    {
        var transformer = new PositionalThrowsTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        var thrown = Assert.ThrowsExactly<TargetInvocationException>(() => transformer.ExposeTransform(add));

        Assert.IsInstanceOfType<InvalidOperationException>(thrown.InnerException);
        Assert.AreEqual("positional rule failure", thrown.InnerException!.Message);
    }

    /// <summary>A transformer whose single-parameter rule always throws.</summary>
    private sealed class SingleThrowsTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Unconditionally throws to characterize how the single-parameter invocation branch wraps a rule's exception.</summary>
        [ExpressionSignature(ExpressionType.Parameter)]
        private Expression Rule(ParameterExpression e)
            => throw new InvalidOperationException("single rule failure");
    }

    /// <summary>
    /// A rule invoked through the <c>InvocationKind.Single</c> branch that throws must also
    /// surface wrapped in <see cref="TargetInvocationException"/>, exactly like the positional branch.
    /// </summary>
    [TestMethod]
    public void Transform_SingleParameterRuleThrows_WrappedInTargetInvocationException()
    {
        var transformer = new SingleThrowsTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        var thrown = Assert.ThrowsExactly<TargetInvocationException>(() => transformer.ExposeTransform(x));

        Assert.IsInstanceOfType<InvalidOperationException>(thrown.InnerException);
        Assert.AreEqual("single rule failure", thrown.InnerException!.Message);
    }

    /// <summary>A transformer whose <c>Expression[]</c>-shaped rule always throws.</summary>
    private sealed class ExpressionArrayThrowsTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Unconditionally throws to characterize how the <c>Expression[]</c> invocation branch wraps a rule's exception.</summary>
        [ExpressionSignature(ExpressionType.Call)]
        private Expression Rule(Expression e, Expression[] args)
            => throw new InvalidOperationException("expression-array rule failure");
    }

    /// <summary>
    /// A rule invoked through the <c>InvocationKind.ExpressionArray</c> branch that throws must
    /// also surface wrapped in <see cref="TargetInvocationException"/>.
    /// </summary>
    [TestMethod]
    public void Transform_ExpressionArrayRuleThrows_WrappedInTargetInvocationException()
    {
        var transformer = new ExpressionArrayThrowsTransformer();
        MethodInfo sqrt = typeof(Math).GetMethod(nameof(Math.Sqrt), new[] { typeof(double) })!;
        MethodCallExpression call = Expression.Call(sqrt, Expression.Constant(4.0));

        var thrown = Assert.ThrowsExactly<TargetInvocationException>(() => transformer.ExposeTransform(call));

        Assert.IsInstanceOfType<InvalidOperationException>(thrown.InnerException);
        Assert.AreEqual("expression-array rule failure", thrown.InnerException!.Message);
    }

    /// <summary>A transformer whose rule throws an <see cref="ArgumentException"/> rather than a generic exception.</summary>
    private sealed class ArgumentExceptionRuleTransformer : ExpressionTransformer
    {
        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Unconditionally throws <see cref="ArgumentException"/>.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule(BinaryExpression e, Expression left, Expression right)
            => throw new ArgumentException("argument rule failure");

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// A rule throwing <see cref="ArgumentException"/> — a type that <em>could</em> otherwise be
    /// (mis)interpreted as an argument-count/type error coming from the invocation mechanism itself —
    /// must still be treated as the rule's own exception and wrapped in
    /// <see cref="TargetInvocationException"/>, not re-thrown directly or swallowed. This guards against
    /// a future fast-path implementation that conflates "the rule threw ArgumentException" with
    /// "the invoker itself rejected the call".
    /// </summary>
    [TestMethod]
    public void Transform_RuleThrowsArgumentException_WrappedInTargetInvocationException()
    {
        var transformer = new ArgumentExceptionRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        var thrown = Assert.ThrowsExactly<TargetInvocationException>(() => transformer.ExposeTransform(add));

        Assert.IsInstanceOfType<ArgumentException>(thrown.InnerException);
        Assert.AreEqual("argument rule failure", thrown.InnerException!.Message);
    }

    /// <summary>A transformer whose rule itself throws an already-constructed <see cref="TargetInvocationException"/>.</summary>
    private sealed class NestedTargetInvocationExceptionRuleTransformer : ExpressionTransformer
    {
        /// <summary>The exact exception instance the rule throws, for identity comparison by the test.</summary>
        public static readonly InvalidOperationException InnermostException = new("innermost failure");

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Unconditionally throws a <see cref="TargetInvocationException"/> wrapping <see cref="InnermostException"/>.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule(BinaryExpression e, Expression left, Expression right)
            => throw new TargetInvocationException(InnermostException);

        /// <inheritdoc cref="ExpressionTransformer.FinalizeExpression"/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
            => CopyExpression(e, parameters);
    }

    /// <summary>
    /// When the rule itself throws a <see cref="TargetInvocationException"/> (rather than some other
    /// exception type), <see cref="MethodBase.Invoke(object, object[])"/> wraps it exactly like any other
    /// exception: the result is a <em>new, distinct</em> <see cref="TargetInvocationException"/> whose
    /// <see cref="Exception.InnerException"/> is the <see cref="TargetInvocationException"/> the rule
    /// threw (which itself wraps the innermost exception one level deeper). A future optimization must
    /// not special-case <c>catch (TargetInvocationException) { throw; }</c> to "avoid double wrapping":
    /// that would not be equivalent to this historical double-wrap behavior.
    /// </summary>
    [TestMethod]
    public void Transform_RuleThrowsTargetInvocationException_IsDoubleWrapped()
    {
        var transformer = new NestedTargetInvocationExceptionRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        var outer = Assert.ThrowsExactly<TargetInvocationException>(() => transformer.ExposeTransform(add));

        var middle = outer.InnerException as TargetInvocationException;
        Assert.IsNotNull(middle, "The outer exception's InnerException must itself be a TargetInvocationException " +
            "(the exact instance the rule threw), not the innermost exception directly.");
        Assert.AreNotSame(outer, middle, "The outer exception must be a NEW TargetInvocationException, distinct from the one the rule threw.");
        Assert.AreSame(NestedTargetInvocationExceptionRuleTransformer.InnermostException, middle!.InnerException,
            "The rule-thrown TargetInvocationException's own InnerException must survive unchanged one level deeper.");
    }

    /// <summary>
    /// A positional rule declaring FEWER parameters than the node's context supplies (here: only
    /// <c>left</c>, omitting <c>right</c>, for a 3-slot <see cref="ExpressionType.Add"/> context).
    /// </summary>
    private sealed class TooFewParametersRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="Rule"/> was ever entered (it should never be, per the characterization below).</summary>
        public bool RuleWasInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Declares only 2 parameters (e, left) though the Add context supplies 3 (e, left, right).</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule(BinaryExpression e, Expression left)
        {
            RuleWasInvoked = true;
            return left;
        }
    }

    /// <summary>
    /// Historical behavior (pinned down before introducing any fast invocation path): a positional rule
    /// declaring fewer parameters than the node's context array supplies passes per-parameter validation
    /// (which only checks the parameters the rule actually declares) and then reaches
    /// <see cref="MethodBase.Invoke(object, object[])"/> with an argument array longer than the method's
    /// parameter list, which throws <see cref="TargetParameterCountException"/> — an error of the
    /// invocation mechanism itself, NOT of the rule body, and therefore must never be wrapped in
    /// <see cref="TargetInvocationException"/>. A future fast path must reproduce this exact failure
    /// (or fall back to <see cref="MethodBase.Invoke(object, object[])"/> for this shape) rather than
    /// silently "repairing" the call by truncating the argument list to the first two elements.
    /// </summary>
    [TestMethod]
    public void Transform_PositionalRuleWithFewerParametersThanContext_ThrowsTargetParameterCountException()
    {
        var transformer = new TooFewParametersRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        Assert.ThrowsExactly<TargetParameterCountException>(() => transformer.ExposeTransform(add));
    }

    /// <summary>
    /// A positional rule declaring MORE parameters than the node's context supplies (here: 4 parameters
    /// for a 3-slot <see cref="ExpressionType.Add"/> context, with a trailing unmatched <c>extra</c>).
    /// </summary>
    private sealed class TooManyParametersRuleTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="Rule"/> was ever entered (it should never be, per the characterization below).</summary>
        public bool RuleWasInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Declares 4 parameters (e, left, right, extra) though the Add context only supplies 3.</summary>
        [ExpressionSignature(ExpressionType.Add)]
        private Expression Rule(BinaryExpression e, Expression left, Expression right, Expression extra)
        {
            RuleWasInvoked = true;
            return left;
        }
    }

    /// <summary>
    /// Historical behavior (pinned down before introducing any fast invocation path): a positional rule
    /// declaring more parameters than the node's context array supplies fails during per-parameter
    /// validation itself — the loop indexes <c>context.Parameters[i]</c> up to <c>rule.Parameters.Length - 1</c>,
    /// which runs past the end of the (shorter) context array — throwing <see cref="IndexOutOfRangeException"/>
    /// before <see cref="MethodBase.Invoke(object, object[])"/> is ever reached. A future fast path must not
    /// convert this into a silent "rule doesn't match, try the next one" outcome.
    /// </summary>
    [TestMethod]
    public void Transform_PositionalRuleWithMoreParametersThanContext_ThrowsIndexOutOfRangeException()
    {
        var transformer = new TooManyParametersRuleTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression add = Expression.Add(x, Expression.Constant(1.0));

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => transformer.ExposeTransform(add));
        Assert.IsFalse(transformer.RuleWasInvoked, "The rule body must never run: the failure happens during parameter validation.");
    }

    /// <summary>A transformer whose single-parameter rule always matches but returns null.</summary>
    private sealed class SingleReturnsNullTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="Rule"/> was invoked.</summary>
        public bool RuleWasInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Always matches a Parameter node but returns null.</summary>
        [ExpressionSignature(ExpressionType.Parameter)]
        private Expression Rule(ParameterExpression e)
        {
            RuleWasInvoked = true;
            return null;
        }
    }

    /// <summary>
    /// Unlike the positional branch, a single-parameter rule returning <see langword="null"/> is
    /// considered to have been APPLIED (it is not retried against the next candidate rule):
    /// <see cref="ExpressionTransformer.Transform(Expression)"/> itself returns <see langword="null"/>.
    /// This historical inconsistency between invocation shapes (see the private InvocationKind enum) is
    /// deliberately preserved, not normalized, by this PR.
    /// </summary>
    [TestMethod]
    public void Transform_SingleParameterRuleReturningNull_IsConsideredApplied_TransformReturnsNull()
    {
        var transformer = new SingleReturnsNullTransformer();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        Expression result = transformer.ExposeTransform(x);

        Assert.IsTrue(transformer.RuleWasInvoked);
        Assert.IsNull(result);
    }

    /// <summary>A transformer whose <c>Expression[]</c>-shaped rule always matches but returns null.</summary>
    private sealed class ExpressionArrayReturnsNullTransformer : ExpressionTransformer
    {
        /// <summary>Whether <see cref="Rule"/> was invoked.</summary>
        public bool RuleWasInvoked { get; private set; }

        /// <summary>Calls the protected <see cref="ExpressionTransformer.Transform(Expression)"/> method for direct unit testing.</summary>
        public Expression ExposeTransform(Expression e) => Transform(e);

        /// <summary>Always matches a Call node but returns null.</summary>
        [ExpressionSignature(ExpressionType.Call)]
        private Expression Rule(Expression e, Expression[] args)
        {
            RuleWasInvoked = true;
            return null;
        }
    }

    /// <summary>
    /// Like the single-parameter branch (and unlike the positional branch), an <c>Expression[]</c>-shaped
    /// rule returning <see langword="null"/> is also considered to have been APPLIED:
    /// <see cref="ExpressionTransformer.Transform(Expression)"/> itself returns <see langword="null"/>
    /// rather than trying the next candidate rule.
    /// </summary>
    [TestMethod]
    public void Transform_ExpressionArrayRuleReturningNull_IsConsideredApplied_TransformReturnsNull()
    {
        var transformer = new ExpressionArrayReturnsNullTransformer();
        MethodInfo sqrt = typeof(Math).GetMethod(nameof(Math.Sqrt), new[] { typeof(double) })!;
        MethodCallExpression call = Expression.Call(sqrt, Expression.Constant(4.0));

        Expression result = transformer.ExposeTransform(call);

        Assert.IsTrue(transformer.RuleWasInvoked);
        Assert.IsNull(result);
    }
}
