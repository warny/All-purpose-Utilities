using System.Linq.Expressions;

namespace Utils.Expressions.VBSyntax.Runtime;

/// <summary>
/// Represents a mutable symbol context used by <see cref="VBSyntaxExpressionCompiler"/>.
/// Symbols can be plain objects, delegates, or expression nodes.
/// </summary>
public sealed class VBSyntaxCompilerContext
{
    /// <summary>
    /// Gets the symbol table used for source resolution.
    /// </summary>
    public IDictionary<string, object?> Symbols { get; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds or replaces a symbol in the context.
    /// </summary>
    /// <param name="name">Symbol name.</param>
    /// <param name="value">Symbol value (object, delegate, or expression).</param>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Symbols[name] = value;
    }

    /// <summary>
    /// Sets an expression-typed symbol directly.
    /// </summary>
    /// <param name="name">Symbol name.</param>
    /// <param name="expression">Expression to bind.</param>
    public void Set(string name, Expression expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(expression);
        Symbols[name] = expression;
    }

    /// <summary>
    /// Tries to resolve a symbol by name.
    /// </summary>
    /// <param name="name">Symbol name.</param>
    /// <param name="value">Resolved symbol value when found.</param>
    /// <returns><see langword="true"/> when found; otherwise <see langword="false"/>.</returns>
    public bool TryGet(string name, out object? value) =>
        Symbols.TryGetValue(name, out value);
}
