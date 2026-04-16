using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates function-call compilation with <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class FunctionCallTests
{
    /// <summary>
    /// Ensures context delegates can be invoked from compiled source.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCall_InvokesContextDelegate()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("sum", (Func<int, int, int>)((a, b) => a + b));

        var expression = compiler.Compile("sum(4, 7)", context);
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();

        Assert.AreEqual(11, lambda());
    }
}
