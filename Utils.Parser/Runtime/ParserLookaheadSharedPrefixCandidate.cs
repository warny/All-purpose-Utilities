namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a shallow shared first-token candidate observed across multiple alternatives.
/// </summary>
internal readonly record struct ParserLookaheadSharedPrefixCandidate(
    string TokenName,
    IReadOnlyList<int> AlternativeIndexes);
