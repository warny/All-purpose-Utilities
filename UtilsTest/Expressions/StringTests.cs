using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates string member access compiled by <see cref="CSyntaxExpressionCompiler"/>.
/// </summary>
[TestClass]
public class StringTests
{
    /// <summary>
    /// Ensures string length access is supported.
    /// </summary>
    [TestMethod]
    public void Compile_StringLengthExpression_ReturnsLength()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var symbols = new Dictionary<string, Expression>
        {
            ["s"] = Expression.Constant("compiler")
        };

        var expression = compiler.Compile("s.Length", symbols);
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();

        Assert.AreEqual(8, lambda());
    }
}
