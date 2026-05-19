using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the outcome of a single rule invocation attempt.
/// A successful result carries the matched <see cref="ParseNode"/> and the position after the match.
/// A failed result has <see cref="IsFailure"/> set to <see langword="true"/> and <see cref="Node"/> equal to <see langword="null"/>.
/// </summary>
internal readonly record struct ParserRuleResult(
    /// <summary>The parse node produced by the rule, or <see langword="null"/> if the rule failed.</summary>
    ParseNode? Node,
    /// <summary>The input position immediately after the matched content, or the start position on failure.</summary>
    int EndPosition,
    /// <summary>Whether the rule invocation failed to produce a match.</summary>
    bool IsFailure);
