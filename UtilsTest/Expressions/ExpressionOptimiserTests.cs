using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Verifies standard optimization rewrites for <see cref="ExpressionOptimiser"/>.
/// </summary>
[TestClass]
public class ExpressionOptimiserTests
{
    /// <summary>
    /// Ensures arithmetic identity rewrites are applied.
    /// </summary>
    [TestMethod]
    public void Optimize_AppliesArithmeticIdentities()
    {
        Expression<Func<double, double>> expression = x => (x * 1.0) + 0.0;
        var optimiser = new ExpressionOptimiser();

        var optimized = (Expression<Func<double, double>>)optimiser.Optimize(expression);

        Assert.AreEqual("x => x", optimized.ToString());
    }

    /// <summary>
    /// Ensures boolean short-circuit rewrites simplify constant operands.
    /// </summary>
    [TestMethod]
    public void Optimize_SimplifiesBooleanShortCircuit()
    {
        Expression<Func<bool, bool>> expression = flag => (true && flag) || false;
        var optimiser = new ExpressionOptimiser();

        var optimized = (Expression<Func<bool, bool>>)optimiser.Optimize(expression);

        Assert.AreEqual("flag => flag", optimized.ToString());
    }

    /// <summary>
    /// Ensures constant-conditional rewrites select the correct branch.
    /// </summary>
    [TestMethod]
    public void Optimize_SimplifiesConstantConditionals()
    {
        Expression<Func<int>> expression = () => true ? 2 : 3;
        var optimiser = new ExpressionOptimiser();

        var optimized = (Expression<Func<int>>)optimiser.Optimize(expression);

        Assert.AreEqual(2, optimized.Compile().Invoke());
    }

    /// <summary>
    /// Ensures that <c>expr &amp;&amp; false</c> still evaluates <c>expr</c> for side effects.
    /// </summary>
    [TestMethod]
    public void Optimize_AndAlsoFalse_PreservesLeftSideEffect()
    {
        var counter = Expression.Parameter(typeof(int).MakeByRefType(), "counter");
        var increment = Expression.PreIncrementAssign(counter);
        var andAlso = Expression.AndAlso(Expression.GreaterThan(increment, Expression.Constant(int.MaxValue)), Expression.Constant(false));
        var optimiser = new ExpressionOptimiser();

        var optimized = optimiser.Optimize(andAlso);

        // The optimised tree must not be a simple false constant — left must still execute.
        Assert.AreNotEqual(ExpressionType.Constant, optimized.NodeType,
            "Optimising expr && false must not discard the left operand.");
    }

    /// <summary>
    /// Ensures that <c>expr || true</c> still evaluates <c>expr</c> for side effects.
    /// </summary>
    [TestMethod]
    public void Optimize_OrElseTrue_PreservesLeftSideEffect()
    {
        var flag = Expression.Parameter(typeof(bool), "flag");
        var orElse = Expression.OrElse(flag, Expression.Constant(true));
        var optimiser = new ExpressionOptimiser();

        var optimized = optimiser.Optimize(orElse);

        // The optimised tree must not be a simple true constant — left must still execute.
        Assert.AreNotEqual(ExpressionType.Constant, optimized.NodeType,
            "Optimising expr || true must not discard the left operand.");
    }

    /// <summary>
    /// Ensures multiply-by-zero optimisation handles nullable numeric types without throwing.
    /// </summary>
    [TestMethod]
    public void Optimize_MultiplyByZero_NullableType_DoesNotThrow()
    {
        var x = Expression.Parameter(typeof(int?), "x");
        var multiply = Expression.Multiply(x, Expression.Constant((int?)0, typeof(int?)));
        var optimiser = new ExpressionOptimiser();

        Expression result = optimiser.Optimize(multiply);

        Assert.IsInstanceOfType<ConstantExpression>(result);
        Assert.AreEqual(typeof(int?), result.Type);
    }
}
