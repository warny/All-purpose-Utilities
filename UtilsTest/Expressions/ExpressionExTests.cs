using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq.Expressions;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Provides regression tests for <see cref="ExpressionEx"/> helper methods.
/// </summary>
[TestClass]
public sealed class ExpressionExTests
{
    /// <summary>
    /// Ensures that the <see cref="ExpressionEx.For(ParameterExpression, Expression, Expression, Expression[], Expression, System.Linq.Expressions.LabelTarget?, System.Linq.Expressions.LabelTarget?)"/> helper produces a loop that executes its increment expression.
    /// </summary>
    [TestMethod]
    public void ForExecutesIncrementAndBody()
    {
        var iterator = Expression.Variable(typeof(int), "i");
        var sum = Expression.Variable(typeof(int), "sum");

        var loop = ExpressionEx.For(
                iterator,
                Expression.Constant(0),
                Expression.LessThan(iterator, Expression.Constant(4)),
                [Expression.PostIncrementAssign(iterator)],
                Expression.AddAssign(sum, iterator));

        var body = Expression.Block(
                new[] { sum },
                Expression.Assign(sum, Expression.Constant(0)),
                loop,
                sum);

        var lambda = Expression.Lambda<Func<int>>(body);
        var result = lambda.Compile().Invoke();

        Assert.AreEqual(6, result);
    }
}
