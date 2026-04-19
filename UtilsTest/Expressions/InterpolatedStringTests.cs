using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates interpolated-string compilation with <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class InterpolatedStringTests
{
    CStyleExpressionCompiler compiler = new CStyleExpressionCompiler();
    [TestMethod]
    public void SimpleInterpolation()
    {
        var expr = compiler.Compile<Func<string, string, string>>("(a, b) => $\"{a} {b}!\"");
        var func = expr.Compile();
        Assert.AreEqual("hello world!", func("hello", "world"));
    }


    /// <summary>
    /// Ensures interpolated strings are compiled and concatenated correctly.
    /// </summary>
    [TestMethod]
    public void Compile_StringLiteral_ReturnsValue()
    {
        var expression = compiler.Compile("\"Hello World!\"");
        var lambda = Expression.Lambda<Func<string>>(Expression.Convert(expression, typeof(string))).Compile();

        Assert.AreEqual("Hello World!", lambda());
    }
}
