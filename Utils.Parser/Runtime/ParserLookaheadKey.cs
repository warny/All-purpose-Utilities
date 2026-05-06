namespace Utils.Parser.Runtime;

/// <summary>
/// Identifies a scheduled alternative look-ahead observation for a parser rule at a specific input position.
/// </summary>
internal readonly record struct ParserLookaheadKey(
    string RuleName,
    int OriginPosition,
    int AlternativeIndex,
    int MinimumPrecedence);
