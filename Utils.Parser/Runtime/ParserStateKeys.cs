namespace Utils.Parser.Runtime;

/// <summary>
/// Identifies a complete rule invocation for memoization lookup.
/// Two invocations with the same rule name, origin position, and minimum precedence
/// share a memoized result in <see cref="ParserStateRegistry"/>.
/// </summary>
internal readonly record struct RuleInvocationKey(
    /// <summary>The name of the grammar rule being invoked.</summary>
    string RuleName,
    /// <summary>The input position at which the invocation begins.</summary>
    int OriginPosition,
    /// <summary>The minimum precedence level required for this invocation.</summary>
    int MinimumPrecedence);

/// <summary>
/// Identifies a continuation point within a caller rule for metadata purposes.
/// Continuations are descriptive metadata only — they carry no execution authority
/// and are never replayed or resumed by the runtime.
/// </summary>
internal readonly record struct ContinuationKey(
    /// <summary>The name of the rule that issued the sub-rule call.</summary>
    string CallerRuleName,
    /// <summary>The index of the alternative within the caller rule.</summary>
    int CallerAlternativeIndex,
    /// <summary>The index of the element within the caller alternative at the call site.</summary>
    int CallerElementIndex,
    /// <summary>The input position at which the caller would resume after the sub-rule completes.</summary>
    int ResumePosition,
    /// <summary>The minimum precedence level active at the continuation point.</summary>
    int MinimumPrecedence);

/// <summary>
/// Identifies a specific parser execution state for visited-state tracking.
/// Used by <see cref="ParserStateRegistry"/> to detect cycles and avoid redundant exploration.
/// </summary>
internal readonly record struct ParserStateKey(
    /// <summary>The name of the grammar rule at this state.</summary>
    string RuleName,
    /// <summary>The current input position.</summary>
    int InputPosition,
    /// <summary>The index of the alternative being explored.</summary>
    int AlternativeIndex,
    /// <summary>The index of the element within the alternative currently being processed.</summary>
    int ElementIndex,
    /// <summary>The minimum precedence level active at this state.</summary>
    int MinimumPrecedence);
