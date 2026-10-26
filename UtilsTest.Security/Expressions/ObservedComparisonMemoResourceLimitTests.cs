using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Resource-exhaustion limits of the S5 P15 observed comparison memo's strict keys: key construction and strict key
/// equality must stay bounded in work and recursion depth for hostile shapes (strongly shared DAGs, very deep chains,
/// very wide calls), exactly like the P9 keys, so an observed key can never make simplification exponential or
/// exhaust the stack.
/// </summary>
[TestClass]
public class ObservedComparisonMemoResourceLimitTests
{
    /// <summary>Upper bound for each bounded worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Explicit worker stack size, so the recursion margin does not depend on the test host's main thread.</summary>
    private const int WorkerStackSize = 1024 * 1024;

    /// <summary>The <see cref="Math.Sin(double)"/> method, a call node the observed tier accepts.</summary>
    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;

    /// <summary>The <see cref="Math.Max(double, double)"/> method, a two-argument call node.</summary>
    private static readonly MethodInfo Max = typeof(Math).GetMethod(nameof(Math.Max), [typeof(double), typeof(double)])!;

    /// <summary>
    /// A strongly shared DAG of calls whose per-path node count far exceeds the key budget is ineligible instead of
    /// being traversed exponentially.
    /// </summary>
    [TestMethod]
    public void StronglySharedCallDag_ExceedingBudget_IsIneligible()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression shared = Expression.Call(Sin, x);
        for (int i = 0; i < 40; i++) shared = Expression.Call(Max, shared, shared);

        Assert.IsFalse(ExpressionSimplifier.IsObservedComparisonMemoEligible(shared));
    }

    /// <summary>
    /// Chains of calls and additions 100,000 levels deep are rejected by the node budget after a bounded number of
    /// recursive frames, without exhausting a 1 MB worker stack.
    /// </summary>
    [TestMethod]
    public void VeryDeepCallChains_AreRejectedWithinBoundedRecursion()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression nestedCalls = x;
        Expression nestedSums = Expression.Call(Sin, x);
        for (int i = 0; i < 100_000; i++)
        {
            nestedCalls = Expression.Call(Sin, nestedCalls);
            nestedSums = Expression.Add(x, nestedSums);
        }

        bool callsEligible = true;
        bool sumsEligible = true;
        RunOnBoundedWorker(() =>
        {
            callsEligible = ExpressionSimplifier.IsObservedComparisonMemoEligible(nestedCalls);
            sumsEligible = ExpressionSimplifier.IsObservedComparisonMemoEligible(nestedSums);
        });

        Assert.IsFalse(callsEligible);
        Assert.IsFalse(sumsEligible);
    }

    /// <summary>
    /// The deepest call chain still inside the budget (1,024 nodes) is eligible, and strict key equality over two
    /// rebuilt copies recurses through every level within a 1 MB worker stack; a difference at the deepest leaf is
    /// still detected, and one more level is rejected.
    /// </summary>
    [TestMethod]
    public void DeepestEligibleCallChain_StrictKeyEqualityStaysBounded()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = BuildCalls(Expression.Constant(1.0), 1023);
        Expression rebuilt = BuildCalls(Expression.Constant(1.0), 1023);
        Expression differentLeaf = BuildCalls(Expression.Constant(2.0), 1023);
        Expression overBudget = BuildCalls(x, 1024);

        bool eligible = false;
        bool equal = false;
        bool differentEqual = true;
        bool overBudgetEligible = true;
        RunOnBoundedWorker(() =>
        {
            eligible = ExpressionSimplifier.IsObservedComparisonMemoEligible(first);
            equal = ExpressionSimplifier.ObservedComparisonMemoKeysEqual(first, rebuilt);
            differentEqual = ExpressionSimplifier.ObservedComparisonMemoKeysEqual(first, differentLeaf);
            overBudgetEligible = ExpressionSimplifier.IsObservedComparisonMemoEligible(overBudget);
        });

        Assert.IsTrue(eligible);
        Assert.IsTrue(equal);
        Assert.IsFalse(differentEqual);
        Assert.IsFalse(overBudgetEligible);
    }

    /// <summary>Wraps <paramref name="leaf"/> in <paramref name="levels"/> nested <see cref="Math.Sin(double)"/> calls.</summary>
    /// <param name="leaf">The deepest operand.</param>
    /// <param name="levels">The number of nested calls.</param>
    /// <returns>The call chain, of <c>levels + 1</c> nodes.</returns>
    private static Expression BuildCalls(Expression leaf, int levels)
    {
        Expression chain = leaf;
        for (int i = 0; i < levels; i++) chain = Expression.Call(Sin, chain);
        return chain;
    }

    /// <summary>Runs <paramref name="action"/> on a dedicated thread with an explicit stack size and a bounded join.</summary>
    /// <param name="action">The action to run.</param>
    private static void RunOnBoundedWorker(Action action)
    {
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, WorkerStackSize);

        worker.Start();
        Assert.IsTrue(worker.Join(Timeout), "The bounded worker did not finish in time.");
        Assert.IsNull(failure, failure?.ToString());
    }
}
