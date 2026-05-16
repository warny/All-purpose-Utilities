namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a stable structural identity for a parser continuation point.
/// Identity is structural metadata and must not be interpreted as semantic-runtime equivalence.
/// </summary>
/// <remarks>
/// This key is metadata-only and does not capture parser runtime state snapshots.
/// </remarks>
internal readonly record struct ParserContinuationKey(
    string RuleName,
    int AlternativeIndex,
    int SequencePosition);
