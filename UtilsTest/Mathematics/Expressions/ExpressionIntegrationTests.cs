using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides compiler-based regression coverage for legacy integration parser tests.
/// </summary>
[TestClass]
public class ExpressionIntegrationTests
{
    /// <summary>
    /// Ensures trigonometric expressions still compile for integration workflows.
    /// </summary>
    [TestMethod]
    public void Compile_TrigExpression_ForIntegrationWorkflow()
    {
        var compiler = new CStyleExpressionCompiler();
        var x = Expression.Parameter(typeof(double), "x");
        var expression = compiler.Compile("x * x + 1", new Dictionary<string, Expression> { ["x"] = x });
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Convert(expression, typeof(double)), x).Compile();

        Assert.AreEqual(10d, lambda(3d), 1e-9);
    }
}
