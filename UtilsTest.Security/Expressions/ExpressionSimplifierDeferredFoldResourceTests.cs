using System;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// S5A-04 (mission "deferred arithmetic exceptions"): the handler that makes a throwing constant fold decline catches only the
/// arithmetic exceptions of the fold itself, so the resource-exhaustion limits keep refusing a tree that holds such a fold: the
/// transformation budget still raises <see cref="ExpressionResourceLimitException"/>, and on a small stack the execution-stack
/// guard still decides, never swallowed or turned into a declined fold.
/// </summary>
[TestClass]
public class ExpressionSimplifierDeferredFoldResourceTests
{
    /// <summary>Upper bound for the bounded worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>The small worker stack of the stack-guard test.</summary>
    private const int SmallStackSize = 256 * 1024;

    /// <summary>
    /// A tree whose simplification exceeds the transformation budget raises the budget refusal, whether or not it holds a throwing
    /// constant fold (<c>1 / 0</c> at its deepest operand).
    /// </summary>
    [TestMethod]
    public void TransformationBudget_IsStillEnforcedAroundAThrowingFold()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        foreach (Expression seed in new Expression[] { Expression.Divide(Expression.Constant(1), Expression.Constant(0)), Expression.Constant(1) })
        {
            Expression large = seed;
            for (int k = 0; k < 20; k++) large = Expression.Add(Expression.Multiply(large, Expression.Constant(1)), i);
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(new ExpressionResourceLimits { MaxTransformVisits = 5 }).Simplify(large));
        }
    }

    /// <summary>
    /// On a 256 KB worker stack, simplifying a conditional that holds <c>int.MinValue / -1</c> either completes with the result of
    /// the main thread or ends with the controlled <see cref="InsufficientExecutionStackException"/> of the stack guard: no other
    /// exception, and never a swallowed one.
    /// </summary>
    [TestMethod]
    public void SmallStack_KeepsTheStackGuardAroundAThrowingFold()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression Build() => Expression.Lambda(Expression.Condition(Expression.GreaterThan(i, Expression.Constant(0)), Expression.Add(i, Expression.Divide(Expression.Constant(int.MinValue), Expression.Constant(-1))), Expression.Constant(2)), i);
        string expected = ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(Build()));

        Exception? failure = null;
        string? limited = null;
        var worker = new Thread(() =>
        {
            try
            {
                limited = ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(Build()));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, SmallStackSize);
        worker.Start();
        Assert.IsTrue(worker.Join(Timeout), "The bounded worker did not finish in time.");
        Assert.IsTrue(failure is null || failure is InsufficientExecutionStackException, failure?.ToString());
        if (failure is null) Assert.AreEqual(expected, limited);
    }
}
