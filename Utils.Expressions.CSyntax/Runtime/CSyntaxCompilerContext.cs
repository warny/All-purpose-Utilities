using System.Linq.Expressions;

namespace Utils.Expressions.CSyntax.Runtime;

/// <summary>
/// Represents a mutable symbol context used by <see cref="CSyntaxExpressionCompiler"/>.
/// Symbols can be plain objects, delegates, or expression nodes.
/// </summary>
public sealed class CSyntaxCompilerContext
{
    /// <summary>
    /// Gets the symbol table used for source resolution.
    /// </summary>
    public IDictionary<string, object?> Symbols { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Adds or replaces a symbol in the context.
    /// </summary>
    /// <param name="name">Symbol name.</param>
    /// <param name="value">Symbol value.</param>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Symbols[name] = value;
    }

    /// <summary>
    /// Tries to resolve a symbol by name.
    /// </summary>
    /// <param name="name">Symbol name.</param>
    /// <param name="value">Resolved value.</param>
    /// <returns><c>true</c> when the symbol exists; otherwise <c>false</c>.</returns>
    public bool TryGet(string name, out object? value) => Symbols.TryGetValue(name, out value);
}
