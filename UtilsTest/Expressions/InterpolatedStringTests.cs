using System.Linq.Expressions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates interpolated-string compilation with <see cref="CSyntaxExpressionCompiler"/>.
/// </summary>
[TestClass]
public class InterpolatedStringTests
{
    CSyntaxExpressionCompiler compiler = new CSyntaxExpressionCompiler();
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

    /// <summary>
    /// Ensures method overload resolution prioritizes interpolated string handlers when available.
    /// </summary>
    [TestMethod]
    public void MethodOverloadPrefersInterpolatedStringHandler()
    {
        ParameterExpression value = Expression.Parameter(typeof(int), "value");
        CSyntaxCompilerContext context = new();
        context.Set("value", value);
        context.Set("Choose", typeof(InterpolatedHandlerTarget).GetMethods().Where(static method => method.Name == nameof(InterpolatedHandlerTarget.Choose)).ToArray());

        Expression body = compiler.Compile("Choose($\"Value={value}\")", context);
        var function = Expression.Lambda<Func<int, string>>(Expression.Convert(body, typeof(string)), value).Compile();

        Assert.AreEqual("handler:Value=5", function(5));
    }

    /// <summary>
    /// Ensures constructor overload resolution prioritizes interpolated string handlers when available.
    /// </summary>
    [TestMethod]
    public void ConstructorOverloadPrefersInterpolatedStringHandler()
    {
        var expression = compiler.Compile<Func<int, object>>("(value) => new UtilsTest.Expressions.InterpolatedHandlerCtorTarget($\"Ctor={value}\")");
        var function = expression.Compile();
        var result = function(7);

        Assert.IsInstanceOfType<InterpolatedHandlerCtorTarget>(result);
        Assert.AreEqual("handler:Ctor=7", ((InterpolatedHandlerCtorTarget)result).Captured);
    }
}

/// <summary>
/// Test target exposing overloads with plain strings and interpolated string handlers.
/// </summary>
public static class InterpolatedHandlerTarget
{
    /// <summary>
    /// Chooses the string overload.
    /// </summary>
    /// <param name="value">Plain string input.</param>
    /// <returns>Tagged payload indicating the selected overload.</returns>
    public static string Choose(string value) => "string:" + value;

    /// <summary>
    /// Chooses the handler overload.
    /// </summary>
    /// <param name="handler">Interpolated string handler payload.</param>
    /// <returns>Tagged payload indicating the selected overload.</returns>
    public static string Choose(TestInterpolatedStringHandler handler) => "handler:" + handler.ToString();
}

/// <summary>
/// Constructor target exposing overloads with plain strings and interpolated string handlers.
/// </summary>
public sealed class InterpolatedHandlerCtorTarget
{
    /// <summary>
    /// Initializes a new instance using a plain string.
    /// </summary>
    /// <param name="value">Plain string payload.</param>
    public InterpolatedHandlerCtorTarget(string value)
    {
        Captured = "string:" + value;
    }

    /// <summary>
    /// Initializes a new instance using an interpolated string handler.
    /// </summary>
    /// <param name="handler">Interpolated handler payload.</param>
    public InterpolatedHandlerCtorTarget(TestInterpolatedStringHandler handler)
    {
        Captured = "handler:" + handler.ToString();
    }

    /// <summary>
    /// Gets the captured overload payload.
    /// </summary>
    public string Captured { get; }
}

/// <summary>
/// Lightweight interpolated string handler used by overload-priority tests.
/// </summary>
[InterpolatedStringHandler]
public struct TestInterpolatedStringHandler
{
    private StringBuilder _builder;

    /// <summary>
    /// Initializes a new handler instance.
    /// </summary>
    /// <param name="literalLength">Total literal character count.</param>
    /// <param name="formattedCount">Number of formatted placeholders.</param>
    public TestInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        _builder = new StringBuilder(literalLength + (formattedCount * 8));
    }

    /// <summary>
    /// Appends a literal segment.
    /// </summary>
    /// <param name="value">Literal content.</param>
    public void AppendLiteral(string value)
    {
        _builder.Append(value);
    }

    /// <summary>
    /// Appends a formatted value.
    /// </summary>
    /// <typeparam name="T">Formatted value type.</typeparam>
    /// <param name="value">Formatted value.</param>
    public void AppendFormatted<T>(T value)
    {
        _builder.Append(value);
    }

    /// <summary>
    /// Returns the rendered interpolated payload.
    /// </summary>
    /// <returns>Rendered content.</returns>
    public override string ToString() => _builder.ToString();
}
