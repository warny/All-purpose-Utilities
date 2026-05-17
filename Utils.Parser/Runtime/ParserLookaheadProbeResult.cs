namespace Utils.Parser.Runtime;

/// <summary>
/// Stores a lightweight structured look-ahead probe observation for a scheduled alternative start attempt.
/// Probe results are transport metadata: they are observable and scheduler-usable, but non-authoritative
/// and discardable without changing parse-tree authority or final parse acceptance rules.
/// </summary>
internal readonly record struct ParserLookaheadProbeResult(
    ParserLookaheadProbeKind Kind,
    string? TokenRuleName,
    string? TokenText,
    IReadOnlyList<string>? ExpectedTokenNames = null);
