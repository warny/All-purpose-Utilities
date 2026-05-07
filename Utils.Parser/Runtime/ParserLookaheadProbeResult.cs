namespace Utils.Parser.Runtime;

/// <summary>
/// Stores a lightweight structured look-ahead probe observation for a scheduled alternative start attempt.
/// </summary>
internal readonly record struct ParserLookaheadProbeResult(
    ParserLookaheadProbeKind Kind,
    string? TokenRuleName,
    string? TokenText,
    IReadOnlyList<string>? ExpectedTokenNames = null);
