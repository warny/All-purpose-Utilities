namespace Utils.Parser.Runtime;

/// <summary>
/// An absolute position and length within a source text.
/// Line/column information is computed on demand via <see cref="ToLineColumn"/>.
/// </summary>
public record SourceSpan(
    /// <summary>Zero-based character offset from the start of the source.</summary>
    int Position,
    /// <summary>Number of characters covered by this span.</summary>
    int Length
)
{
    /// <summary>
    /// Converts the absolute character offset to a 1-based (line, column) pair
    /// by scanning through <paramref name="source"/> up to <see cref="Position"/>.
    /// </summary>
    /// <param name="source">The full source text.</param>
    /// <returns>A tuple of (1-based line number, 1-based column number).</returns>
    public (int Line, int Column) ToLineColumn(string source)
    {
        int line = 1, col = 1;
        for (int i = 0; i < Position && i < source.Length; i++)
        {
            if (source[i] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }
}

/// <summary>
/// An atomic lexical unit produced by <see cref="LexerEngine"/>.
/// Each token records its source position, the rule that matched it,
/// the active lexer mode at the time of the match, and the matched text.
/// </summary>
public record Token(
    /// <summary>Position and length of the token in the source text.</summary>
    SourceSpan Span,
    /// <summary>Name of the lexer rule that produced this token (e.g. <c>"ID"</c>).</summary>
    string RuleName,
    /// <summary>Name of the active lexer mode when this token was produced.</summary>
    string ModeName,
    /// <summary>Raw matched text.</summary>
    string Text
);
