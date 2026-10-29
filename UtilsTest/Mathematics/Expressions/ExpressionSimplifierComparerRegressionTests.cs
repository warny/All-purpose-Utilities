using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Mandatory end-to-end regression for the S1 <see cref="ExpressionComparer"/> hardening: proves that the
/// baseline's <see cref="BinaryExpression.Method"/>-blind comparison could let
/// <see cref="ExpressionSimplifier"/> apply the <c>sin(x) / cos(x) -&gt; tan(x)</c> identity to two
/// operands that only look alike (same <see cref="ExpressionType.Add"/> shape, same left/right operands)
/// but are built from two different custom operator methods - a real, dangerous false positive, not merely
/// a comparer-unit-test concern. The existing built-in trigonometric/power identity matrix (ordinary
/// <c>double</c> operands, no custom operators) is unaffected by this hardening and continues to pass
/// unchanged in <see cref="ExpressionSimplifierTests"/> as part of the full Unit run.
/// </summary>
[TestClass]
public class ExpressionSimplifierComparerRegressionTests
{
    /// <summary>A custom addition operator with an observably different implementation from <see cref="CustomAddB"/>.</summary>
    /// <param name="x">First operand.</param>
    /// <param name="y">Second operand.</param>
    /// <returns><paramref name="x"/> plus <paramref name="y"/>.</returns>
    private static double CustomAddA(double x, double y) => x + y;

    /// <summary>A custom addition operator with an observably different implementation from <see cref="CustomAddA"/>.</summary>
    /// <param name="x">First operand.</param>
    /// <param name="y">Second operand.</param>
    /// <returns><paramref name="x"/> plus <paramref name="y"/>, negated, so the two methods are observably different.</returns>
    private static double CustomAddB(double x, double y) => -(x + y);

    /// <summary>
    /// Mandatory regression: <c>sin(CustomAddA(x, y)) / cos(CustomAddB(x, y))</c> must NOT be rewritten to
    /// <c>tan(CustomAddA(x, y))</c>, because the two additions use different operator methods and are
    /// therefore not the same argument even though their operands match. On the pre-#590 baseline this
    /// rewrite incorrectly fired (verified separately by
    /// <see cref="ExpressionComparerTests.Binary_SameOperandsDifferentMethod_ReturnsFalse"/>), which would
    /// silently change the evaluated result for any custom numeric type overloading its addition operator.
    /// </summary>
    [TestMethod]
    public void SinOverCos_DifferentCustomAddMethods_DoesNotRewriteToTan()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source =
            (x, y) => double.Sin(CustomAddA(x, y)) / double.Cos(CustomAddB(x, y));

        var simplified = (Expression<Func<double, double, double>>)simplifier.Simplify(source);

        if (simplified.Body is MethodCallExpression rewritten)
        {
            Assert.AreNotEqual(nameof(double.Tan), rewritten.Method.Name,
                "the sin/cos -> tan identity must not fire across two different custom operator methods");
        }

        Func<double, double, double> compiledSource = source.Compile();
        Func<double, double, double> compiledSimplified = simplified.Compile();

        foreach ((double x, double y) in new[] { (0.3, 0.7), (1.1, -0.4), (-2.5, 0.05), (0.0, 1.0) })
        {
            Assert.AreEqual(compiledSource(x, y), compiledSimplified(x, y), 1e-9);
        }
    }

    /// <summary>
    /// Positive control for <see cref="SinOverCos_DifferentCustomAddMethods_DoesNotRewriteToTan"/>: when
    /// both arguments are the SAME call of the same method with the same operands, the identity
    /// must still fire, proving the fix did not simply disable the rule. Since ETC R1b (C-06) the identity merges the two
    /// evaluations of the argument only when that argument is proven inert, so the control uses an allow-listed pure
    /// function (<see cref="Math.Atan2(double, double)"/>); the user method variant is
    /// <see cref="SinOverCos_SameCustomAddMethod_KeepsBothEvaluations"/>.
    /// </summary>
    [TestMethod]
    public void SinOverCos_SameSafeMethod_StillRewritesToTan()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source =
            (x, y) => double.Sin(Math.Atan2(x, y)) / double.Cos(Math.Atan2(x, y));

        var simplified = (Expression<Func<double, double, double>>)simplifier.Simplify(source);

        Assert.IsInstanceOfType<MethodCallExpression>(simplified.Body);
        Assert.AreEqual(nameof(double.Tan), ((MethodCallExpression)simplified.Body).Method.Name);

        Func<double, double, double> compiledSource = source.Compile();
        Func<double, double, double> compiledSimplified = simplified.Compile();

        foreach ((double x, double y) in new[] { (0.3, 0.7), (1.1, -0.4), (-2.5, 0.05) })
        {
            Assert.AreEqual(compiledSource(x, y), compiledSimplified(x, y), 1e-9);
        }
    }

    /// <summary>
    /// Same scenario as the historical positive control with the user method <see cref="CustomAddA"/> as the shared
    /// argument (ETC R1b, C-06): the method is not proven free of effects, so merging its two evaluations into one
    /// <c>tan</c> argument is no longer allowed. The division keeps both calls, and the result is unchanged.
    /// </summary>
    [TestMethod]
    public void SinOverCos_SameCustomAddMethod_KeepsBothEvaluations()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source =
            (x, y) => double.Sin(CustomAddA(x, y)) / double.Cos(CustomAddA(x, y));

        var simplified = (Expression<Func<double, double, double>>)simplifier.Simplify(source);

        var division = (BinaryExpression)simplified.Body;
        Assert.AreEqual(ExpressionType.Divide, division.NodeType);
        Assert.AreEqual(nameof(double.Sin), ((MethodCallExpression)division.Left).Method.Name);
        Assert.AreEqual(nameof(double.Cos), ((MethodCallExpression)division.Right).Method.Name);

        Func<double, double, double> compiledSource = source.Compile();
        Func<double, double, double> compiledSimplified = simplified.Compile();

        foreach ((double x, double y) in new[] { (0.3, 0.7), (1.1, -0.4), (-2.5, 0.05) })
        {
            Assert.AreEqual(compiledSource(x, y), compiledSimplified(x, y), 1e-9);
        }
    }
}
