namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the result of one scheduled alternative execution attempt,
/// including both parse state and shallow look-ahead observation metadata.
/// </summary>
internal readonly record struct ScheduledAlternativeExecutionResult(
    ActiveParseState? State,
    ParserLookaheadProbeResult Probe);
