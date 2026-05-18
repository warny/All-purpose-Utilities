namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a shallow shared first-token candidate observed across multiple alternatives.
/// This candidate is observational grouping metadata only:
/// it does not authorize execution sharing, replay, or branch merging.
/// </summary>
internal readonly record struct ParserLookaheadSharedPrefixCandidate(
    string TokenName,
    IReadOnlyList<int> AlternativeIndexes);
