using System.Globalization;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Format;

namespace UtilsTest.Objects;

/// <summary>
/// Validates injectable string-format builder behavior.
/// </summary>
[TestClass]
public class StringFormatBuilderTests
{
    /// <summary>
    /// Ensures that the builder validates each formatted expression through the injected compiler.
    /// </summary>
    [TestMethod]
    public void Create_ValidFormat_InvokesInjectedCompilerForEachFormattedPart()
    {
        var compiler = new RecordingCompiler();
        IStringFormatBuilder builder = new StringFormatBuilder(compiler);

        Func<int, int, string> formatter = builder.Create<Func<int, int, string>>("{a} + {b}", "a", "b");
        string result = formatter(2, 3);

        Assert.AreEqual("2 + 3", result);
        CollectionAssert.AreEqual(new[] { "a", "b" }, compiler.CompiledExpressions);
    }

    /// <summary>
    /// Ensures the C-style compiler can be consumed through the new compiler interface.
    /// </summary>
    [TestMethod]
    public void CSyntaxCompiler_ImplementsExpressionCompilerInterface()
    {
        IExpressionCompiler compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.Compile("1 + 2");
        Func<double> lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

        Assert.AreEqual(3d, lambda());
    }

    /// <summary>
    /// Recording test double used to track compiler invocations.
    /// </summary>
    private sealed class RecordingCompiler : IExpressionCompiler
    {
        /// <summary>
        /// Gets expressions that were validated by the builder.
        /// </summary>
        public List<string> CompiledExpressions { get; } = new();

        /// <inheritdoc />
        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
        {
            CompiledExpressions.Add(content);

            if (symbols is not null && symbols.TryGetValue(content, out Expression? symbol))
            {
                return symbol;
            }

            return Expression.Constant(0d);
        }
    }
}
