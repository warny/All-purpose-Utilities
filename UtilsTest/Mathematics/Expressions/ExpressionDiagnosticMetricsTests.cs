using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>Validates the temporary P8 structural and recursion diagnostics.</summary>
[TestClass]
public class ExpressionDiagnosticMetricsTests
{
    /// <summary>Returns the sum of two values for a custom-operator expression.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The sum.</returns>
    private static double CustomAdd(double left, double right) => left + right;

    /// <summary>Validates that ordinary simplification leaves all diagnostic state inactive.</summary>
    [TestMethod]
    public void Diagnostics_Inactive_NormalSimplificationDoesNotCreateMetricState()
    {
        Assert.IsFalse(ExpressionComparer.IsDiagnosticCaptureActive);
        Assert.IsFalse(ExpressionSimplifier.HasExpressionMetricsCache);

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        new ExpressionSimplifier().Simplify(Expression.Add(x, Expression.Constant(1.0)));
        ExpressionComparer.Default.Equals(Expression.Add(x, Expression.Constant(1.0)), Expression.Add(x, Expression.Constant(2.0)));

        Assert.IsFalse(ExpressionComparer.IsDiagnosticCaptureActive);
        Assert.IsFalse(ExpressionSimplifier.HasExpressionMetricsCache);
        Assert.AreEqual(0, ExpressionSimplifier.ExpressionMetricAnalysisCount);
        Assert.AreEqual(0, ExpressionComparer.ComparisonSimplificationDepth);
    }

    /// <summary>Validates the required ordinary-arithmetic examples and method-call chain break.</summary>
    [TestMethod]
    public void Metrics_OrdinaryExamples_HaveSpecifiedValues()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        AssertMetrics(x, 1, 0, 0);
        AssertMetrics(Expression.Add(x, y), 3, 1, 1);
        AssertMetrics(Expression.Multiply(Expression.Add(x, y), z), 5, 2, 2);

        MethodCallExpression sin = Expression.Call(typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!, Expression.Add(x, y));
        AssertMetrics(sin, 4, 1, 0);
    }

    /// <summary>Validates that custom and lifted arithmetic do not extend ordinary arithmetic depth.</summary>
    [TestMethod]
    public void Metrics_CustomAndLiftedArithmetic_BreakChains()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        MethodInfo method = typeof(ExpressionDiagnosticMetricsTests).GetMethod(nameof(CustomAdd), BindingFlags.Static | BindingFlags.NonPublic)!;
        AssertMetrics(Expression.Add(x, y, method), 3, 0, 0);

        ParameterExpression nullableX = Expression.Parameter(typeof(double?), "x");
        ParameterExpression nullableY = Expression.Parameter(typeof(double?), "y");
        AssertMetrics(Expression.Add(nullableX, nullableY), 3, 0, 0);
    }

    /// <summary>
    /// Validates that floating-point complexity magnitudes grow safely beyond integer range on a shared DAG.
    /// </summary>
    [TestMethod]
    public void Metrics_StronglySharedDag_RemainsPositiveAndThresholdComparable()
    {
        Expression shallow = BuildSharedAdditionDag(12);
        Expression deep = BuildSharedAdditionDag(40);

        using ExpressionSimplifier.ExpressionMetricsScope scope = ExpressionSimplifier.BeginExpressionMetricsScope();
        ExpressionSimplifier.ExpressionMetrics shallowMetrics = ExpressionSimplifier.GetExpressionMetrics(shallow);
        ExpressionSimplifier.ExpressionMetrics deepMetrics = ExpressionSimplifier.GetExpressionMetrics(deep);

        Assert.IsGreaterThan(0f, shallowMetrics.NodeCount);
        Assert.IsGreaterThan(0f, shallowMetrics.ArithmeticNodeCount);
        Assert.IsGreaterThan(shallowMetrics.NodeCount, deepMetrics.NodeCount);
        Assert.IsGreaterThan(shallowMetrics.ArithmeticNodeCount, deepMetrics.ArithmeticNodeCount);
        Assert.IsGreaterThan(1_000_000f, deepMetrics.NodeCount);
        Assert.IsGreaterThan(1_000_000f, deepMetrics.ArithmeticNodeCount);
        Assert.IsGreaterThan((float)int.MaxValue, deepMetrics.NodeCount);
        Assert.AreEqual(40, deepMetrics.ArithmeticDepth);
    }

    /// <summary>Validates that cumulative budget remains usable for threshold checks on shared structure.</summary>
    [TestMethod]
    public void CumulativeNodeBudget_SharedDag_RemainsPositiveAndThresholdComparable()
    {
        ExpressionComparer.BeginDiagnosticCapture();
        try
        {
            ExpressionComparer.SimplifyForComparison(BuildSharedAdditionDag(13));
            var events = ExpressionComparer.EndDiagnosticCapture();
            Assert.IsNotEmpty(events);
            Assert.IsGreaterThan(0f, events[0].CumulativeNodeBudget);
            Assert.IsGreaterThan(8_192f, events[0].CumulativeNodeBudget);
        }
        finally
        {
            if (ExpressionComparer.IsDiagnosticCaptureActive) ExpressionComparer.EndDiagnosticCapture();
        }
    }

    /// <summary>Validates reference identity, nested reuse, and outermost cache teardown.</summary>
    [TestMethod]
    public void MetricCache_UsesReferenceIdentityAndOutermostLifetime()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        BinaryExpression first = Expression.Add(x, Expression.Constant(1.0));
        BinaryExpression rebuilt = Expression.Add(x, Expression.Constant(1.0));
        using (ExpressionSimplifier.ExpressionMetricsScope outer = ExpressionSimplifier.BeginExpressionMetricsScope())
        {
            ExpressionSimplifier.GetExpressionMetrics(first);
            int afterFirst = ExpressionSimplifier.ExpressionMetricAnalysisCount;
            ExpressionSimplifier.GetExpressionMetrics(first);
            Assert.AreEqual(afterFirst, ExpressionSimplifier.ExpressionMetricAnalysisCount);

            using (ExpressionSimplifier.ExpressionMetricsScope nested = ExpressionSimplifier.BeginExpressionMetricsScope())
            {
                ExpressionSimplifier.GetExpressionMetrics(first);
                Assert.AreEqual(afterFirst, ExpressionSimplifier.ExpressionMetricAnalysisCount);
                ExpressionSimplifier.GetExpressionMetrics(rebuilt);
                Assert.IsTrue(ExpressionSimplifier.ExpressionMetricAnalysisCount > afterFirst);
            }

            Assert.IsTrue(ExpressionSimplifier.HasExpressionMetricsCache);
        }

        Assert.IsFalse(ExpressionSimplifier.HasExpressionMetricsCache);
    }

    /// <summary>Validates that two threads own independent metric caches using bounded synchronization.</summary>
    [TestMethod]
    public void MetricCache_TwoThreads_AreIndependent()
    {
        BinaryExpression shared = Expression.Add(Expression.Constant(1.0), Expression.Constant(2.0));
        using Barrier barrier = new(2);
        int[] counts = new int[2];
        Exception?[] exceptions = new Exception?[2];
        Thread[] threads = new Thread[2];
        for (int i = 0; i < threads.Length; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    using ExpressionSimplifier.ExpressionMetricsScope scope = ExpressionSimplifier.BeginExpressionMetricsScope();
                    if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Worker scopes did not overlap.");
                    ExpressionSimplifier.GetExpressionMetrics(shared);
                    counts[index] = ExpressionSimplifier.ExpressionMetricAnalysisCount;
                    if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Workers did not finish their first analysis together.");
                    ExpressionSimplifier.GetExpressionMetrics(shared);
                    if (ExpressionSimplifier.ExpressionMetricAnalysisCount != counts[index])
                    {
                        throw new InvalidOperationException("The thread-local cache did not reuse the shared reference.");
                    }
                }
                catch (Exception exception)
                {
                    exceptions[index] = exception;
                }
            });
            threads[i].Start();
        }

        foreach (Thread thread in threads) Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        foreach (Exception? exception in exceptions)
        {
            if (exception is not null) Assert.Fail(exception.ToString());
        }

        CollectionAssert.AreEqual(new[] { 3, 3 }, counts);
    }

    /// <summary>Validates nested comparison depth and cleanup after normal and exceptional completion.</summary>
    [TestMethod]
    public void ComparisonDepth_IsNestedAndNeverLeaks()
    {
        ExpressionComparer.BeginDiagnosticCapture();
        ParameterExpression[] parameters = Enumerable.Range(0, 4)
            .Select(index => Expression.Parameter(typeof(double), $"p{index}"))
            .ToArray();
        Expression source = Expression.Multiply(Expression.Constant(2.0), parameters[0]);
        for (int i = 1; i < parameters.Length; i++)
        {
            source = Expression.Add(source, Expression.Multiply(Expression.Constant((double)i + 2), parameters[i]));
        }

        new ExpressionSimplifier().Simplify(source);
        var events = ExpressionComparer.EndDiagnosticCapture();
        Assert.IsTrue(events.Any(item => item.ComparisonDepth == 1));
        Assert.IsTrue(events.Any(item => item.ComparisonDepth >= 2));
        Assert.AreEqual(0, ExpressionComparer.ComparisonSimplificationDepth);

        ExpressionComparer.BeginDiagnosticCapture();
        Assert.ThrowsExactly<ArgumentNullException>(() => ExpressionComparer.SimplifyForComparison(null!));
        ExpressionComparer.EndDiagnosticCapture();
        Assert.AreEqual(0, ExpressionComparer.ComparisonSimplificationDepth);
    }

    /// <summary>Asserts structural metrics for one expression in an explicit cache lifetime.</summary>
    /// <param name="expression">The expression to measure.</param>
    /// <param name="nodes">The expected node count.</param>
    /// <param name="arithmeticNodes">The expected arithmetic-node count.</param>
    /// <param name="depth">The expected arithmetic depth.</param>
    private static void AssertMetrics(Expression expression, int nodes, int arithmeticNodes, int depth)
    {
        using ExpressionSimplifier.ExpressionMetricsScope scope = ExpressionSimplifier.BeginExpressionMetricsScope();
        ExpressionSimplifier.ExpressionMetrics metrics = ExpressionSimplifier.GetExpressionMetrics(expression);
        Assert.AreEqual((float)nodes, metrics.NodeCount);
        Assert.AreEqual((float)arithmeticNodes, metrics.ArithmeticNodeCount);
        Assert.AreEqual(depth, metrics.ArithmeticDepth);
    }

    /// <summary>Builds a logical binary tree whose two branches share the same object at every level.</summary>
    /// <param name="depth">The exact number of ordinary addition levels.</param>
    /// <returns>The root of the strongly shared expression DAG.</returns>
    private static Expression BuildSharedAdditionDag(int depth)
    {
        Expression node = Expression.Parameter(typeof(double), "x");
        for (int i = 0; i < depth; i++)
        {
            node = Expression.Add(node, node);
        }

        return node;
    }
}
