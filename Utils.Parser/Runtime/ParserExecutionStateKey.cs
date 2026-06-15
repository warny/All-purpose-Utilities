namespace Utils.Parser.Runtime;

/// <summary>
/// Identifies the semantic parser execution state that can affect rule-result memoization.
/// </summary>
/// <param name="Value">Opaque deterministic state value supplied by an execution-state manager.</param>
public readonly record struct ParserExecutionStateKey(ulong Value)
{
    /// <summary>
    /// Gets the stable key used by stateless execution-state managers.
    /// </summary>
    public static ParserExecutionStateKey Stateless { get; } = new(0);
}
