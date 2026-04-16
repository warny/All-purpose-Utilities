using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Validates simple arithmetic parsing with <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class SimpleExpressionParserTest
{
    /// <summary>
    /// Ensures two-variable arithmetic expressions compile and execute.
    /// </summary>
    [TestMethod]
    public void Compile_BinaryExpression_ReturnsExpectedValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var x = Expression.Parameter(typeof(double), "x");
        var y = Expression.Parameter(typeof(double), "y");
        var symbols = new Dictionary<string, Expression> { ["x"] = x, ["y"] = y };
        var expression = compiler.Compile("x + y", symbols);
        var lambda = Expression.Lambda<Func<double, double, double>>(Expression.Convert(expression, typeof(double)), x, y).Compile();

        Assert.AreEqual(7d, lambda(3d, 4d), 1e-9);
    }
}
