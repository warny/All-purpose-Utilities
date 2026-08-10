using System.Collections.Immutable;
namespace Utils.Parser.Runtime;
/// <summary>Represents an immutable shallow shared-prefix segment.</summary>
internal readonly record struct ParserSharedPrefixSegment
{
    /// <summary>Initializes a segment and captures its ordered structural tokens.</summary>
    /// <param name="sharedTokenName">Shared shallow token name.</param>
    /// <param name="structuralTokens">Ordered shared structural token names.</param>
    /// <param name="boundary">Conservative structural boundary.</param>
    public ParserSharedPrefixSegment(string sharedTokenName, IReadOnlyList<string> structuralTokens, ParserSharedPrefixBoundary boundary) { SharedTokenName = sharedTokenName; StructuralTokens = structuralTokens.ToImmutableArray(); Boundary = boundary; }
    public string SharedTokenName { get; }
    public IReadOnlyList<string> StructuralTokens { get; }
    public ParserSharedPrefixBoundary Boundary { get; }
}
