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

    /// <summary>
    /// Ensures foreach over arrays uses index-based loop construction instead of enumerator calls.
    /// </summary>
    [TestMethod]
    public void ForEach_Array_UsesIndexBasedLoop()
    {
        var iterator = Expression.Variable(typeof(int), "item");
        var enumerable = Expression.Variable(typeof(int[]), "values");
        var loop = ExpressionEx.ForEach(iterator, enumerable, Expression.Empty());

        Assert.IsFalse(ContainsMethodCall(loop, nameof(System.Collections.IEnumerable.GetEnumerator)));
        Assert.IsFalse(ContainsMethodCall(loop, nameof(System.Collections.IEnumerator.MoveNext)));
    }

    /// <summary>
    /// Checks whether an expression tree contains a method call by name.
    /// </summary>
    /// <param name="expression">Expression tree to inspect.</param>
    /// <param name="methodName">Method name to search for.</param>
    /// <returns><see langword="true"/> when the method call is found; otherwise <see langword="false"/>.</returns>
    private static bool ContainsMethodCall(Expression expression, string methodName)
    {
        var visitor = new MethodNameSearchVisitor(methodName);
        visitor.Visit(expression);
        return visitor.Found;
    }

    /// <summary>
    /// Visits expression trees and tracks whether a matching method call was found.
    /// </summary>
    private sealed class MethodNameSearchVisitor : ExpressionVisitor
    {
        private readonly string _methodName;

        /// <summary>
        /// Gets a value indicating whether the target method call has been found.
        /// </summary>
        public bool Found { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodNameSearchVisitor"/> class.
        /// </summary>
        /// <param name="methodName">Method name to match.</param>
        public MethodNameSearchVisitor(string methodName)
        {
            _methodName = methodName;
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == _methodName)
            {
                Found = true;
            }

            return base.VisitMethodCall(node);
        }
    }
}
