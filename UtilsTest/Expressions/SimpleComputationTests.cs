using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates arithmetic computations compiled by <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class SimpleComputationTests
{
    /// <summary>
    /// Ensures arithmetic precedence remains consistent.
    /// </summary>
    [TestMethod]
    public void Compile_ArithmeticExpression_RespectsPrecedence()
    {
        var compiler = new CStyleExpressionCompiler();
        var expression = compiler.Compile("(10 + 2) * 3 - 6 / 2");
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();

        Assert.AreEqual(33, lambda());
    }
}
