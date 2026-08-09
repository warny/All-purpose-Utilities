using System.Collections.Immutable;
namespace Utils.Parser.Runtime;
/// <summary>Represents an immutable shallow shared first-token candidate.</summary>
internal readonly record struct ParserLookaheadSharedPrefixCandidate
{
    /// <summary>Initializes a candidate and captures the ordered alternative indexes.</summary>
    public ParserLookaheadSharedPrefixCandidate(string tokenName, IReadOnlyList<int> alternativeIndexes) { TokenName = tokenName; AlternativeIndexes = alternativeIndexes.ToImmutableArray(); }
    public string TokenName { get; }
    public IReadOnlyList<int> AlternativeIndexes { get; }
}
