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
        using Barrier barrier = new(2);
        int[] counts = new int[2];
        Thread[] threads = new Thread[2];
        for (int i = 0; i < threads.Length; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                using ExpressionSimplifier.ExpressionMetricsScope scope = ExpressionSimplifier.BeginExpressionMetricsScope();
                ExpressionSimplifier.GetExpressionMetrics(Expression.Add(Expression.Constant(1.0), Expression.Constant(2.0)));
                counts[index] = ExpressionSimplifier.ExpressionMetricAnalysisCount;
                Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            });
            threads[i].Start();
        }

        foreach (Thread thread in threads) Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
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

        Assert.ThrowsExactly<ArgumentNullException>(() => ExpressionComparer.SimplifyForComparison(null!));
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
        Assert.AreEqual(nodes, metrics.NodeCount);
        Assert.AreEqual(arithmeticNodes, metrics.ArithmeticNodeCount);
        Assert.AreEqual(depth, metrics.ArithmeticDepth);
    }
}
