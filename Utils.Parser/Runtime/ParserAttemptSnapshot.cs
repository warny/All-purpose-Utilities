namespace Utils.Parser.Runtime;

/// <summary>
/// Captures the parser cursor and opaque semantic execution state for one parser attempt-boundary.
/// This snapshot is intentionally internal transport only; it does not authorize replay, continuation execution,
/// quantifier rollback, left-recursive extension rollback, negation probe isolation, or action buffering.
/// </summary>
/// <param name="InputPosition">Token-stream position captured for the attempt boundary.</param>
/// <param name="ExecutionStateSnapshot">Opaque semantic execution-state snapshot captured by the runtime policy.</param>
internal readonly record struct ParserAttemptSnapshot(
    int InputPosition,
    object ExecutionStateSnapshot);
