using System.Linq.Expressions;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates block-style expressions compiled by <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class BlockTests
{
    /// <summary>
    /// Ensures statement blocks return the last expression value.
    /// </summary>
    [TestMethod]
    public void Compile_BlockExpression_ReturnsLastValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var x = Expression.Variable(typeof(int), "x");
        var expression = compiler.Compile("{ x = 2; x + 3; }", new Dictionary<string, Expression> { ["x"] = x });
        var lambda = Expression.Lambda<Func<int>>(Expression.Block([x], Expression.Convert(expression, typeof(int)))).Compile();

        Assert.AreEqual(5, lambda());
    }
}
