using Utils.Expressions;

namespace Utils.Expressions.CSyntax.Runtime;

/// <summary>
/// Represents a mutable symbol context used by <see cref="CSyntaxExpressionCompiler"/>.
/// Symbols can be plain objects, delegates, or expression nodes.
/// </summary>
public sealed class CSyntaxCompilerContext : ExpressionCompilerContext
{
    /// <summary>
    /// Reads a C-syntax context from a stream.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>A populated C-syntax context.</returns>
    public static CSyntaxCompilerContext ReadFromStream(Stream stream)
    {
        ExpressionCompilerContext context = ReadFromStreamCore(stream);
        CSyntaxCompilerContext result = new();
        foreach (KeyValuePair<string, object?> symbol in context.Symbols)
        {
            result.Symbols[symbol.Key] = symbol.Value;
        }

        return result;
    }

    /// <summary>
    /// Reads a shared compiler context from a stream.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>A populated context.</returns>
    private static ExpressionCompilerContext ReadFromStreamCore(Stream stream)
    {
        return ExpressionCompilerContext.ReadFromStream(stream);
    }
}
