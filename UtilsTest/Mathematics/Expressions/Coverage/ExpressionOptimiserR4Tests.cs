using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R4 behavior of <see cref="ExpressionOptimiser"/>: an extension node is kept by reference, and a dynamic operation is
/// returned as is when nothing changes and still runs exactly like its source. The security invariants (no call into a hostile
/// extension node or binder) are in <c>UtilsTest.Security</c> (<c>ExpressionOptimiserR4SecurityTests</c>).
/// </summary>
[TestClass]
public class ExpressionOptimiserR4Tests
{
    /// <summary>A <see cref="double"/> parameter.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The optimised parents keep the extension node itself, by reference, when they are rewritten around it.</summary>
    [TestMethod]
    public void HostileExtension_IsKeptByReference()
    {
        var hostile = new R4HostileExtension(X);
        Assert.AreSame(hostile, new ExpressionOptimiser().Optimize(hostile));

        Expression lambda = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Multiply(X, Expression.Constant(1.0)), hostile), X);
        var optimized = (LambdaExpression)new ExpressionOptimiser().Optimize(lambda);
        var sum = (BinaryExpression)optimized.Body;
        Assert.AreSame(X, sum.Left);
        Assert.AreSame(hostile, sum.Right);
    }

    /// <summary>An unchanged dynamic operation is returned by reference and its compiled form runs exactly like the source.</summary>
    [TestMethod]
    public void UnchangedDynamicOperation_IsReturnedAsIs()
    {
        var binder = new R4CountingBinder("sum");
        Expression dynamic = Expression.MakeDynamic(typeof(Func<CallSite, double, double, object>), binder, X, X);
        Assert.AreSame(dynamic, new ExpressionOptimiser().Optimize(dynamic));

        var tree = Expression.Lambda<Func<double, object>>(dynamic, X);
        var optimized = (Expression<Func<double, object>>)new ExpressionOptimiser().Optimize(tree);
        Assert.AreSame(tree, optimized);
        R4DynamicRuntime.Reset();
        Assert.AreEqual(4.0, optimized.Compile()(2.0));
        CollectionAssert.AreEqual(new[] { "sum:4" }, R4DynamicRuntime.Events);
    }
}
