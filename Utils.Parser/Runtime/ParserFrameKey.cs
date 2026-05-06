namespace Utils.Parser.Runtime;

/// <summary>
/// Key used for O(1) detection of left-recursive cycles in the parser engine.
/// A cycle is detected when the same rule is re-entered at the same token-list position.
/// </summary>
internal readonly record struct ParserFrameKey(string RuleName, int InputPosition);
