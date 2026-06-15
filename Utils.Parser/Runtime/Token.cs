using Utils.Parser.Source;

namespace Utils.Parser.Runtime;

/// <summary>
/// An atomic lexical unit produced by <see cref="LexerEngine"/>.
/// Each token records its source position, the rule that matched it,
/// the active lexer mode at the time of the match, and the matched text.
/// </summary>
/// <param name="Span">Position and length of the token in the source text.</param>
/// <param name="RuleName">Name of the lexer rule that produced this token, for example <c>"ID"</c>.</param>
/// <param name="ModeName">Name of the active lexer mode when this token was produced.</param>
/// <param name="Channel">Channel name assigned to this token.</param>
/// <param name="Text">Raw matched text.</param>
public record Token(
    SourceSpan Span,
    string RuleName,
    string ModeName,
    string Channel,
    string Text
);
