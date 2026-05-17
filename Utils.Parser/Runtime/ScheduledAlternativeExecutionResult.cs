namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the result of one scheduled alternative execution attempt,
/// including both parse state and shallow look-ahead observation metadata.
/// <para>
/// <see cref="State"/> is the only candidate for scheduler selection.
/// <see cref="Probe"/> is observable orchestration metadata and does not authorize acceptance on its own.
/// </para>
/// </summary>
internal readonly record struct ScheduledAlternativeExecutionResult(
    ActiveParseState? State,
    ParserLookaheadProbeResult Probe);
