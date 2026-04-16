using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates unary operators compiled by <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class UnaryOperatorsTests
{
    /// <summary>
    /// Ensures unary negation is compiled correctly.
    /// </summary>
    [TestMethod]
    public void Compile_UnaryNegation_ReturnsExpectedValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var expression = compiler.Compile("-(3)");
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();

        Assert.AreEqual(-3, lambda());
    }
}
