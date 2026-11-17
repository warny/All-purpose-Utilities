using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The additive chains of quotients, <c>1 - x1/x0 - x2/x0 - … - xn/x0</c> (mission "trigonometric rule and division chains",
/// part B, <c>docs/expressions/SimplifierFlatten.md</c> §15): their cost, their results, their values, their effects and their
/// budgets.
/// </summary>
[TestClass]
public class ExpressionSimplifierDivisionChainTests
{
    /// <summary>Counts the transformation visits.</summary>
    private sealed class VisitCounter : IExpressionTransformTracer
    {
        /// <summary>Gets the number of treatments entered.</summary>
        public long Visits { get; private set; }

        /// <inheritdoc/>
        public void OnEvent(in ExpressionTransformEvent traceEvent)
        {
            if (traceEvent.Kind == ExpressionTransformEventKind.TransformEnter) Visits++;
        }
    }

    /// <summary>Builds <c>1 - x1/x0 - … - xn/x0</c>, left-associated, over <see cref="double"/> parameters.</summary>
    /// <param name="n">The number of quotients.</param>
    /// <returns>The chain.</returns>
    internal static Expression SubChain(int n)
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0");
        Expression chain = Expression.Constant(1.0);
        for (int i = 1; i <= n; i++) chain = Expression.Subtract(chain, Expression.Divide(Expression.Parameter(typeof(double), "x" + i), x0));
        return chain;
    }

    /// <summary>Simplifies <paramref name="source"/> without limits and returns its visits and comparer simplifications.</summary>
    /// <param name="source">The tree.</param>
    /// <returns>The transformation visits and the simplifications requested from the comparer.</returns>
    private static (long Visits, long ComparerSimplifications) Cost(Expression source)
    {
        var counter = new VisitCounter();
        var statistics = new SimplificationMetadataStatistics();
        ExpressionSimplifier.MetadataStatistics = statistics;
        try
        {
            using (ExpressionTransformTracing.Begin(counter))
            {
                new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(source);
            }
        }
        finally
        {
            ExpressionSimplifier.MetadataStatistics = null;
        }

        return (counter.Visits, statistics.ComparerSimplifications);
    }

    /// <summary>
    /// The historical treatment compared, at every level, the growing canonical prefix with the new quotient, and the comparer
    /// simplified that prefix again, which rebuilt it level by level: cubic visits (6,934 for 16 quotients, about 1.4 s for 64).
    /// The work is linear: no comparison of the prefix, so no simplification of it.
    /// </summary>
    [TestMethod]
    public void SubChain_WorkIsLinear()
    {
        (long visits8, long comparer8) = Cost(SubChain(8));
        (long visits32, long comparer32) = Cost(SubChain(32));
        Assert.IsTrue(visits32 <= 5 * visits8, $"visits: {visits8} for 8 quotients, {visits32} for 32");
        Assert.IsTrue(comparer32 <= 32, $"comparer simplifications: {comparer8} for 8 quotients, {comparer32} for 32");
    }
}
