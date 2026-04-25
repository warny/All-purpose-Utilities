using Utils.Parser.Runtime;

namespace Utils.Expressions.VBSyntax.Runtime;

/// <summary>
/// Represents a VB-like token produced by <see cref="VBSyntaxTokenParser"/>.
/// </summary>
/// <param name="RuleName">Grammar lexer rule name.</param>
/// <param name="Text">Raw token text.</param>
/// <param name="Position">Zero-based source position.</param>
/// <param name="Length">Token length in characters.</param>
public sealed record VBSyntaxToken(string RuleName, string Text, int Position, int Length);

/// <summary>
/// Provides tokenization and parsing support for VB-like expressions
/// using the <c>Utils.Parser</c> infrastructure.
/// </summary>
public sealed class VBSyntaxTokenParser
{
    private static readonly Lazy<CompiledGrammar> SharedGrammar =
        new(() => VBSyntaxTokenizerGrammar.Grammar, isThreadSafe: true);

    /// <summary>
    /// Tokenizes VB-like source code and returns the raw token stream.
    /// </summary>
    /// <param name="content">Source code to tokenize.</param>
    /// <returns>Read-only list of tokens in source order.</returns>
    public IReadOnlyList<VBSyntaxToken> Tokenize(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return SharedGrammar.Value.Tokenize(content)
            .Select(t => new VBSyntaxToken(t.RuleName, t.Text, t.Span.Position, t.Span.Length))
            .ToList();
    }

    /// <summary>
    /// Parses VB-like source text and returns a parse tree rooted on the grammar start rule.
    /// </summary>
    /// <param name="content">Source text to parse.</param>
    /// <returns>Parse tree root node.</returns>
    public ParseNode Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return SharedGrammar.Value.Parse(content);
    }
}
