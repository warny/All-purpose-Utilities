using System;
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
}
