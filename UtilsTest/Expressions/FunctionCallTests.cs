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

    /// <summary>
    /// Ensures delegate invocation supports array arguments.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCall_WithArrayArgument_ReturnsExpectedValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("values", new[] { 1, 2, 3 });
        context.Set("concatInt", (Func<int[], string>)(values => string.Concat(values)));

        var expression = compiler.Compile("concatInt(values)", context);
        var lambda = Expression.Lambda<Func<string>>(Expression.Convert(expression, typeof(string))).Compile();

        Assert.AreEqual("123", lambda());
    }

    /// <summary>
    /// Ensures delegate invocation supports function arguments.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCall_WithLambdaArgument_ReturnsExpectedValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("apply", (Func<Func<string, string>, string, string>)((f, s) => f(s)));
        context.Set("toUpper", (Func<string, string>)(s => s.ToUpperInvariant()));
        context.Set("text", "aBc");

        var expression = compiler.Compile("apply(toUpper, text)", context);
        var lambda = Expression.Lambda<Func<string>>(Expression.Convert(expression, typeof(string))).Compile();

        Assert.AreEqual("ABC", lambda());
    }

    /// <summary>
    /// Ensures a method declared in source can be invoked in the same source block.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionDeclarationThenCall_Compiles()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();

        var expression = compiler.CompileSource(
            """
            public int add(int a, int b) { a + b; }
            add(5, 8)
            """,
            context);
        Assert.IsNotNull(expression);
    }
}
