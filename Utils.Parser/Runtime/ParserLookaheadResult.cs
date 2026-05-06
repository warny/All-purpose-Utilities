namespace Utils.Parser.Runtime;

/// <summary>
/// Stores a lightweight look-ahead observation for a scheduled alternative start attempt.
/// </summary>
internal readonly record struct ParserLookaheadResult(
    bool CanStart,
    string? TokenRuleName,
    string? TokenText);
