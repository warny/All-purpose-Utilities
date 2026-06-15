using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the outcome of a single rule invocation attempt.
/// A successful result carries the matched <see cref="ParseNode"/>, the position after the match,
/// and the opaque parser execution-state snapshot observed after the invocation completed.
/// A failed result has <see cref="IsFailure"/> set to <see langword="true"/> and <see cref="Node"/> equal to <see langword="null"/>.
/// The snapshot is restore transport for memoization hits only; it is not action replay authority and does not provide action buffering.
/// </summary>
internal readonly record struct ParserRuleResult(
    /// <summary>The parse node produced by the rule, or <see langword="null"/> if the rule failed.</summary>
    ParseNode? Node,
    /// <summary>The input position immediately after the matched content, or the start position on failure.</summary>
    int EndPosition,
    /// <summary>Whether the rule invocation failed to produce a match.</summary>
    bool IsFailure,
    /// <summary>
    /// Opaque parser execution-state snapshot captured after the rule invocation completed, including any <c>@after</c> lifecycle hook effects.
    /// On memoization hits, the stored snapshot is restored so that lifecycle hooks are not replayed and the caller sees the same semantic state as the original invocation.
    /// <see langword="null"/> when state management is not active.
    /// </summary>
    object? ExecutionStateSnapshot = null);
