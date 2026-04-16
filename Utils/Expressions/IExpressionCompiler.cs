using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Defines a lightweight contract for expression compilers that turn source text into LINQ expression trees.
/// </summary>
public interface IExpressionCompiler
{
    /// <summary>
    /// Compiles source text to an expression tree.
    /// </summary>
    /// <param name="content">Source text to compile.</param>
    /// <param name="symbols">Optional symbol table used for identifier resolution.</param>
    /// <returns>The compiled expression.</returns>
    Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null);
}
