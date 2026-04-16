using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates boolean and relational operations compiled by <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class OperationsTests
{
    /// <summary>
    /// Ensures compound logical operations return expected results.
    /// </summary>
    [TestMethod]
    public void Compile_BooleanOperations_ReturnsExpectedValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var expression = compiler.Compile("(2 < 3) && (5 >= 5)");
        var lambda = Expression.Lambda<Func<bool>>(Expression.Convert(expression, typeof(bool))).Compile();

        Assert.IsTrue(lambda());
    }
}
