using System;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Resource-exhaustion limits of the S5 P9 comparison memo's strict keys: key construction and strict key
/// equality must stay bounded in work and recursion depth for hostile shapes (strongly shared DAGs and very deep
/// chains), so a memo key can never make simplification exponential or exhaust the stack.
/// </summary>
[TestClass]
public class ExpressionComparisonMemoResourceLimitTests
{
    /// <summary>Upper bound for each bounded worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Explicit worker stack size, so the recursion margin does not depend on the test host's main thread.</summary>
    private const int WorkerStackSize = 1024 * 1024;

    /// <summary>
    /// A strongly shared DAG whose per-path node count (2^41 - 1) far exceeds the key budget is ineligible instead of
    /// being traversed exponentially.
    /// </summary>
    [TestMethod]
    public void StronglySharedDag_ExceedingBudget_IsIneligible()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression shared = x;
        for (int i = 0; i < 40; i++) shared = Expression.Add(shared, shared);

        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(shared));
    }

    /// <summary>
    /// Left- and right-nested chains 100,000 levels deep are rejected by the node budget after a bounded number of
    /// recursive frames, without exhausting a 1 MB worker stack.
    /// </summary>
    [TestMethod]
    public void VeryDeepChains_AreRejectedWithinBoundedRecursion()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression leftNested = x;
        Expression rightNested = x;
        for (int i = 0; i < 100_000; i++)
        {
            leftNested = Expression.Add(leftNested, x);
            rightNested = Expression.Add(x, rightNested);
        }

        bool leftEligible = true;
        bool rightEligible = true;
        RunOnBoundedWorker(() =>
        {
            leftEligible = ExpressionSimplifier.IsComparisonMemoEligible(leftNested);
            rightEligible = ExpressionSimplifier.IsComparisonMemoEligible(rightNested);
        });

        Assert.IsFalse(leftEligible);
        Assert.IsFalse(rightEligible);
    }

    /// <summary>
    /// The deepest chain still inside the budget (1,023 nodes, 511 nested levels) is eligible, and strict key
    /// equality over two rebuilt copies recurses through every level within a 1 MB worker stack; a difference at
    /// the deepest leaf is still detected.
    /// </summary>
    [TestMethod]
    public void DeepestEligibleChain_StrictKeyEqualityStaysBounded()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = BuildChain(x, 511, 1.0);
        Expression rebuilt = BuildChain(x, 511, 1.0);
        Expression differentLeaf = BuildChain(x, 511, 2.0);
        Expression overBudget = BuildChain(x, 512, 1.0);

        bool eligible = false;
        bool equal = false;
        bool differentEqual = true;
        bool overBudgetEligible = true;
        RunOnBoundedWorker(() =>
        {
            eligible = ExpressionSimplifier.IsComparisonMemoEligible(first);
            equal = ExpressionSimplifier.ComparisonMemoKeysEqual(first, rebuilt);
            differentEqual = ExpressionSimplifier.ComparisonMemoKeysEqual(first, differentLeaf);
            overBudgetEligible = ExpressionSimplifier.IsComparisonMemoEligible(overBudget);
        });

        Assert.IsTrue(eligible);
        Assert.IsTrue(equal);
        Assert.IsFalse(differentEqual);
        Assert.IsFalse(overBudgetEligible);
    }

    /// <summary>
    /// A transformation-budget refusal raised in the middle of an eligible operand's simplification propagates on every
    /// attempt and is never cached: the memo holds no entry afterwards and the session depth is restored, so a refused
    /// simplification can neither be replayed as a result nor leave the session state corrupted.
    /// </summary>
    /// <remarks>
    /// Until S5A-04 the throwing eligible operand of this check was an integral division by zero (unit test
    /// <c>Exceptions_AreNeverCached</c>); a constant fold no longer throws, so no eligible operand, which holds only native
    /// arithmetic, raises an exception of its own while simplifying, and the refusal of a comparer with tight limits is used.
    /// </remarks>
    [TestMethod]
    public void RefusedSimplification_IsNeverCached()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression refused = Expression.Add(Expression.Multiply(Expression.Add(i, Expression.Constant(1)), Expression.Constant(2)), Expression.Add(i, Expression.Constant(3)));
        Assert.IsTrue(ExpressionSimplifier.IsComparisonMemoEligible(refused));
        ExpressionComparer tight = ExpressionComparer.Create(new ExpressionResourceLimits { MaxTransformVisits = 3 });

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => tight.GetHashCode(refused));
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => tight.GetHashCode(refused));
            Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoEntryCount);
            Assert.AreEqual(1, ExpressionSimplifier.ComparisonMemoDepth, "The session depth must be restored after a refused simplification.");
        }

        Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoDepth);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoActive);
        Assert.IsFalse(ExpressionSimplifier.HasComparisonMemo);
        Assert.IsFalse(ExpressionSimplifier.HasObservedComparisonMemo);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoRetained);
    }

    /// <summary>
    /// Builds a left-nested chain <c>((leaf + x) + x) + ...</c> of <paramref name="levels"/> additions whose
    /// deepest operand is the constant <paramref name="leaf"/>; it has <c>2 * levels + 1</c> nodes.
    /// </summary>
    /// <param name="x">The repeated parameter.</param>
    /// <param name="levels">The number of nested additions.</param>
    /// <param name="leaf">The deepest constant.</param>
    /// <returns>The chain.</returns>
    private static Expression BuildChain(ParameterExpression x, int levels, double leaf)
    {
        Expression chain = Expression.Constant(leaf);
        for (int i = 0; i < levels; i++) chain = Expression.Add(chain, x);
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
