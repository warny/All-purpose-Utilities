namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a concrete location in a source file.
/// </summary>
public sealed record SourceLocation(
    /// <summary>Optional path of the source file containing the location.</summary>
    string? FilePath,
    /// <summary>1-based line number.</summary>
    int Line,
    /// <summary>1-based column number.</summary>
    int Column,
    /// <summary>Zero-based absolute position from the start of the source.</summary>
    int Position);

/// <summary>
/// An absolute position and length within a source text.
/// </summary>
public sealed record SourceSpan(
    /// <summary>Zero-based character offset from the start of the source.</summary>
    int Position,
    /// <summary>Number of characters covered by this span.</summary>
    int Length,
    /// <summary>1-based line where the span starts.</summary>
    int Line = 1,
    /// <summary>1-based column where the span starts.</summary>
    int Column = 1,
    /// <summary>Optional source file path for diagnostics formatting.</summary>
    string? FilePath = null);

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
    /// <summary>Channel name assigned to this token.</summary>
    string Channel,
    /// <summary>Raw matched text.</summary>
    string Text
);
