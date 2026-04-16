using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides compiler-based regression coverage for legacy derivation parser tests.
/// </summary>
[TestClass]
public class ExpressionDerivationTests
{
    /// <summary>
    /// Ensures polynomial expressions can still be compiled for derivative workflows.
    /// </summary>
    [TestMethod]
    public void Compile_PolynomialExpression_ForDerivativeWorkflow()
    {
        var compiler = new CStyleExpressionCompiler();
        var x = Expression.Parameter(typeof(double), "x");
        var expression = compiler.Compile("x * x + 2 * x", new Dictionary<string, Expression> { ["x"] = x });
        var lambda = Expression.Lambda<Func<double, double>>(Expression.Convert(expression, typeof(double)), x).Compile();

        Assert.AreEqual(15d, lambda(3d), 1e-9);
    }
}
