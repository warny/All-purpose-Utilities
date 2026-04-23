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

    /// <summary>
    /// Ensures <c>continue</c> in <see cref="ExpressionEx.For"/> still executes increment expressions.
    /// </summary>
    [TestMethod]
    public void ForContinueExecutesIncrementExpressions()
    {
        var iterator = Expression.Variable(typeof(int), "i");
        var sum = Expression.Variable(typeof(int), "sum");
        var continueLabel = Expression.Label("__continue__");

        var loop = ExpressionEx.For(
            iterator,
            Expression.Constant(0),
            Expression.LessThan(iterator, Expression.Constant(5)),
            [Expression.PostIncrementAssign(iterator)],
            Expression.IfThen(
                Expression.Equal(Expression.Modulo(iterator, Expression.Constant(2)), Expression.Constant(0)),
                Expression.Goto(continueLabel)),
            continueLoop: continueLabel);

        var body = Expression.Block(
            new[] { sum },
            Expression.Assign(sum, Expression.Constant(0)),
            loop,
            sum);

        var lambda = Expression.Lambda<Func<int>>(body);
        var result = lambda.Compile().Invoke();

        Assert.AreEqual(0, result);
    }

    /// <summary>
    /// Ensures <c>continue</c> in <see cref="ExpressionEx.Do"/> still evaluates the loop condition.
    /// </summary>
    [TestMethod]
    public void DoContinueEvaluatesCondition()
    {
        var iterator = Expression.Variable(typeof(int), "i");
        var continueLabel = Expression.Label("__continue__");

        var loop = ExpressionEx.Do(
            Expression.LessThan(Expression.PostIncrementAssign(iterator), Expression.Constant(3)),
            Expression.Goto(continueLabel),
            continueLoop: continueLabel);

        var body = Expression.Block(
            new[] { iterator },
            Expression.Assign(iterator, Expression.Constant(0)),
            loop,
            iterator);

        var lambda = Expression.Lambda<Func<int>>(body);
        var result = lambda.Compile().Invoke();

        Assert.AreEqual(4, result);
    }
}
