namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a stable structural identity for a parser continuation point.
/// </summary>
/// <remarks>
/// This key is metadata-only and does not capture parser runtime state snapshots.
/// </remarks>
internal readonly record struct ParserContinuationKey(
    string RuleName,
    int AlternativeIndex,
    int SequencePosition);
