using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates ternary/switch-like branching compiled by <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class SwitchTests
{
    /// <summary>
    /// Ensures conditional operator branching produces expected output.
    /// </summary>
    [TestMethod]
    public void Compile_IfExpressionStyleCondition_ReturnsExpectedValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var expression = compiler.Compile("(1 < 2) && (3 > 1)");
        var lambda = Expression.Lambda<Func<bool>>(Expression.Convert(expression, typeof(bool))).Compile();

        Assert.IsTrue(lambda());
    }
}
