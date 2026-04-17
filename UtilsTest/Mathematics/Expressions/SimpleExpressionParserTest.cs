using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Validates simple arithmetic parsing with <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class SimpleExpressionParserTest
{
    /// <summary>
    /// Ensures two-variable arithmetic expressions compile and execute.
    /// </summary>
    [TestMethod]
    public void Compile_BinaryExpression_ReturnsExpectedValue()
    {
        var compiler = new CStyleExpressionCompiler();
        var x = Expression.Parameter(typeof(double), "x");
        var y = Expression.Parameter(typeof(double), "y");
        var symbols = new Dictionary<string, Expression> { ["x"] = x, ["y"] = y };

        var expression = CompileExpression(compiler, "x + y", symbols);
        var lambda = Expression.Lambda<Func<double, double, double>>(Expression.Convert(expression, typeof(double)), x, y).Compile();

        Assert.AreEqual(7d, lambda(3d, 4d), 1e-9);
    }

    /// <summary>
    /// Compiles an expression while keeping compatibility with compiler overload differences across versions.
    /// </summary>
    /// <param name="compiler">Compiler instance used to parse and compile the source text.</param>
    /// <param name="source">Source expression to compile.</param>
    /// <param name="symbols">Symbol table used to bind identifiers.</param>
    /// <returns>The compiled expression tree.</returns>
    private static Expression CompileExpression(CStyleExpressionCompiler compiler, string source, IReadOnlyDictionary<string, Expression> symbols)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(symbols);

        MethodInfo[] methods = typeof(CStyleExpressionCompiler).GetMethods(BindingFlags.Instance | BindingFlags.Public);

        MethodInfo? compileWithSymbols = methods.FirstOrDefault(
            m =>
                m.Name == nameof(CStyleExpressionCompiler.Compile) &&
                m.GetParameters() is var parameters &&
                parameters.Length == 2 &&
                parameters[0].ParameterType == typeof(string) &&
                typeof(IReadOnlyDictionary<string, Expression>).IsAssignableFrom(parameters[1].ParameterType));

        if (compileWithSymbols is not null)
        {
            return (Expression)compileWithSymbols.Invoke(compiler, [source, symbols])!;
        }

        MethodInfo? compileWithContext = methods.FirstOrDefault(
            m =>
                m.Name == nameof(CStyleExpressionCompiler.Compile) &&
                m.GetParameters() is var parameters &&
                parameters.Length == 2 &&
                parameters[0].ParameterType == typeof(string) &&
                parameters[1].ParameterType == typeof(CStyleCompilerContext));

        if (compileWithContext is not null)
        {
            var context = new CStyleCompilerContext();
            foreach ((string key, Expression value) in symbols)
            {
                context.Set(key, value);
            }

            return (Expression)compileWithContext.Invoke(compiler, [source, context])!;
        }

        throw new AssertFailedException("No compatible Compile overload was found on CStyleExpressionCompiler.");
    }
}
